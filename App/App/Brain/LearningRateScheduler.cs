namespace Brain;

public class LearningRateScheduler
    {
        private readonly float _initialLR;
        private readonly float _minLR;
        private readonly int _warmupSteps;
        private readonly int _totalSteps;
        private int _currentStep = 0;

        public LearningRateScheduler(
            float initialLR = 0.01f, 
            float minLR = 0.0001f,
            int warmupSteps = 2000,
            int totalSteps = 100000)
        {
            _initialLR = initialLR;
            _minLR = minLR;
            _warmupSteps = warmupSteps;
            _totalSteps = totalSteps;
        }

        /// <summary>
        /// Retorna o Learning Rate atual baseado no step.
        /// </summary>
        public float GetLearningRate()
        {
            return GetLearningRate(_currentStep);
        }

        /// <summary>
        /// Calcula LR para um step específico.
        /// </summary>
        public float GetLearningRate(int step)
        {
            // Fase 1: Linear Warmup
            if (step < _warmupSteps)
            {
                float warmupProgress = step / (float)_warmupSteps;
                return _minLR + (_initialLR - _minLR) * warmupProgress;
            }

            // Fase 2: Cosine Annealing
            int decaySteps = step - _warmupSteps;
            int maxDecaySteps = _totalSteps - _warmupSteps;
            
            if (decaySteps >= maxDecaySteps)
            {
                return _minLR;
            }

            float cosineProgress = decaySteps / (float)maxDecaySteps;
            float cosineDecay = 0.5f * (1.0f + MathF.Cos(MathF.PI * cosineProgress));
            
            return _minLR + (_initialLR - _minLR) * cosineDecay;
        }

        /// <summary>
        /// Incrementa o step interno.
        /// </summary>
        public void Step()
        {
            _currentStep++;
        }

        /// <summary>
        /// Reseta o scheduler para uma nova época.
        /// </summary>
        public void Reset()
        {
            _currentStep = 0;
        }

        /// <summary>
        /// Retorna informações de debug.
        /// </summary>
        public string GetStatus()
        {
            float currentLR = GetLearningRate();
            string phase = _currentStep < _warmupSteps ? "WARMUP" : "DECAY";
            return $"Step: {_currentStep} | LR: {currentLR:F6} | Phase: {phase}";
        }
    }