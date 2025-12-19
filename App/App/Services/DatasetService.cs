using System.Text.RegularExpressions;
using Core;
using TreeSwapFile;

namespace Services;

public class DatasetService : IDisposable
    {
        private readonly string _swapFilePath;
        private readonly BinaryTreeFileStorage _batchStorage;
        
        // Índices dos lotes salvos no disco (apenas ponteiros long, baixo consumo)
        private List<long> _trainBatchOffsets;
        private List<long> _validationBatchOffsets;
        
        private int _batchSize;
        private int _contextWindowSize;

        public DatasetService(string swapFilePath)
        {
            _swapFilePath = swapFilePath;
            var batchStoragePath = Path.Combine(Path.GetDirectoryName(swapFilePath) ?? "Dayson", "batches.bts");
            
            // Garante diretório
            Directory.CreateDirectory(Path.GetDirectoryName(batchStoragePath)!);
            
            _batchStorage = new BinaryTreeFileStorage(batchStoragePath);
            _trainBatchOffsets = new List<long>();
            _validationBatchOffsets = new List<long>();
        }

        /// <summary>
        /// Lê o dataset do disco (streaming), tokeniza via SQLite e grava lotes binários.
        /// </summary>
        public void InitializeAndSplit(
            string datasetPath, 
            int contextWindowSize, 
            VocabularyManager vocabManager, // Agora recebe o Manager, não o Dictionary
            string padToken, 
            int batchSize, 
            float validationSplit)
        {
            if (!File.Exists(datasetPath))
                throw new FileNotFoundException("Dataset não encontrado", datasetPath);

            Console.WriteLine($"[DatasetService] Iniciando processamento (Streaming) do dataset...");
            
            _batchStorage.Clear();
            _trainBatchOffsets.Clear();
            _validationBatchOffsets.Clear();
            _batchSize = batchSize;
            _contextWindowSize = contextWindowSize;

            // 1. Tokenização via Streaming (Memory Efficient)
            // Converte texto -> int[] linha por linha sem carregar o arquivo todo
            var allIndices = new List<int>();
            int padTokenId = vocabManager.GetTokenIndex(padToken);

            using (var reader = new StreamReader(datasetPath))
            {
                string line;
                long lineCount = 0;
                while ((line = reader.ReadLine()) != null)
                {
                    if (string.IsNullOrWhiteSpace(line)) continue;

                    // Regex simples para tokenizar (mesma lógica do VocabularyManager)
                    var tokens = Regex.Split(line.ToLower(), @"(\p{L}+|\p{N}+|[.,!?;:'""/\-])")
                                      .Where(x => !string.IsNullOrWhiteSpace(x));

                    foreach (var token in tokens)
                    {
                        // Busca ID no SQLite (cacheado)
                        int id = vocabManager.GetTokenIndex(token);
                        allIndices.Add(id);
                    }

                    lineCount++;
                    if (lineCount % 10000 == 0) 
                        Console.Write($"\r[DatasetService] Processando linhas: {lineCount:N0}");
                }
            }
            Console.WriteLine($"\n[DatasetService] Total de tokens carregados: {allIndices.Count:N0}");

            // Validação de tamanho mínimo
            int totalSequences = Math.Max(0, allIndices.Count - contextWindowSize);
            if (totalSequences == 0) 
                throw new Exception("Dataset muito pequeno para a janela de contexto.");

            int validationSize = (int)(totalSequences * validationSplit);
            int trainSize = totalSequences - validationSize;

            Console.WriteLine($"[DatasetService] Treino: {trainSize:N0} seqs | Validação: {validationSize:N0} seqs");

            // 2. Geração e Gravação dos Lotes
            Console.WriteLine("[DatasetService] Gerando lotes e gravando no disco...");

            // Gera lotes de Treino
            GenerateBatches(allIndices, 0, trainSize, contextWindowSize, batchSize, _trainBatchOffsets);
            
            // Gera lotes de Validação
            GenerateBatches(allIndices, trainSize, totalSequences, contextWindowSize, batchSize, _validationBatchOffsets);

            _batchStorage.Flush();

            // 3. Limpeza Crítica de Memória
            // O array allIndices pode ser grande (ex: 100MB para 25M tokens), 
            // mas agora que salvamos os lotes no disco, não precisamos mais dele.
            allIndices.Clear();
            allIndices.TrimExcess();
            GC.Collect(2, GCCollectionMode.Forced, true);

            Console.WriteLine($"[DatasetService] Processamento concluído. RAM liberada.");
            Console.WriteLine($"[DatasetService] Lotes Treino: {_trainBatchOffsets.Count} | Lotes Validação: {_validationBatchOffsets.Count}");
        }

        private void GenerateBatches(List<int> data, int startIndex, int count, int contextWindow, int batchSize, List<long> offsetsList)
        {
            var currentBatch = new List<(int[] Input, int[] Target)>(batchSize);
            int seqLen = contextWindow; 

            // Ajuste de segurança: O último índice possível para começar uma sequência é:
            // (Total de Elementos) - (Tamanho da Sequência) - 1 (para o target shiftado)
            // Ex: Se temos 100 itens, seqLen 10.
            // O último input começa em 89 (vai até 98), target começa em 90 (vai até 99).
            // Se começar em 90, input vai até 99, target vai até 100 (BOOM).
            
            int maxSafeStartIndex = data.Count - seqLen - 1;

            for (int i = 0; i < count; i++)
            {
                int absoluteIndex = startIndex + i;
                
                // 🔥 CORREÇÃO BLINDADA: Se ultrapassar o limite seguro, encerra o loop deste lote imediatamente.
                if (absoluteIndex > maxSafeStartIndex)
                {
                    // Console.WriteLine($"[DatasetService] Fim seguro atingido no índice {absoluteIndex}. Parando geração.");
                    break;
                }
                
                int[] input = new int[seqLen];
                int[] target = new int[seqLen];

                // Copia Input
                data.CopyTo(absoluteIndex, input, 0, seqLen);
                
                // Copia Target (deslocado em 1)
                data.CopyTo(absoluteIndex + 1, target, 0, seqLen);

                currentBatch.Add((input, target));

                if (currentBatch.Count == batchSize)
                {
                    long offset = SaveBatchToDisk(currentBatch);
                    if (offset != -1) offsetsList.Add(offset);
                    currentBatch.Clear();
                }
            }

            // Salva o último lote parcial
            if (currentBatch.Count > 0)
            {
                long offset = SaveBatchToDisk(currentBatch);
                if (offset != -1) offsetsList.Add(offset);
            }
        }

        private long SaveBatchToDisk(List<(int[] Input, int[] Target)> batch)
        {
            try
            {
                using (var ms = new MemoryStream())
                using (var writer = new BinaryWriter(ms))
                {
                    // Formato do Lote em Bytes:
                    // [Int32: Count]
                    // Loop:
                    //   [Int32: SeqLen]
                    //   [Bytes: InputData]
                    //   [Int32: SeqLen]
                    //   [Bytes: TargetData]

                    writer.Write(batch.Count);

                    foreach (var item in batch)
                    {
                        // Inputs
                        writer.Write(item.Input.Length);
                        byte[] inputBytes = new byte[item.Input.Length * sizeof(int)];
                        Buffer.BlockCopy(item.Input, 0, inputBytes, 0, inputBytes.Length);
                        writer.Write(inputBytes);

                        // Targets
                        writer.Write(item.Target.Length);
                        byte[] targetBytes = new byte[item.Target.Length * sizeof(int)];
                        Buffer.BlockCopy(item.Target, 0, targetBytes, 0, targetBytes.Length);
                        writer.Write(targetBytes);
                    }

                    return _batchStorage.StoreData(ms.ToArray());
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[DatasetService] Erro ao salvar lote: {ex.Message}");
                return -1;
            }
        }

        public List<(int[] InputIndices, int[] TargetIndices)>? LoadBatchFromDisk(long offset)
        {
            if (offset < 0) return null;

            try
            {
                byte[] data = _batchStorage.GetDataBytes(offset);
                if (data == null || data.Length == 0) return null;

                var batch = new List<(int[], int[])>();

                using (var ms = new MemoryStream(data))
                using (var reader = new BinaryReader(ms))
                {
                    int count = reader.ReadInt32();

                    for (int i = 0; i < count; i++)
                    {
                        // Lê Input
                        int inputLen = reader.ReadInt32();
                        byte[] inputBytes = reader.ReadBytes(inputLen * sizeof(int));
                        int[] inputArr = new int[inputLen];
                        Buffer.BlockCopy(inputBytes, 0, inputArr, 0, inputBytes.Length);

                        // Lê Target
                        int targetLen = reader.ReadInt32();
                        byte[] targetBytes = reader.ReadBytes(targetLen * sizeof(int));
                        int[] targetArr = new int[targetLen];
                        Buffer.BlockCopy(targetBytes, 0, targetArr, 0, targetBytes.Length);

                        batch.Add((inputArr, targetArr));
                    }
                }

                return batch;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[DatasetService] Falha leitura offset {offset}: {ex.Message}");
                return null;
            }
        }

        public List<long> GetTrainBatchOffsets() => _trainBatchOffsets;
        public List<long> GetValidationBatchOffsets() => _validationBatchOffsets;

        public void Dispose()
        {
            _trainBatchOffsets?.Clear();
            _validationBatchOffsets?.Clear();
            _batchStorage?.Dispose();
            GC.SuppressFinalize(this);
        }
    }