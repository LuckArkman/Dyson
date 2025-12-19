using System.Diagnostics;
using Core;
using Gpu;
using Interfaces;
using Services;

namespace Brain;

public class ModelTrainerLSTM
    {
        private readonly IMathEngine _mathEngine;
        private readonly Stopwatch _stopwatch = new Stopwatch();
        private readonly Stopwatch _loteswatch = new Stopwatch();
        private readonly Process _currentProcess;
        private readonly ISearchService _searchService;
        private readonly GpuSyncGuard? _syncGuard;
        
        // 🔥 CORREÇÃO: Limites ajustados para evitar GC excessivo em máquinas com 16GB+ RAM
        private const long CRITICAL_MEMORY_MB = 14000; 
        private const long MEMORY_TRIM_THRESHOLD_MB = 10000; // GC apenas se passar de 10GB
        
        private long _peakMemoryUsageMB = 0;
        private long _lastTrimMemory = 0;
        private readonly string logPath = Path.Combine(Environment.CurrentDirectory, "Dayson", "training_log.txt");

        public ModelTrainerLSTM(IMathEngine mathEngine)
        {
            _mathEngine = mathEngine ?? throw new ArgumentNullException(nameof(mathEngine));
            _currentProcess = Process.GetCurrentProcess();
            _searchService = new MockSearchService();
            if (mathEngine is GpuMathEngine gpuEngine)
            {
                _syncGuard = gpuEngine.GetSyncGuard();
            }
        }

        public GenerativeNeuralNetworkLSTM? TrainModel(
            GenerativeNeuralNetworkLSTM initialModel,
            string datasetPath,
            string finalModelPath,
            float learningRate,
            int epochs,
            int batchSize,
            int contextWindowSize,
            float validationSplit)
        {
            int failedBatches = 0;
            if (!File.Exists(datasetPath))
                throw new FileNotFoundException("Arquivo de dataset não encontrado.", datasetPath);

            var swapFilePath = Path.Combine(Environment.CurrentDirectory, "Dayson", "batches.bts");
            
            // Listas para armazenar os offsets dos lotes no disco
            List<long> trainBatchIndices;
            List<long> validationBatchIndices;

            using (var datasetService = new DatasetService(swapFilePath))
            {
                // 1. Prepara o Dataset
                datasetService.InitializeAndSplit(
                    datasetPath, 
                    contextWindowSize,
                    initialModel.vocabularyManager,
                    "<PAD>", 
                    batchSize, 
                    validationSplit
                );

                trainBatchIndices = datasetService.GetTrainBatchOffsets();
                validationBatchIndices = datasetService.GetValidationBatchOffsets();

                Console.WriteLine($"\n[Trainer] Configuração do Ciclo de Treinamento Híbrido (VRAM Optimized).");
                Console.WriteLine($"[Trainer] Memory Watchdog ativado: Limite de ~{MEMORY_TRIM_THRESHOLD_MB} MB.");

                GenerativeNeuralNetworkLSTM? currentModel = initialModel;
                TimeSpan totalElapsedTime = TimeSpan.Zero;

                for (int epoch = 0; epoch < epochs; epoch++)
                {
                    _stopwatch.Restart();
                    Console.WriteLine($"\n{'═',60}");
                    Console.WriteLine($"ÉPOCA {epoch + 1}/{epochs} >> Learning Rate: {learningRate} >> {DateTime.UtcNow}");
                    Console.WriteLine($"Total Batches: {trainBatchIndices.Count}");
                    
                    if (currentModel != null) currentModel.warmupSteps = 50;
                    Console.WriteLine($"{'═',60}");

                    double totalEpochLoss = 0;
                    int batchCount = 0;

                    // ========================================================================
                    // INÍCIO DO ESCOPO DA ÉPOCA
                    // ========================================================================
                    
                    using (var pool = new TensorPool(_mathEngine))
                    using (var epochScope = new TensorScope($"Epoch_{epoch + 1}", _mathEngine, currentModel.GetTensorManager(), pool))
                    {
                        Console.WriteLine($"[Trainer] Carregando pesos do modelo para a VRAM...");
                        
                        var weights = new ModelWeights
                        {
                            Embedding = epochScope.LoadTensor(currentModel.GetWeightsEmbeddingId()),
                            W_if = epochScope.LoadTensor(currentModel.GetWeightsInputForgetId()),
                            W_hf = epochScope.LoadTensor(currentModel.GetWeightsHiddenForgetId()),
                            B_f = epochScope.LoadTensor(currentModel.GetBiasForgetId()),
                            W_ii = epochScope.LoadTensor(currentModel.GetWeightsInputInputId()),
                            W_hi = epochScope.LoadTensor(currentModel.GetWeightsHiddenInputId()),
                            B_i = epochScope.LoadTensor(currentModel.GetBiasInputId()),
                            W_ic = epochScope.LoadTensor(currentModel.GetWeightsInputCellId()),
                            W_hc = epochScope.LoadTensor(currentModel.GetWeightsHiddenCellId()),
                            B_c = epochScope.LoadTensor(currentModel.GetBiasCellId()),
                            W_io = epochScope.LoadTensor(currentModel.GetWeightsInputOutputId()),
                            W_ho = epochScope.LoadTensor(currentModel.GetWeightsHiddenOutputId()),
                            B_o = epochScope.LoadTensor(currentModel.GetBiasOutputId()),
                            W_hy = epochScope.LoadTensor(currentModel.GetWeightsHiddenOutputFinalId()),
                            B_y = epochScope.LoadTensor(currentModel.GetBiasOutputFinalId()),
                            // Layer Norm
                            LN_f_gamma = epochScope.LoadTensor(currentModel.GetLnForgetGammaId()),
                            LN_f_beta = epochScope.LoadTensor(currentModel.GetLnForgetBetaId()),
                            LN_i_gamma = epochScope.LoadTensor(currentModel.GetLnInputGammaId()),
                            LN_i_beta = epochScope.LoadTensor(currentModel.GetLnInputBetaId()),
                            LN_c_gamma = epochScope.LoadTensor(currentModel.GetLnCellGammaId()),
                            LN_c_beta = epochScope.LoadTensor(currentModel.GetLnCellBetaId()),
                            LN_o_gamma = epochScope.LoadTensor(currentModel.GetLnOutputGammaId()),
                            LN_o_beta = epochScope.LoadTensor(currentModel.GetLnOutputBetaId())
                        };
                        Console.WriteLine("[Trainer] Pesos carregados.");

                        foreach (var batchIndex in trainBatchIndices)
                        {
                            _loteswatch.Restart();
                            List<(int[] InputIndices, int[] TargetIndices)>? batch = null;
                            try
                            {
                                batch = datasetService.LoadBatchFromDisk(batchIndex);
                                if (batch == null || !batch.Any()) continue;

                                if (currentModel != null)
                                {
                                    // 🔥 VRAM Cached Training
                                    double batchLoss = currentModel.TrainBatch(batch, learningRate, weights, epochScope);
                                    
                                    totalEpochLoss += batchLoss;
                                    batchCount++;
                                    Console.WriteLine($"Batch {batchCount}/{trainBatchIndices.Count} | Loss: {batchLoss:F4} | {(int)_loteswatch.ElapsedMilliseconds}ms    ");
                                }

                                // === MANUTENÇÃO OTIMIZADA ===

                                // 1. Limpeza do Pool (Frequência reduzida para 500)
                                if (batchCount % 500 == 0) 
                                {
                                    pool.Trim();
                                    
                                    // Sync GPU periodico
                                    if (_mathEngine is GpuMathEngine gpuEngine2)
                                    {
                                        gpuEngine2.Synchronize();
                                        gpuEngine2.FlushQueue();
                                    }
                                }

                                // 2. Watchdog de RAM (Apenas se exceder 10GB e tiver crescido 512MB)
                                if (batchCount % 500 == 0)
                                {
                                    long currentMemoryMB = GetCurrentMemoryUsageMB();
                                    if (currentMemoryMB > MEMORY_TRIM_THRESHOLD_MB && currentMemoryMB > (_lastTrimMemory + 512))
                                    {
                                        Console.Write($"\n⚠️ [RAM WATCHDOG] Uso: {currentMemoryMB}MB. GC... ");
                                        
                                        if (_syncGuard != null) _syncGuard.SynchronizeBeforeRead("EmergencyCleanup");
                                        
                                        pool.Trim();
                                        ForceAggressiveGarbageCollection();
                                        
                                        long memoryAfterGC = GetCurrentMemoryUsageMB();
                                        Console.WriteLine($"-> {memoryAfterGC}MB");
                                        _lastTrimMemory = memoryAfterGC;
                                    }
                                }
                            }
                            catch (Exception ex)
                            {
                                failedBatches++;
                                Console.WriteLine($"\n[ERRO] Lote {batchIndex}: {ex.Message}");
                                if (failedBatches > 10) throw new Exception($"Muitos erros ({failedBatches}). Abortando.");
                            }
                            finally
                            {
                                if (batch != null) { CleanupSingleBatch(batch); batch = null; }
                            }
                        }
                    } // Fim do scope

                    _stopwatch.Stop();
                    totalElapsedTime += _stopwatch.Elapsed;
                    
                    double avgLoss = batchCount > 0 ? totalEpochLoss / batchCount : float.PositiveInfinity;
                    string elapsedFormatted = $"{(int)_stopwatch.Elapsed.TotalHours:D2}:{_stopwatch.Elapsed.Minutes:D2}:{_stopwatch.Elapsed.Seconds:D2}";
                    
                    Console.WriteLine($"\nÉpoca {epoch + 1} Finalizada. Loss Média: {avgLoss:F4} | Duração: {elapsedFormatted}");
                    File.AppendAllText(logPath, $"Época {epoch + 1}: Loss {avgLoss:F4}, Tempo {elapsedFormatted}{Environment.NewLine}");

                    // === CHECKPOINT ===
                    if (currentModel != null)
                    {
                        string modelPathForEpoch = Path.Combine(Path.GetDirectoryName(finalModelPath)!, $"dayson_{epoch + 1}.json");
                        Console.WriteLine($"[Checkpoint] Salvando {modelPathForEpoch}...");
                        currentModel.SaveModel(modelPathForEpoch);
                    }

                    // Limpeza entre épocas
                    ForceAggressiveGarbageCollection();
                }

                if (currentModel != null)
                {
                    string lastModelPath = Path.Combine(Path.GetDirectoryName(finalModelPath)!, $"dayson_final.json");
                    currentModel.SaveModel(lastModelPath);
                }

                return currentModel;
            }
        }

        private void CleanupSingleBatch(List<(int[] InputIndices, int[] TargetIndices)> batch)
        {
            if (batch == null) return;
            batch.Clear();
            batch.TrimExcess();
        }

        private void ForceAggressiveGarbageCollection()
        {
            GC.Collect(2, GCCollectionMode.Forced, true, true);
            GC.WaitForPendingFinalizers();
            GC.Collect(2, GCCollectionMode.Forced, true, true);
        }

        private long GetCurrentMemoryUsageMB()
        {
            _currentProcess.Refresh();
            long currentMemory = _currentProcess.WorkingSet64 / (1024 * 1024);
            if (currentMemory > _peakMemoryUsageMB) _peakMemoryUsageMB = currentMemory;
            return currentMemory;
        }

        private double ValidateModel(GenerativeNeuralNetworkLSTM modelToValidate, DatasetService datasetService, List<long> validationBatchIndices)
        {
            Console.WriteLine($"\n[Validação] Iniciando avaliação em {validationBatchIndices.Count} lotes de teste...");
            var sw = Stopwatch.StartNew();
            
            double totalLoss = 0;
            long totalSequences = 0;
            int batchCounter = 0;

            // 1. Cria Pool e Escopo exclusivos para validação (isola da memória de treino)
            using (var pool = new TensorPool(_mathEngine))
            using (var valScope = new TensorScope("Validation_Scope", _mathEngine, modelToValidate.GetTensorManager(), pool))
            {
                // 2. Carrega TODOS os pesos para a VRAM uma única vez.
                // Isso é crucial para performance. Se carregássemos por lote, demoraria horas.
                var weights = new ModelWeights
                {
                    Embedding = valScope.LoadTensor(modelToValidate.GetWeightsEmbeddingId()),
                    W_if = valScope.LoadTensor(modelToValidate.GetWeightsInputForgetId()),
                    W_hf = valScope.LoadTensor(modelToValidate.GetWeightsHiddenForgetId()),
                    B_f = valScope.LoadTensor(modelToValidate.GetBiasForgetId()),
                    W_ii = valScope.LoadTensor(modelToValidate.GetWeightsInputInputId()),
                    W_hi = valScope.LoadTensor(modelToValidate.GetWeightsHiddenInputId()),
                    B_i = valScope.LoadTensor(modelToValidate.GetBiasInputId()),
                    W_ic = valScope.LoadTensor(modelToValidate.GetWeightsInputCellId()),
                    W_hc = valScope.LoadTensor(modelToValidate.GetWeightsHiddenCellId()),
                    B_c = valScope.LoadTensor(modelToValidate.GetBiasCellId()),
                    W_io = valScope.LoadTensor(modelToValidate.GetWeightsInputOutputId()),
                    W_ho = valScope.LoadTensor(modelToValidate.GetWeightsHiddenOutputId()),
                    B_o = valScope.LoadTensor(modelToValidate.GetBiasOutputId()),
                    W_hy = valScope.LoadTensor(modelToValidate.GetWeightsHiddenOutputFinalId()),
                    B_y = valScope.LoadTensor(modelToValidate.GetBiasOutputFinalId()),
                    
                    // Layer Norm (Carregamos para garantir integridade do struct, mesmo se não usado no kernel simplificado)
                    LN_f_gamma = valScope.LoadTensor(modelToValidate.GetLnForgetGammaId()),
                    LN_f_beta = valScope.LoadTensor(modelToValidate.GetLnForgetBetaId()),
                    LN_i_gamma = valScope.LoadTensor(modelToValidate.GetLnInputGammaId()),
                    LN_i_beta = valScope.LoadTensor(modelToValidate.GetLnInputBetaId()),
                    LN_c_gamma = valScope.LoadTensor(modelToValidate.GetLnCellGammaId()),
                    LN_c_beta = valScope.LoadTensor(modelToValidate.GetLnCellBetaId()),
                    LN_o_gamma = valScope.LoadTensor(modelToValidate.GetLnOutputGammaId()),
                    LN_o_beta = valScope.LoadTensor(modelToValidate.GetLnOutputBetaId())
                };

                // 3. Itera sobre os lotes de validação
                foreach (var batchIndex in validationBatchIndices)
                {
                    var batch = datasetService.LoadBatchFromDisk(batchIndex);
                    if (batch == null || batch.Count == 0) continue;

                    try
                    {
                        foreach (var (input, target) in batch)
                        {
                            // Executa apenas o Forward Pass (VRAM Cached)
                            // Retorna a perda e uma lista vazia de arquivos swap (pois está otimizado)
                            var (loss, _) = modelToValidate.RunForwardPassForInference(input, target, weights);
                            
                            if (!float.IsNaN(loss) && !float.IsInfinity(loss))
                            {
                                totalLoss += loss;
                                totalSequences++;
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        // Log silencioso para não spammar o console, apenas ignora o batch corrompido
                        // Console.WriteLine($"[Validação] Erro lote {batchIndex}: {ex.Message}");
                    }
                    finally
                    {
                        batch.Clear();
                    }

                    batchCounter++;
                    // Feedback visual simples
                    if (batchCounter % 100 == 0)
                    {
                        Console.Write($"\r[Validação] Progresso: {batchCounter}/{validationBatchIndices.Count} | Loss Atual: {(totalSequences > 0 ? totalLoss / totalSequences : 0):F4}   ");
                    }
                }
            }

            sw.Stop();
            double avgLoss = totalSequences > 0 ? totalLoss / totalSequences : double.PositiveInfinity;

            Console.WriteLine($"\n[Validação] Concluída. Loss Média: {avgLoss:F4} | Tempo: {sw.Elapsed:mm\\:ss}");
            
            return avgLoss;
        }
    }