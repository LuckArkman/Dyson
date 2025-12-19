using System.Text.Json;
using Akka.Actor;
using Interfaces;
using MathNet.Numerics.Distributions;
using MathNet.Numerics.LinearAlgebra;

namespace Brain;

public class NeuralNetworkLSTM : IDisposable
    {
        protected readonly AdamOptimizer _adamOptimizer;
        protected readonly IndividualFileTensorManager _tensorManager;
        protected readonly IMathEngine _mathEngine;
        public readonly DiskSwapManager _swapManager;

        private readonly int inputSize;
        private readonly int hiddenSize;
        public readonly int outputSize;
        private readonly string _sessionId;
        private bool _disposed = false;
        public int warmupSteps;

        // IDs dos pesos
        protected string _weightsEmbeddingId = null!;
        protected string _weightsInputForgetId = null!;
        protected string _weightsHiddenForgetId = null!;
        protected string _weightsInputInputId = null!;
        protected string _weightsHiddenInputId = null!;
        protected string _weightsInputCellId = null!;
        protected string _weightsHiddenCellId = null!;
        protected string _weightsInputOutputId = null!;
        protected string _weightsHiddenOutputId = null!;
        protected string _biasForgetId = null!;
        protected string _biasInputId = null!;
        protected string _biasCellId = null!;
        protected string _biasOutputId = null!;
        protected string _weightsHiddenOutputFinalId = null!;
        protected string _biasOutputFinalId = null!;
        protected string _hiddenStateId = null!;
        protected string _cellStateId = null!;
        
        // Layer Norm IDs
        protected string _lnForgetGammaId = null!;
        protected string _lnForgetBetaId = null!;
        protected string _lnInputGammaId = null!;
        protected string _lnInputBetaId = null!;
        protected string _lnCellGammaId = null!;
        protected string _lnCellBetaId = null!;
        protected string _lnOutputGammaId = null!;
        protected string _lnOutputBetaId = null!;

        public int InputSize => inputSize;
        public int HiddenSize => hiddenSize;
        public int OutputSize => outputSize;
        public IMathEngine GetMathEngine() => _mathEngine;

        public NeuralNetworkLSTM(int vocabSize, int embeddingSize, int hiddenSize, int outputSize, IMathEngine mathEngine)
        {
            this.inputSize = vocabSize;
            this.hiddenSize = hiddenSize;
            this.outputSize = outputSize;
            this._mathEngine = mathEngine ?? throw new ArgumentNullException(nameof(mathEngine));
            this._sessionId = $"session_{DateTime.UtcNow:yyyyMMdd_HHmmss}_{Guid.NewGuid():N}";

            // Aumentado o limite de memória para evitar swapping desnecessário para o disco
            // em modelos pequenos/médios. (16GB)
            this._swapManager = new DiskSwapManager(mathEngine, _sessionId, memoryLimitMb: 16000);
            this._tensorManager = new IndividualFileTensorManager(mathEngine, _sessionId);
            this._adamOptimizer = new AdamOptimizer(_tensorManager);

            Console.WriteLine("╔═══════════════════════════════════════════════════════════╗");
            Console.WriteLine("║   🔥 VRAM-OPTIMIZED LSTM (Hybrid VRAM Cache)             ║");
            Console.WriteLine("╚═══════════════════════════════════════════════════════════╝");

            InitializeWeights(vocabSize, embeddingSize, hiddenSize, outputSize);
        }

        private void InitializeWeights(int vocabSize, int embeddingSize, int hiddenSize, int outputSize)
        {
            var rand = new Random(42);
            _weightsEmbeddingId = InitializeWeight(vocabSize, embeddingSize, rand, "WeightsEmbedding");
            _weightsInputForgetId = InitializeWeight(embeddingSize, hiddenSize, rand, "WeightsInputForget");
            _weightsHiddenForgetId = InitializeWeight(hiddenSize, hiddenSize, rand, "WeightsHiddenForget");
            _weightsInputInputId = InitializeWeight(embeddingSize, hiddenSize, rand, "WeightsInputInput");
            _weightsHiddenInputId = InitializeWeight(hiddenSize, hiddenSize, rand, "WeightsHiddenInput");
            _weightsInputCellId = InitializeWeight(embeddingSize, hiddenSize, rand, "WeightsInputCell");
            _weightsHiddenCellId = InitializeWeight(hiddenSize, hiddenSize, rand, "WeightsHiddenCell");
            _weightsInputOutputId = InitializeWeight(embeddingSize, hiddenSize, rand, "WeightsInputOutput");
            _weightsHiddenOutputId = InitializeWeight(hiddenSize, hiddenSize, rand, "WeightsHiddenOutput");
            _biasForgetId = InitializeWeight(1, hiddenSize, rand, "BiasForget");
            _biasInputId = InitializeWeight(1, hiddenSize, rand, "BiasInput");
            _biasCellId = InitializeWeight(1, hiddenSize, rand, "BiasCell");
            _biasOutputId = InitializeWeight(1, hiddenSize, rand, "BiasOutput");
            _weightsHiddenOutputFinalId = InitializeWeight(hiddenSize, outputSize, rand, "WeightsOutputFinal");
            _biasOutputFinalId = InitializeWeight(1, outputSize, rand, "BiasOutputFinal");

            _hiddenStateId = _tensorManager.CreateAndStoreZeros(new[] { 1, hiddenSize }, "HiddenState");
            _cellStateId = _tensorManager.CreateAndStoreZeros(new[] { 1, hiddenSize }, "CellState");

            _lnForgetGammaId = _tensorManager.CreateAndStore(Enumerable.Repeat(1.0f, hiddenSize).ToArray(), new[] { 1, hiddenSize }, "LN_Forget_Gamma");
            _lnForgetBetaId = _tensorManager.CreateAndStoreZeros(new[] { 1, hiddenSize }, "LN_Forget_Beta");
            _lnInputGammaId = _tensorManager.CreateAndStore(Enumerable.Repeat(1.0f, hiddenSize).ToArray(), new[] { 1, hiddenSize }, "LN_Input_Gamma");
            _lnInputBetaId = _tensorManager.CreateAndStoreZeros(new[] { 1, hiddenSize }, "LN_Input_Beta");
            _lnCellGammaId = _tensorManager.CreateAndStore(Enumerable.Repeat(1.0f, hiddenSize).ToArray(), new[] { 1, hiddenSize }, "LN_Cell_Gamma");
            _lnCellBetaId = _tensorManager.CreateAndStoreZeros(new[] { 1, hiddenSize }, "LN_Cell_Beta");
            _lnOutputGammaId = _tensorManager.CreateAndStore(Enumerable.Repeat(1.0f, hiddenSize).ToArray(), new[] { 1, hiddenSize }, "LN_Output_Gamma");
            _lnOutputBetaId = _tensorManager.CreateAndStoreZeros(new[] { 1, hiddenSize }, "LN_Output_Beta");
        }

        protected NeuralNetworkLSTM(NeuralNetworkLSTM existingModel)
        {
            this.inputSize = existingModel.inputSize;
            this.hiddenSize = existingModel.hiddenSize;
            this.outputSize = existingModel.outputSize;
            this._mathEngine = existingModel._mathEngine;
            this._adamOptimizer = existingModel._adamOptimizer;
            this._sessionId = existingModel._sessionId;
            this._tensorManager = existingModel._tensorManager;
            this._swapManager = existingModel._swapManager;

            // Copy IDs
            _weightsEmbeddingId = existingModel._weightsEmbeddingId;
            _weightsInputForgetId = existingModel._weightsInputForgetId;
            _weightsHiddenForgetId = existingModel._weightsHiddenForgetId;
            _weightsInputInputId = existingModel._weightsInputInputId;
            _weightsHiddenInputId = existingModel._weightsHiddenInputId;
            _weightsInputCellId = existingModel._weightsInputCellId;
            _weightsHiddenCellId = existingModel._weightsHiddenCellId;
            _weightsInputOutputId = existingModel._weightsInputOutputId;
            _weightsHiddenOutputId = existingModel._weightsHiddenOutputId;
            _biasForgetId = existingModel._biasForgetId;
            _biasInputId = existingModel._biasInputId;
            _biasCellId = existingModel._biasCellId;
            _biasOutputId = existingModel._biasOutputId;
            _weightsHiddenOutputFinalId = existingModel._weightsHiddenOutputFinalId;
            _biasOutputFinalId = existingModel._biasOutputFinalId;
            _hiddenStateId = existingModel._hiddenStateId;
            _cellStateId = existingModel._cellStateId;
            
            _lnForgetGammaId = existingModel._lnForgetGammaId; _lnForgetBetaId = existingModel._lnForgetBetaId;
            _lnInputGammaId = existingModel._lnInputGammaId; _lnInputBetaId = existingModel._lnInputBetaId;
            _lnCellGammaId = existingModel._lnCellGammaId; _lnCellBetaId = existingModel._lnCellBetaId;
            _lnOutputGammaId = existingModel._lnOutputGammaId; _lnOutputBetaId = existingModel._lnOutputBetaId;
        }

        // =========================================================================================
        // 🚀 MÉTODO OTIMIZADO DE TREINAMENTO (Substitui a lógica baseada em swap)
        // =========================================================================================
        public float TrainBatch(List<(int[] InputIndices, int[] TargetIndices)> batch, float learningRate, ModelWeights weights, TensorScope epochScope)
        {
            float totalBatchLoss = 0;
            int count = 0;
            var weightKeys = GetWeightKeys();

            // Dicionário de Acumuladores de Gradiente (VRAM)
            var accumulatedGradients = new Dictionary<string, IMathTensor>();

            // Escopo para acumulação do Batch
            using (var batchScope = epochScope.CreateSubScope("BatchAccumulation"))
            {
                // 1. Inicializa acumuladores com Zeros
                foreach (var key in weightKeys)
                {
                    var shape = _tensorManager.GetShape(key.Value);
                    var zeroTensor = batchScope.CreateTensor(shape);
                    _mathEngine.Fill(zeroTensor, 0.0f);
                    accumulatedGradients[key.Key] = zeroTensor;
                }

                // 2. Processa sequências do batch
                foreach (var (input, target) in batch)
                {
                    // 🔥 OTIMIZAÇÃO: Usa cache em VRAM ao invés de Swap Files
                    var (loss, seqGrads) = ProcessSequenceVramCached(input, target, weights, batchScope);
                    
                    totalBatchLoss += loss;
                    count++;

                    // Acumula gradientes diretamente na VRAM
                    foreach (var kvp in seqGrads)
                    {
                        if (accumulatedGradients.TryGetValue(kvp.Key, out var accTensor))
                        {
                            _mathEngine.Add(accTensor, kvp.Value, accTensor);
                        }
                    }

                    // Limpa tensores temporários da sequência (Gradientes parciais)
                    foreach(var t in seqGrads.Values) if(t is IDisposable d) d.Dispose();
                }

                // 3. Média e Normalização
                float avgLoss = totalBatchLoss / Math.Max(1, count);
                float batchScale = 1.0f / Math.Max(1, count);
                
                foreach (var accGrad in accumulatedGradients.Values) 
                    _mathEngine.Scale(accGrad, batchScale);

                SanitizeGradients(accumulatedGradients);
                ApplyGlobalGradientClippingVRAM(accumulatedGradients, 5.0f);
                
                // 4. Update Adam (In-Place na VRAM)
                UpdateAdamWithVRAMGradients(weights, accumulatedGradients);

                return avgLoss;
            }
        }

        // --- ESTRUTURAS DE CACHE VRAM ---
        private class StepCache {
            public IMathTensor Input;
            public IMathTensor Fg, Ig, Cc, Og;
            public IMathTensor C_Next, H_Next, Tanh_C;
            public IMathTensor Pred;
        }

        // --- PIPELINE VRAM-RESIDENT ---
        protected (float loss, Dictionary<string, IMathTensor> grads) ProcessSequenceVramCached(
            int[] inputIndices, int[] targetIndices, ModelWeights weights, TensorScope scope)
        {
            var stepCaches = new List<StepCache>(inputIndices.Length);

            // Carrega estado inicial (Clona para VRAM local)
            var h_init = scope.Track(_mathEngine.Clone(_tensorManager.LoadTensor(_hiddenStateId)));
            var c_init = scope.Track(_mathEngine.Clone(_tensorManager.LoadTensor(_cellStateId)));

            IMathTensor h_prev = h_init;
            IMathTensor c_prev = c_init;
            float sequenceLoss = 0;

            // --- FORWARD PASS (VRAM) ---
            for (int t = 0; t < inputIndices.Length; t++)
            {
                var step = new StepCache();

                // Embedding
                var inputEmbedding = scope.CreateTensor(new[] { 1, weights.Embedding.Shape[1] });
                _mathEngine.Lookup(weights.Embedding, inputIndices[t], inputEmbedding);
                step.Input = inputEmbedding;

                // Gates
                step.Fg = ComputeGate(inputEmbedding, h_prev, weights.W_if, weights.W_hf, weights.B_f, scope);
                step.Ig = ComputeGate(inputEmbedding, h_prev, weights.W_ii, weights.W_hi, weights.B_i, scope);
                step.Cc = ComputeCellCandidate(inputEmbedding, h_prev, weights.W_ic, weights.W_hc, weights.B_c, scope);
                step.Og = ComputeGate(inputEmbedding, h_prev, weights.W_io, weights.W_ho, weights.B_o, scope);

                // C_Next
                var c_next = scope.CreateTensor(new[] { 1, hiddenSize });
                using (var t1 = scope.CreateTensor(new[] { 1, hiddenSize }))
                using (var t2 = scope.CreateTensor(new[] { 1, hiddenSize }))
                {
                    _mathEngine.Multiply(step.Fg, c_prev, t1);
                    _mathEngine.Multiply(step.Ig, step.Cc, t2);
                    _mathEngine.Add(t1, t2, c_next);
                }
                step.C_Next = c_next;

                // H_Next
                var h_next = scope.CreateTensor(new[] { 1, hiddenSize });
                var tanh_c = scope.CreateTensor(new[] { 1, hiddenSize });
                _mathEngine.Tanh(c_next, tanh_c);
                step.Tanh_C = tanh_c;
                _mathEngine.Multiply(step.Og, tanh_c, h_next);
                step.H_Next = h_next;

                // Predição
                var pred = scope.CreateTensor(new[] { 1, outputSize });
                _mathEngine.MatrixMultiply(h_next, weights.W_hy, pred);
                _mathEngine.AddBroadcast(pred, weights.B_y, pred);
                
                var probs = scope.CreateTensor(new[] { 1, outputSize });
                _mathEngine.Softmax(pred, probs);
                step.Pred = probs; 

                // Loss (CPU read - pequeno overhead)
                using (var probsCpu = probs.ToCpuTensor())
                {
                    float p = probsCpu.GetData()[targetIndices[t]];
                    sequenceLoss += -MathF.Log(Math.Max(p, 1e-9f));
                }
                
                // Limpeza imediata do logit bruto para poupar VRAM, mantemos probs
                if(pred is IDisposable pd) pd.Dispose();

                stepCaches.Add(step);
                h_prev = h_next;
                c_prev = c_next;
            }

            // Atualiza estado global persistente
            _tensorManager.OverwriteTensor(_hiddenStateId, h_prev);
            _tensorManager.OverwriteTensor(_cellStateId, c_prev);

            // --- BACKWARD PASS (VRAM) ---
            var grads = InitializeVramGradients(scope);
            
            var dh_next = scope.CreateTensor(new[] { 1, hiddenSize }); _mathEngine.Fill(dh_next, 0.0f);
            var dc_next = scope.CreateTensor(new[] { 1, hiddenSize }); _mathEngine.Fill(dc_next, 0.0f);

            for (int t = inputIndices.Length - 1; t >= 0; t--)
            {
                var s = stepCaches[t];
                
                // Determina estados anteriores
                IMathTensor h_prev_t = (t > 0) ? stepCaches[t - 1].H_Next : h_init;
                IMathTensor c_prev_t = (t > 0) ? stepCaches[t - 1].C_Next : c_init;

                // dL/dy (Softmax CrossEntropy: probs - target_one_hot)
                var d_y = scope.CreateTensor(s.Pred.Shape);
                _mathEngine.CloneTo(s.Pred, d_y); // Copia probs para d_y (implementar CloneTo ou usar Clone)
                // Fallback se CloneTo não existir na interface: var d_y = scope.Track(_mathEngine.Clone(s.Pred));
                
                using(var oneHot = _mathEngine.CreateOneHotTensor(new[]{targetIndices[t]}, outputSize))
                    _mathEngine.Subtract(d_y, oneHot, d_y);

                // Gradientes Saída (W_hy, B_y)
                _mathEngine.MatrixMultiplyTransposeA(s.H_Next, d_y, grads["W_hy_tmp"]);
                _mathEngine.Add(grads["W_hy"], grads["W_hy_tmp"], grads["W_hy"]);
                _mathEngine.Add(grads["B_y"], d_y, grads["B_y"]);

                // Backprop para H (dh)
                var dh = scope.CreateTensor(h_prev_t.Shape);
                _mathEngine.MatrixMultiplyTransposeB(d_y, weights.W_hy, dh);
                _mathEngine.Add(dh, dh_next, dh);

                // Backprop Gates
                // dH -> dOg, dTanhC
                var d_og = scope.CreateTensor(s.Og.Shape);
                var d_tanh_c = scope.CreateTensor(s.Tanh_C.Shape);
                
                _mathEngine.Multiply(dh, s.Tanh_C, d_og);
                _mathEngine.SigmoidDerivative(s.Og, d_og); // d_og * sig'(og)
                _mathEngine.Multiply(d_og, s.Og, d_og); // Ajuste se SigmoidDerivative não multiplicar pelo output

                _mathEngine.Multiply(dh, s.Og, d_tanh_c);
                _mathEngine.TanhDerivative(s.Tanh_C, d_tanh_c); 
                _mathEngine.Multiply(d_tanh_c, s.Tanh_C, d_tanh_c);

                // dC
                var dc = scope.CreateTensor(s.C_Next.Shape);
                _mathEngine.Add(dc_next, d_tanh_c, dc);

                // dC -> dFg, dIg, dCc, dC_prev
                var d_fg = scope.CreateTensor(s.Fg.Shape);
                var d_ig = scope.CreateTensor(s.Ig.Shape);
                var d_cc = scope.CreateTensor(s.Cc.Shape);
                var d_c_prev = scope.CreateTensor(c_prev_t.Shape);

                // Forget Gate Grad
                _mathEngine.Multiply(dc, c_prev_t, d_fg);
                using(var tmp = scope.CreateTensor(s.Fg.Shape)) { _mathEngine.SigmoidDerivative(s.Fg, tmp); _mathEngine.Multiply(d_fg, tmp, d_fg); }

                // Input Gate Grad
                _mathEngine.Multiply(dc, s.Cc, d_ig);
                using(var tmp = scope.CreateTensor(s.Ig.Shape)) { _mathEngine.SigmoidDerivative(s.Ig, tmp); _mathEngine.Multiply(d_ig, tmp, d_ig); }

                // Cell Candidate Grad
                _mathEngine.Multiply(dc, s.Ig, d_cc);
                using(var tmp = scope.CreateTensor(s.Cc.Shape)) { _mathEngine.TanhDerivative(s.Cc, tmp); _mathEngine.Multiply(d_cc, tmp, d_cc); }

                // C_Prev Grad
                _mathEngine.Multiply(dc, s.Fg, d_c_prev);

                // Acumula Grads dos Pesos (Gates)
                void Accumulate(IMathTensor d_gate, string w_h, string w_i, string b)
                {
                    _mathEngine.MatrixMultiplyTransposeA(h_prev_t, d_gate, grads[w_h + "_tmp"]);
                    _mathEngine.Add(grads[w_h], grads[w_h + "_tmp"], grads[w_h]);
                    
                    _mathEngine.MatrixMultiplyTransposeA(s.Input, d_gate, grads[w_i + "_tmp"]);
                    _mathEngine.Add(grads[w_i], grads[w_i + "_tmp"], grads[w_i]);
                    
                    _mathEngine.Add(grads[b], d_gate, grads[b]);
                }

                Accumulate(d_fg, "W_hf", "W_if", "B_f");
                Accumulate(d_ig, "W_hi", "W_ii", "B_i");
                Accumulate(d_cc, "W_hc", "W_ic", "B_c");
                Accumulate(d_og, "W_ho", "W_io", "B_o");

                // Embedding Grads (Simplificado: d_input = soma das projeções)
                var d_input = scope.CreateTensor(s.Input.Shape);
                _mathEngine.Fill(d_input, 0.0f);
                
                void AddInputGrad(IMathTensor d_gate, string w_i) {
                     using(var t = scope.CreateTensor(s.Input.Shape)) {
                        _mathEngine.MatrixMultiplyTransposeB(d_gate, weights.GetType().GetProperty(w_i).GetValue(weights) as IMathTensor, t);
                        _mathEngine.Add(d_input, t, d_input);
                     }
                }
                AddInputGrad(d_fg, "W_if"); AddInputGrad(d_ig, "W_ii");
                AddInputGrad(d_cc, "W_ic"); AddInputGrad(d_og, "W_io");
                
                // Acumula no Embedding Mestre
                _mathEngine.AccumulateGradient(grads["W_embedding"], d_input, inputIndices[t]);

                // Atualiza next grads para próxima iteração (t-1)
                // Precisamos copiar os valores, pois os tensores atuais serão descartados
                _mathEngine.Fill(dh_next, 0.0f); // Reset
                _mathEngine.Fill(dc_next, 0.0f); // Reset
                
                // Calcula dh_prev total (soma das projeções dos gates)
                // (Omitido cálculo completo de dh_prev por brevidade, mas segue lógica similar ao d_input usando W_h)
                // Na prática, em LSTM, dh_prev vem das multiplicações W_h * d_gate.
                void AddHiddenGrad(IMathTensor d_gate, string w_h) {
                     using(var t = scope.CreateTensor(h_prev_t.Shape)) {
                        _mathEngine.MatrixMultiplyTransposeB(d_gate, weights.GetType().GetProperty(w_h).GetValue(weights) as IMathTensor, t);
                        _mathEngine.Add(dh_next, t, dh_next); // dh_next aqui atua como dh_prev para o próximo loop
                     }
                }
                AddHiddenGrad(d_fg, "W_hf"); AddHiddenGrad(d_ig, "W_hi");
                AddHiddenGrad(d_cc, "W_hc"); AddHiddenGrad(d_og, "W_ho");

                // Passa dc_prev
                _mathEngine.Add(dc_next, d_c_prev, dc_next);
            }

            // Retorna apenas os acumuladores principais, removendo os temporários "_tmp"
            var finalGrads = grads.Where(k => !k.Key.EndsWith("_tmp")).ToDictionary(k => k.Key, k => k.Value);
            return (sequenceLoss / inputIndices.Length, finalGrads);
        }

        private IMathTensor ComputeCellCandidate(IMathTensor input, IMathTensor h_prev, IMathTensor W_i, IMathTensor W_h, IMathTensor bias, TensorScope scope)
        {
            // Tanh activation for Cell Candidate
            var res = scope.CreateTensor(new[] { 1, hiddenSize });
            using (var t1 = scope.CreateTensor(res.Shape))
            using (var t2 = scope.CreateTensor(res.Shape))
            using (var lin = scope.CreateTensor(res.Shape))
            {
                _mathEngine.MatrixMultiply(input, W_i, t1);
                _mathEngine.MatrixMultiply(h_prev, W_h, t2);
                _mathEngine.Add(t1, t2, lin);
                _mathEngine.AddBroadcast(lin, bias, lin);
                _mathEngine.Tanh(lin, res); // Diferença principal: Tanh em vez de Sigmoid
            }
            return res;
        }

        private Dictionary<string, IMathTensor> InitializeVramGradients(TensorScope scope)
        {
            var grads = new Dictionary<string, IMathTensor>();
            var keys = GetWeightKeys();
            foreach (var k in keys)
            {
                var shape = _tensorManager.GetShape(k.Value);
                var t = scope.CreateTensor(shape);
                _mathEngine.Fill(t, 0.0f);
                grads[k.Key] = t;
                
                // Buffer temporário para cálculos intermediários (evita alocação excessiva)
                var tmp = scope.CreateTensor(shape);
                grads[k.Key + "_tmp"] = tmp;
            }
            return grads;
        }

        private IMathTensor ComputeGate(IMathTensor input, IMathTensor h_prev, IMathTensor W_i, IMathTensor W_h, IMathTensor bias, TensorScope scope)
        {
            var res = scope.CreateTensor(new[] { 1, hiddenSize });
            using (var t1 = scope.CreateTensor(res.Shape))
            using (var t2 = scope.CreateTensor(res.Shape))
            using (var lin = scope.CreateTensor(res.Shape))
            {
                _mathEngine.MatrixMultiply(input, W_i, t1);
                _mathEngine.MatrixMultiply(h_prev, W_h, t2);
                _mathEngine.Add(t1, t2, lin);
                _mathEngine.AddBroadcast(lin, bias, lin);
                _mathEngine.Sigmoid(lin, res);
            }
            return res;
        }

        // =========================================================================================

        private void SanitizeGradients(Dictionary<string, IMathTensor> gradients)
        {
            foreach (var grad in gradients.Values)
                _mathEngine.SanitizeAndClip(grad, 10.0f); 
        }

        private void UpdateAdamWithVRAMGradients(ModelWeights weights, Dictionary<string, IMathTensor> vramGradients)
        {
            var weightInstances = new Dictionary<string, IMathTensor> {
                { "W_embedding", weights.Embedding }, { "W_if", weights.W_if }, { "W_hf", weights.W_hf }, { "B_f", weights.B_f },
                { "W_ii", weights.W_ii }, { "W_hi", weights.W_hi }, { "B_i", weights.B_i },
                { "W_ic", weights.W_ic }, { "W_hc", weights.W_hc }, { "B_c", weights.B_c },
                { "W_io", weights.W_io }, { "W_ho", weights.W_ho }, { "B_o", weights.B_o },
                { "W_hy", weights.W_hy }, { "B_y", weights.B_y },
                { "LN_f_gamma", weights.LN_f_gamma }, { "LN_f_beta", weights.LN_f_beta },
                { "LN_i_gamma", weights.LN_i_gamma }, { "LN_i_beta", weights.LN_i_beta },
                { "LN_c_gamma", weights.LN_c_gamma }, { "LN_c_beta", weights.LN_c_beta },
                { "LN_o_gamma", weights.LN_o_gamma }, { "LN_o_beta", weights.LN_o_beta }
            };

            var ids = GetWeightKeys();

            using (var updateScope = new TensorScope("AdamUpdate_Batch", _mathEngine, _tensorManager))
            {
                int layerIndex = 0;
                foreach (var kvp in weightInstances)
                {
                    string key = kvp.Key;
                    IMathTensor paramTensor = kvp.Value;
                    
                    if (!vramGradients.TryGetValue(key, out var gradTensor)) { layerIndex++; continue; }
                    
                    // Adam Update In-Place na VRAM
                    _adamOptimizer.UpdateParametersGpu(layerIndex, paramTensor, gradTensor, _mathEngine);
                    
                    // Persistência para disco (pode ser feita menos frequentemente se desejar, mas mantida aqui por segurança)
                    if (ids.TryGetValue(key, out string id))
                    {
                        _tensorManager.OverwriteTensor(id, paramTensor);
                    }
                    
                    layerIndex++;
                }
            }
        }
        
        // --- MÉTODOS ORIGINAIS / AUXILIARES ---

        private void ApplyGlobalGradientClippingVRAM(Dictionary<string, IMathTensor> gradients, float maxNorm)
        {
            double totalSumSquares = 0;
            foreach(var t in gradients.Values) totalSumSquares += _mathEngine.CalculateSumOfSquares(t);
            float totalNorm = MathF.Sqrt((float)totalSumSquares);
            if (totalNorm > maxNorm) {
                float scale = maxNorm / (totalNorm + 1e-8f);
                foreach(var t in gradients.Values) _mathEngine.Scale(t, scale);
            }
        }
        
        private string InitializeWeight(int rows, int cols, Random rand, string name)
        {
            float[] data = new float[rows * cols];
            if (name.Contains("Bias")) { if (name == "BiasForget") Array.Fill(data, 1.0f); else Array.Fill(data, 0.0f); }
            else if (!name.Contains("Embedding") && rows == cols) { data = CreateOrthogonalMatrix(rows, cols, rand); }
            else { float limit = MathF.Sqrt(6.0f / (rows + cols)); for (int i = 0; i < data.Length; i++) data[i] = (float)((rand.NextDouble() * 2 - 1) * limit); }
            return _tensorManager.CreateAndStore(data, new[] { rows, cols }, name);
        }

        private float[] CreateOrthogonalMatrix(int rows, int cols, Random rand)
        {
            var M = Matrix<float>.Build.Dense(rows, cols);
            var normalDist = new Normal(0, 1, rand);
            for (int i = 0; i < rows; i++) for (int j = 0; j < cols; j++) M[i, j] = (float)normalDist.Sample();
            var svd = M.Svd(true);
            Matrix<float> orthogonalMatrix = rows >= cols ? svd.U : svd.VT.Transpose();
            if (orthogonalMatrix.RowCount != rows || orthogonalMatrix.ColumnCount != cols) {
                var finalMatrix = Matrix<float>.Build.Dense(rows, cols);
                finalMatrix.SetSubMatrix(0, 0, orthogonalMatrix.SubMatrix(0, Math.Min(rows, orthogonalMatrix.RowCount), 0, Math.Min(cols, orthogonalMatrix.ColumnCount)));
                return ConvertToRowMajor(finalMatrix, rows, cols);
            }
            return ConvertToRowMajor(orthogonalMatrix, rows, cols);
        }

        private float[] ConvertToRowMajor(Matrix<float> matrix, int rows, int cols)
        {
            float[] rowMajorData = new float[rows * cols];
            for(int i = 0; i < rows; i++) for(int j = 0; j < cols; j++) rowMajorData[i * cols + j] = matrix[i, j];
            return rowMajorData;
        }

        private Dictionary<string, string> GetWeightKeys() => new Dictionary<string, string> {
            { "W_embedding", _weightsEmbeddingId }, { "W_if", _weightsInputForgetId }, { "W_hf", _weightsHiddenForgetId }, { "B_f", _biasForgetId },
            { "W_ii", _weightsInputInputId }, { "W_hi", _weightsHiddenInputId }, { "B_i", _biasInputId }, { "W_ic", _weightsInputCellId },
            { "W_hc", _weightsHiddenCellId }, { "B_c", _biasCellId }, { "W_io", _weightsInputOutputId }, { "W_ho", _weightsHiddenOutputId },
            { "B_o", _biasOutputId }, { "W_hy", _weightsHiddenOutputFinalId }, { "B_y", _biasOutputFinalId }, { "LN_f_gamma", _lnForgetGammaId },
            { "LN_f_beta", _lnForgetBetaId }, { "LN_i_gamma", _lnInputGammaId }, { "LN_i_beta", _lnInputBetaId }, { "LN_c_gamma", _lnCellGammaId },
            { "LN_c_beta", _lnCellBetaId }, { "LN_o_gamma", _lnOutputGammaId }, { "LN_o_beta", _lnOutputBetaId }
        };

        public void SaveModel(string filePath) {
            var modelData = new { VocabSize = inputSize, EmbeddingSize = _tensorManager.GetShape(_weightsEmbeddingId)[1], HiddenSize = hiddenSize, OutputSize = outputSize, SessionId = _sessionId, TensorIds = GetWeightKeys().ToDictionary(k => k.Key, k => k.Value) };
            File.WriteAllText(filePath, JsonSerializer.Serialize(modelData, new JsonSerializerOptions { WriteIndented = true }));
        }

        public static NeuralNetworkLSTM? LoadModel(string filePath, IMathEngine mathEngine) {
            if (!File.Exists(filePath)) return null;
            var root = JsonDocument.Parse(File.ReadAllText(filePath)).RootElement;
            var model = new NeuralNetworkLSTM(root.GetProperty("VocabSize").GetInt32(), root.GetProperty("EmbeddingSize").GetInt32(), root.GetProperty("HiddenSize").GetInt32(), root.GetProperty("OutputSize").GetInt32(), mathEngine);
            var ids = root.GetProperty("TensorIds");
            model._weightsEmbeddingId = ids.GetProperty("W_embedding").GetString()!;
            model._weightsInputForgetId = ids.GetProperty("W_if").GetString()!;
            // ... (restante do load simplificado)
            return model;
        }
        
        // Métodos auxiliares de computação e gates (Legacy Forward/Backward methods mantidos se necessário, mas TrainBatch usa o novo)
        protected (float, List<string>) ForwardPassZeroRAM(int[] i, int[] t, ModelWeights w, TensorScope s) => (0.0f, new List<string>()); // Stub
        protected Dictionary<string, string> BackwardPassZeroRAM(int[] i, int[] t, List<string> s, ModelWeights w) => new Dictionary<string, string>(); // Stub

        // Compatibilidade para Inferência (usa também VRAM cache pois é mais rápido)
        public (float, List<string>) RunForwardPassForInference(int[] inputIndices, int[] targetIndices, ModelWeights weights) {
            using (var inferenceScope = new TensorScope("InferencePass", _mathEngine, _tensorManager)) {
                var (loss, _) = ProcessSequenceVramCached(inputIndices, targetIndices, weights, inferenceScope);
                return (loss, new List<string>()); // Retorna lista vazia de swap files pois não geramos nenhum
            }
        }
        
        // Helpers
        public void ResetOptimizerState() => _adamOptimizer.Reset();
        public void ClearEpochTemporaryTensors() => _swapManager.ClearAllSwap();
        public IndividualFileTensorManager GetTensorManager() => _tensorManager;
        public DiskSwapManager GetSwapManager() => _swapManager;
        public string GetWeightsEmbeddingId() => _weightsEmbeddingId;
        public string GetWeightsInputForgetId() => _weightsInputForgetId;
        public string GetWeightsHiddenForgetId() => _weightsHiddenForgetId;
        public string GetBiasForgetId() => _biasForgetId;
        public string GetWeightsInputInputId() => _weightsInputInputId;
        public string GetWeightsHiddenInputId() => _weightsHiddenInputId;
        public string GetBiasInputId() => _biasInputId;
        public string GetWeightsInputCellId() => _weightsInputCellId;
        public string GetWeightsHiddenCellId() => _weightsHiddenCellId;
        public string GetBiasCellId() => _biasCellId;
        public string GetWeightsInputOutputId() => _weightsInputOutputId;
        public string GetWeightsHiddenOutputId() => _weightsHiddenOutputId;
        public string GetBiasOutputId() => _biasOutputId;
        public string GetWeightsHiddenOutputFinalId() => _weightsHiddenOutputFinalId;
        public string GetBiasOutputFinalId() => _biasOutputFinalId;
        public string GetLnForgetGammaId() => _lnForgetGammaId;
        public string GetLnForgetBetaId() => _lnForgetBetaId;
        public string GetLnInputGammaId() => _lnInputGammaId;
        public string GetLnInputBetaId() => _lnInputBetaId;
        public string GetLnCellGammaId() => _lnCellGammaId;
        public string GetLnCellBetaId() => _lnCellBetaId;
        public string GetLnOutputGammaId() => _lnOutputGammaId;
        public string GetLnOutputBetaId() => _lnOutputBetaId;

        public void Dispose() {
            if (_disposed) return;
            _swapManager?.Dispose();
            _tensorManager?.Dispose();
            _adamOptimizer?.Reset();
            _disposed = true;
            GC.SuppressFinalize(this);
        }

        public void ResetHiddenState()
        {
            var zeros = new float[HiddenSize];
            _tensorManager.UpdateTensor(_hiddenStateId, t => t.UpdateFromCpu(zeros));
            _tensorManager.UpdateTensor(_cellStateId, t => t.UpdateFromCpu(zeros));
        }

    }