using Interfaces;

namespace Brain;

public class AdamOptimizer
    {
        private readonly IndividualFileTensorManager _tensorManager;
        // Cache em memória para os estados do otimizador (M e V)
        private readonly Dictionary<string, IMathTensor> _stateCache;
        private readonly Dictionary<int, int> _t; // Timesteps por camada

        private readonly float _learningRate;
        private readonly float _beta1;
        private readonly float _beta2;
        private readonly float _epsilon;

        public AdamOptimizer(IndividualFileTensorManager tensorManager, float learningRate = 0.001f)
        {
            _tensorManager = tensorManager;
            _stateCache = new Dictionary<string, IMathTensor>();
            _t = new Dictionary<int, int>();
            _learningRate = learningRate;
            _beta1 = 0.9f;
            _beta2 = 0.999f;
            _epsilon = 1e-8f;
        }

        public void UpdateParametersGpu(int layerId, IMathTensor parameters, IMathTensor gradients, IMathEngine mathEngine)
        {
            string mKey = $"m_{layerId}";
            string vKey = $"v_{layerId}";

            // Inicializa estados na primeira vez (Lazy Init)
            if (!_stateCache.ContainsKey(mKey))
            {
                // Tenta carregar do disco (se for retomada de treino) ou cria zeros
                try 
                {
                    // Tenta carregar estado persistido
                    var mDisk = _tensorManager.LoadTensor($"opt_{mKey}");
                    _stateCache[mKey] = mathEngine.Clone(mDisk); // Clone para memória local
                    mDisk.Dispose(); // Libera o do manager
                }
                catch 
                {
                    // Se não existir, cria novo
                    _stateCache[mKey] = mathEngine.CreateTensor(parameters.Shape);
                    mathEngine.Fill(_stateCache[mKey], 0.0f);
                }

                try
                {
                    var vDisk = _tensorManager.LoadTensor($"opt_{vKey}");
                    _stateCache[vKey] = mathEngine.Clone(vDisk);
                    vDisk.Dispose();
                }
                catch
                {
                    _stateCache[vKey] = mathEngine.CreateTensor(parameters.Shape);
                    mathEngine.Fill(_stateCache[vKey], 0.0f);
                }

                if (!_t.ContainsKey(layerId)) _t[layerId] = 0;
            }

            IMathTensor m = _stateCache[mKey];
            IMathTensor v = _stateCache[vKey];
            
            _t[layerId]++;
            int t = _t[layerId];

            // Atualização puramente na GPU/RAM
            mathEngine.AdamUpdate(parameters, gradients, m, v, _learningRate, _beta1, _beta2, _epsilon, t);
        }

        // Método explícito para salvar estado (chamar ao final da época ou checkpoint)
        public void SaveStateToDisk()
        {
            Console.WriteLine("[Adam] Salvando estados do otimizador...");
            foreach (var kvp in _stateCache)
            {
                _tensorManager.OverwriteTensor($"opt_{kvp.Key}", kvp.Value);
            }
        }

        public void Reset()
        {
            foreach (var t in _stateCache.Values) t.Dispose();
            _stateCache.Clear();
            _t.Clear();
        }
    }