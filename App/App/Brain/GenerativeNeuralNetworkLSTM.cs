using Core;
using Interfaces;

namespace Brain;

public class GenerativeNeuralNetworkLSTM : NeuralNetworkLSTM
    {
        public readonly VocabularyManager vocabularyManager;
        private readonly ISearchService searchService;
        private readonly int _embeddingSize;

        /// <summary>
        /// Construtor para criar um novo modelo generativo para treinamento.
        /// </summary>
        public GenerativeNeuralNetworkLSTM(int vocabSize, int embeddingSize, int hiddenSize, string datasetPath,
            ISearchService? searchService, IMathEngine mathEngine)
            : base(vocabSize, embeddingSize, hiddenSize, vocabSize, mathEngine)
        {
            this.vocabularyManager = new VocabularyManager();
            this.searchService = searchService ?? new MockSearchService();
            this._embeddingSize = embeddingSize;

            // Constrói ou carrega o vocabulário do SQLite
            int loadedVocabSize = vocabularyManager.BuildVocabulary(datasetPath);
            if (loadedVocabSize == 0)
            {
                throw new InvalidOperationException("Vocabulário vazio. Verifique o arquivo de dataset.");
            }
        }

        /// <summary>
        /// Construtor privado para "envolver" um modelo base já carregado.
        /// </summary>
        private GenerativeNeuralNetworkLSTM(NeuralNetworkLSTM baseModel,
            VocabularyManager vocabManager, ISearchService? searchService)
            : base(baseModel) // Chama construtor protegido de cópia da classe base
        {
            if (baseModel == null)
                throw new ArgumentNullException(nameof(baseModel), "Modelo base não pode ser nulo");

            this.vocabularyManager = vocabManager ?? throw new ArgumentNullException(nameof(vocabManager));
            this.searchService = searchService ?? new MockSearchService();

            if (_tensorManager == null || string.IsNullOrEmpty(_weightsEmbeddingId))
            {
                throw new InvalidOperationException("Modelo base está em estado inválido.");
            }

            try
            {
                var shape = _tensorManager.GetShape(_weightsEmbeddingId);
                if (shape == null || shape.Length < 2)
                {
                    throw new InvalidOperationException($"Shape do embedding inválido.");
                }
                this._embeddingSize = shape[1];
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Falha ao inicializar GenerativeNeuralNetworkLSTM: {ex.Message}", ex);
            }
            Console.WriteLine($"[GenerativeNeuralNetworkLSTM] Inicializado com embedding size: {_embeddingSize}");
        }

        /// <summary>
        /// Método de fábrica estático para carregar um modelo e envolvê-lo.
        /// </summary>
        public static GenerativeNeuralNetworkLSTM? Load(string modelPath, IMathEngine mathEngine,
            VocabularyManager vocabManager, ISearchService? searchService)
        {
            var baseModel = NeuralNetworkLSTM.LoadModel(modelPath, mathEngine);
            if (baseModel == null)
            {
                return null;
            }
            return new GenerativeNeuralNetworkLSTM(baseModel, vocabManager, searchService);
        }

        /// <summary>
        /// Gera uma continuação de texto a partir de um prompt.
        /// </summary>
        public string GenerateResponse(string inputText, int maxLength = 50)
        {
             if (string.IsNullOrEmpty(inputText)) return "Erro: Entrada vazia ou nula.";

             // Tokenização usando SQLite
             var tokens = Tokenize(inputText);
             var inputIndices = tokens.Select(t => GetTokenIndex(t)).ToArray();

             // Placeholder: A inferência completa token-a-token requer um loop de ForwardPass
             // mantendo o estado (h, c) e amostrando o próximo token.
             return $"[Modelo Carregado] Resposta simulada para: '{inputText}' (Inferência real requer implementação do loop de amostragem)";
        }

        /// <summary>
        /// Calcula a perda para uma sequência para fins de validação.
        /// </summary>
        public float CalculateSequenceLoss(int[] inputIndices, int[] targetIndices)
        {
            // Usa um pool temporário para validação
            using (var pool = new TensorPool(_mathEngine))
            using (var masterScope = new TensorScope("CalculateLoss", _mathEngine, _tensorManager, pool))
            {
                var weights = new ModelWeights
                {
                    Embedding = masterScope.LoadTensor(_weightsEmbeddingId), W_if = masterScope.LoadTensor(_weightsInputForgetId), W_hf = masterScope.LoadTensor(_weightsHiddenForgetId), B_f = masterScope.LoadTensor(_biasForgetId),
                    W_ii = masterScope.LoadTensor(_weightsInputInputId), W_hi = masterScope.LoadTensor(_weightsHiddenInputId), B_i = masterScope.LoadTensor(_biasInputId),
                    W_ic = masterScope.LoadTensor(_weightsInputCellId), W_hc = masterScope.LoadTensor(_weightsHiddenCellId), B_c = masterScope.LoadTensor(_biasCellId),
                    W_io = masterScope.LoadTensor(_weightsInputOutputId), W_ho = masterScope.LoadTensor(_weightsHiddenOutputId), B_o = masterScope.LoadTensor(_biasOutputId),
                    W_hy = masterScope.LoadTensor(_weightsHiddenOutputFinalId), B_y = masterScope.LoadTensor(_biasOutputFinalId),
                    
                    // LayerNorm weights (carregados mas não usados se a lógica foi removida do Forward)
                    LN_f_gamma = masterScope.LoadTensor(_lnForgetGammaId), LN_f_beta = masterScope.LoadTensor(_lnForgetBetaId), 
                    LN_i_gamma = masterScope.LoadTensor(_lnInputGammaId), LN_i_beta = masterScope.LoadTensor(_lnInputBetaId),
                    LN_c_gamma = masterScope.LoadTensor(_lnCellGammaId), LN_c_beta = masterScope.LoadTensor(_lnCellBetaId), 
                    LN_o_gamma = masterScope.LoadTensor(_lnOutputGammaId), LN_o_beta = masterScope.LoadTensor(_lnOutputBetaId)
                };

                var (loss, swapFiles) = base.RunForwardPassForInference(inputIndices, targetIndices, weights);

                // Limpeza imediata dos swaps
                foreach (var file in swapFiles)
                {
                    _swapManager.DeleteSwapFile(file);
                }
                
                return loss;
            }
        }

        public void Reset()
        {
            base.ResetHiddenState();
        }
        
        private int GetTokenIndex(string token)
        {
            // Chama o método otimizado do VocabularyManager (SQLite + Cache)
            return vocabularyManager.GetTokenIndex(token.ToLower());
        }

        private string GetTokenFromIndex(int id)
        {
            return vocabularyManager.GetToken(id);
        }

        private string[] Tokenize(string text)
        {
            // Tokenização simples por espaço (pode ser melhorada para usar Regex igual ao DatasetService)
            return text.ToLower().Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
        }

        public void RunSanityCheckZeroRAM()
        {
            Console.WriteLine("\n╔═══════════════════════════════════════════════════════════╗");
            Console.WriteLine("║    🚀 VERIFICAÇÃO DE SANIDADE (VRAM OTIMIZADA)            ║");
            Console.WriteLine("╚═══════════════════════════════════════════════════════════╝");

            var inputIndices = new[] { 5, 10 };
            var targetIndices = new[] { 10, 15 };
            Console.WriteLine($"[Sanity] Input={{{string.Join(",", inputIndices)}}}, Target={{{string.Join(",", targetIndices)}}}");

            try
            {
                using (var pool = new TensorPool(_mathEngine))
                using (var masterScope = new TensorScope("SanityCheckMaster", _mathEngine, _tensorManager, pool))
                {
                    // Carrega Pesos
                    var weights = new ModelWeights {
                        Embedding = masterScope.LoadTensor(_weightsEmbeddingId), 
                        W_if = masterScope.LoadTensor(_weightsInputForgetId), W_hf = masterScope.LoadTensor(_weightsHiddenForgetId), B_f = masterScope.LoadTensor(_biasForgetId),
                        W_ii = masterScope.LoadTensor(_weightsInputInputId), W_hi = masterScope.LoadTensor(_weightsHiddenInputId), B_i = masterScope.LoadTensor(_biasInputId),
                        W_ic = masterScope.LoadTensor(_weightsInputCellId), W_hc = masterScope.LoadTensor(_weightsHiddenCellId), B_c = masterScope.LoadTensor(_biasCellId),
                        W_io = masterScope.LoadTensor(_weightsInputOutputId), W_ho = masterScope.LoadTensor(_weightsHiddenOutputId), B_o = masterScope.LoadTensor(_biasOutputId),
                        W_hy = masterScope.LoadTensor(_weightsHiddenOutputFinalId), B_y = masterScope.LoadTensor(_biasOutputFinalId),
                        LN_f_gamma = masterScope.LoadTensor(_lnForgetGammaId), LN_f_beta = masterScope.LoadTensor(_lnForgetBetaId), 
                        LN_i_gamma = masterScope.LoadTensor(_lnInputGammaId), LN_i_beta = masterScope.LoadTensor(_lnInputBetaId),
                        LN_c_gamma = masterScope.LoadTensor(_lnCellGammaId), LN_c_beta = masterScope.LoadTensor(_lnCellBetaId), 
                        LN_o_gamma = masterScope.LoadTensor(_lnOutputGammaId), LN_o_beta = masterScope.LoadTensor(_lnOutputBetaId)
                    };
                    
                    Console.WriteLine("\n[Sanity] Executando Pipeline (Forward + Backward) na VRAM...");
                    
                    var (loss, grads) = base.ProcessSequenceVramCached(inputIndices, targetIndices, weights, masterScope);

                    Console.WriteLine($"[Sanity] Perda calculada: {loss:F4}");

                    if (float.IsNaN(loss) || float.IsInfinity(loss)) 
                        throw new InvalidOperationException($"Falha: Perda inválida ({loss}).");
                    
                    double totalGradSum = 0;
                    foreach (var grad in grads.Values)
                    {
                        totalGradSum += _mathEngine.CalculateSumOfSquares(grad);
                    }
                    totalGradSum = Math.Sqrt(totalGradSum); // Norma L2 aproximada

                    Console.WriteLine($"[Sanity] Norma dos Gradientes: {totalGradSum:E2}");
                    if (totalGradSum < 1e-9) 
                        throw new InvalidOperationException("Falha: Vanishing Gradient (Gradiente zero).");

                    Console.WriteLine("\n[Sanity] Testando Update do Otimizador...");
                    // Simula update
                    _adamOptimizer.UpdateParametersGpu(0, weights.W_hy, grads["W_hy"], _mathEngine);
                    Console.WriteLine("[Sanity] Update OK.");
                }

                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("╔═══════════════════════════════════════════════════════════╗");
                Console.WriteLine("║         ✅ VERIFICAÇÃO DE SANIDADE CONCLUÍDA!             ║");
                Console.WriteLine("╚═══════════════════════════════════════════════════════════╝");
                Console.ResetColor();
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"[Sanity] ERRO CRÍTICO: {ex.Message}");
                Console.ResetColor();
                throw; 
            }
        }
    }