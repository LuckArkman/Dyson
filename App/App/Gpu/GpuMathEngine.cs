using Brain;
using Interfaces;
using OpenCL.NetCore;
using static OpenCL.NetCore.Cl;
using Exception = System.Exception;
using Platform = OpenCL.NetCore.Platform;
using Device = OpenCL.NetCore.Device;
using DeviceType = OpenCL.NetCore.DeviceType;

namespace Gpu;

public class GpuMathEngine : IMathEngine, IDisposable
    {
        public bool IsGpu => true;
        private readonly Context _context;
        private readonly CommandQueue _commandQueue;
        private readonly OpenCL.NetCore.Program _program;
        private readonly GpuSyncGuard _syncGuard;
        private readonly EventPool _eventPool;

        private int _operationsSinceLastSync = 0;
        private const int SYNC_INTERVAL = 5000;

        // Kernels
        private readonly Kernel _matrixMultiplyKernel;
        private readonly Kernel _addKernel;
        private readonly Kernel _addBroadcastKernel;
        private readonly Kernel _multiplyKernel;
        private readonly Kernel _sigmoidKernel;
        private readonly Kernel _tanhKernel;
        private readonly Kernel _cloneKernel;
        private readonly Kernel _transposeKernel;
        private readonly Kernel _subtractKernel;
        private readonly Kernel _sigmoidDerivativeKernel;
        private readonly Kernel _tanhDerivativeKernel;
        private readonly Kernel _matrixMultiplyTransposeAKernel;
        private readonly Kernel _matrixMultiplyTransposeBKernel;
        private readonly Kernel _addScaledKernel;
        private readonly Kernel _subtractScaledKernel;
        private readonly Kernel _sliceKernel;
        private readonly Kernel _setKernel;
        private readonly Kernel _clipKernel;
        private readonly Kernel _scaleKernel;
        private readonly Kernel _softmaxKernel;
        private readonly Kernel _lookupKernel;
        private readonly Kernel _accumulateGradientKernel;
        private readonly Kernel _oneHotEncodeKernel;
        private readonly Kernel _adamUpdateKernel;
        private readonly Kernel _layerNormKernel;
        private readonly Kernel _sanitizeAndClipKernel;
        private readonly Kernel _sumOfSquaresKernel;
        private readonly Kernel _fillKernel; 

        // Novos Kernels de Batch
        private readonly Kernel _lookupBatchKernel;
        private readonly Kernel _accumulateGradientBatchKernel;
        private readonly Kernel _oneHotEncodeBatchKernel;

        private bool _disposed = false;

        #region Kernels Source

        // ATENÇÃO: A correção do kernel 'fill' está aplicada abaixo
        private const string ProgramSource = @"
    __kernel void matrix_multiply(__global const float* A, __global const float* B, __global float* C, int M, int N, int P) { int i = get_global_id(0); int j = get_global_id(1); if (i < M && j < P) { float sum = 0.0f; for (int k = 0; k < N; ++k) { sum += A[i * N + k] * B[k * P + j]; } C[i * P + j] = sum; } }
    __kernel void add(__global const float* a, __global const float* b, __global float* result) { int gid = get_global_id(0); result[gid] = a[gid] + b[gid]; }
    __kernel void add_broadcast(__global float* a, __global const float* bias, int bias_size) { int gid = get_global_id(0); int col = gid % bias_size; if (col < bias_size && gid < get_global_size(0)) { a[gid] = a[gid] + bias[col]; } }
    __kernel void multiply(__global const float* a, __global const float* b, __global float* result) { int gid = get_global_id(0); result[gid] = a[gid] * b[gid]; }
    __kernel void clone_buffer(__global const float* input, __global float* output) { int gid = get_global_id(0); output[gid] = input[gid]; }
    __kernel void transpose(__global const float* input, __global float* output, int rows, int cols) { int i = get_global_id(0); int j = get_global_id(1); if (i < rows && j < cols) { output[j * rows + i] = input[i * cols + j]; } }
    __kernel void subtract(__global const float* a, __global const float* b, __global float* result) { int gid = get_global_id(0); result[gid] = a[gid] - b[gid]; }
    __kernel void matrix_multiply_transpose_a(__global const float* A, __global const float* B, __global float* C, int M, int K, int P) { int i = get_global_id(0); int j = get_global_id(1); if (i < M && j < P) { float sum = 0.0f; for (int k = 0; k < K; ++k) { sum += A[k * M + i] * B[k * P + j]; } C[i * P + j] = sum; } }
    __kernel void matrix_multiply_transpose_b(__global const float* A, __global const float* B, __global float* C, int M, int K, int P) { int i = get_global_id(0); int j = get_global_id(1); if (i < M && j < P) { float sum = 0.0f; for (int k = 0; k < K; ++k) { sum += A[i * K + k] * B[j * K + k]; } C[i * P + j] = sum; } }
    __kernel void add_scaled(__global float* target, __global const float* source, float scalar) { int gid = get_global_id(0); target[gid] += source[gid] * scalar; }
    __kernel void subtract_scaled(__global float* target, __global const float* source, float scalar) { int gid = get_global_id(0); target[gid] -= source[gid] * scalar; }
    __kernel void slice(__global const float* source, __global float* dest, int offset, int size) { int gid = get_global_id(0); if (gid < size) { dest[gid] = source[offset + gid]; } }
    __kernel void set(__global float* dest, __global const float* source, int offset, int size) { int gid = get_global_id(0); if (gid < size) { dest[offset + gid] = source[gid]; } }
    __kernel void clip(__global float* data, float min_val, float max_val) { int gid = get_global_id(0); data[gid] = fmax(min_val, fmin(max_val, data[gid])); }
    __kernel void scale(__global float* data, float scalar) { int gid = get_global_id(0); data[gid] *= scalar; }
    
    // CORREÇÃO APLICADA AQUI: Adicionado 'int size' e verificação 'if(gid < size)'
    __kernel void fill(__global float* data, float value, int size) { int gid = get_global_id(0); if(gid < size) data[gid] = value; }

    __kernel void lookup(__global const float* embedding_matrix, __global float* result, int index, int embedding_size) { int gid = get_global_id(0); if (gid < embedding_size) { result[gid] = embedding_matrix[index * embedding_size + gid]; } }
    __kernel void accumulate_gradient_no_atomic(__global float* embedding_gradients, __global const float* gradient, int index, int embedding_size) { int gid = get_global_id(0); if (gid < embedding_size) { embedding_gradients[index * embedding_size + gid] += gradient[gid]; } }
    __kernel void one_hot_encode(__global float* output, __global const int* indices, int total_classes) { int i = get_global_id(0); int row_offset = i * total_classes; for(int j = 0; j < total_classes; ++j) { output[row_offset + j] = 0.0f; } int one_hot_index = indices[i]; output[row_offset + one_hot_index] = 1.0f; }
    __kernel void tanh_activation(__global const float* a, __global float* result) { int gid = get_global_id(0); float input = a[gid]; const float MAX_TANH_INPUT = 20.0f; input = clamp(input, -MAX_TANH_INPUT, MAX_TANH_INPUT); if (isnan(input) || isinf(input)) { result[gid] = 0.0f; return; } float exp2x = exp(2.0f * input); if (isinf(exp2x) || isnan(exp2x)) { result[gid] = (input > 0.0f) ? 1.0f : -1.0f; return; } float tanh_result = (exp2x - 1.0f) / (exp2x + 1.0f); if (isnan(tanh_result) || isinf(tanh_result)) { result[gid] = (input > 0.0f) ? 1.0f : -1.0f; } else { result[gid] = clamp(tanh_result, -1.0f, 1.0f); } }
    __kernel void sigmoid(__global const float* a, __global float* result) { int gid = get_global_id(0); float input = a[gid]; const float MAX_SIGMOID_INPUT = 88.0f; input = clamp(input, -MAX_SIGMOID_INPUT, MAX_SIGMOID_INPUT); if (isnan(input) || isinf(input)) { result[gid] = 0.5f; return; } float sigmoid_result; if (input >= 0.0f) { float exp_neg = exp(-input); if (isinf(exp_neg) || isnan(exp_neg)) { sigmoid_result = 1.0f; } else { sigmoid_result = 1.0f / (1.0f + exp_neg); } } else { float exp_pos = exp(input); if (isinf(exp_pos) || isnan(exp_pos)) { sigmoid_result = 0.0f; } else { sigmoid_result = exp_pos / (1.0f + exp_pos); } } if (isnan(sigmoid_result) || isinf(sigmoid_result)) { result[gid] = 0.5f; } else { result[gid] = clamp(sigmoid_result, 0.0f, 1.0f); } }
    __kernel void tanh_derivative(__global const float* output, __global float* result) { int gid = get_global_id(0); float o = output[gid]; if (isnan(o) || isinf(o)) { result[gid] = 0.0f; return; } float deriv = 1.0f - o * o; if (isnan(deriv) || isinf(deriv)) { result[gid] = 0.0f; } else { result[gid] = clamp(deriv, 0.0f, 1.0f); } }
    __kernel void sigmoid_derivative(__global const float* output, __global float* result) { int gid = get_global_id(0); float o = output[gid]; if (isnan(o) || isinf(o)) { result[gid] = 0.0f; return; } float deriv = o * (1.0f - o); if (isnan(deriv) || isinf(deriv)) { result[gid] = 0.0f; } else { result[gid] = clamp(deriv, 0.0f, 0.25f); } }
    __kernel void softmax(__global const float* input, __global float* output, int size) { int row = get_global_id(0); int offset = row * size; float maxVal = input[offset]; for (int i = 1; i < size; i++) { float val = input[offset + i]; if (!isnan(val) && !isinf(val) && val > maxVal) { maxVal = val; } } maxVal = clamp(maxVal, -88.0f, 88.0f); float sumExp = 0.0f; for (int i = 0; i < size; i++) { float val = input[offset + i]; if (isnan(val) || isinf(val)) { output[offset + i] = 0.0f; continue; } float shifted = clamp(val - maxVal, -88.0f, 0.0f); float exp_val = exp(shifted); if (isnan(exp_val) || isinf(exp_val)) { exp_val = 0.0f; } output[offset + i] = exp_val; sumExp += exp_val; } if (sumExp < 1e-10f || isnan(sumExp) || isinf(sumExp)) { float uniform = 1.0f / (float)size; for (int i = 0; i < size; i++) { output[offset + i] = uniform; } } else { for (int i = 0; i < size; i++) { float normalized = output[offset + i] / sumExp; if (isnan(normalized) || isinf(normalized)) { output[offset + i] = 1e-10f; } else { output[offset + i] = clamp(normalized, 1e-10f, 1.0f); } } } }
    
    __kernel void adam_update(__global float* p, __global const float* g, __global float* m, __global float* v, float lr, float beta1, float beta2, float epsilon, int t) {
        int i = get_global_id(0);
        float grad = g[i];

        if (isnan(grad) || isinf(grad)) return;
        const float MAX_GRAD = 10.0f;
        grad = clamp(grad, -MAX_GRAD, MAX_GRAD);

        float m_old = m[i];
        float v_old = v[i];
        
        if (isnan(m_old) || isinf(m_old)) m_old = 0.0f;
        if (isnan(v_old) || isinf(v_old)) v_old = 0.0f;

        float m_val = beta1 * m_old + (1.0f - beta1) * grad;
        float v_val = beta2 * v_old + (1.0f - beta2) * (grad * grad);

        if (isnan(m_val) || isinf(m_val)) m_val = 0.0f;
        if (isnan(v_val) || isinf(v_val)) v_val = 0.0f;

        float beta1_pow_t = pow(beta1, (float)t);
        float beta2_pow_t = pow(beta2, (float)t);
        
        float m_hat = m_val / (1.0f - beta1_pow_t);
        float v_hat = v_val / (1.0f - beta2_pow_t);

        if (isnan(m_hat) || isinf(m_hat)) m_hat = m_val;
        if (isnan(v_hat) || isinf(v_hat)) v_hat = v_val;

        float sqrt_v_hat = sqrt(fabs(v_hat)); 
        float denominator = sqrt_v_hat + epsilon;
        
        if (denominator < 1e-8f) return;

        float update = lr * m_hat / denominator;
        const float MAX_UPDATE = 0.1f;
        update = clamp(update, -MAX_UPDATE, MAX_UPDATE);

        if (isnan(update) || isinf(update)) return;

        p[i] -= update;
        m[i] = m_val;
        v[i] = v_val;
    }

    __kernel void layer_norm(__global float* input, __global const float* gamma, __global const float* beta, int size, float epsilon) { int row = get_global_id(0); int offset = row * size; float mean = 0.0f; for (int i = 0; i < size; ++i) { mean += input[offset + i]; } mean /= size; float variance = 0.0f; for (int i = 0; i < size; ++i) { float diff = input[offset + i] - mean; variance += diff * diff; } variance /= size; float inv_std = rsqrt(variance + epsilon); for (int i = 0; i < size; ++i) { input[offset + i] = ((input[offset + i] - mean) * inv_std) * gamma[i] + beta[i]; } }

    __kernel void sanitize_and_clip(__global float* data, float clip_val) {
        int gid = get_global_id(0);
        float current_val = data[gid];
        if (isnan(current_val) || isinf(current_val)) {
            data[gid] = 0.0f;
        } else {
            data[gid] = clamp(current_val, -clip_val, clip_val);
        }
    }

    __kernel void sum_of_squares(__global const float* input, __local float* scratch, __global float* result, uint length) {
        int gid = get_global_id(0); int lid = get_local_id(0); int lsize = get_local_size(0);
        float acc = 0.0f;
        for (int i = gid; i < length; i += get_global_size(0)) acc += input[i] * input[i];
        scratch[lid] = acc;
        barrier(CLK_LOCAL_MEM_FENCE);
        for (int offset = lsize / 2; offset > 0; offset /= 2) {
            if (lid < offset) scratch[lid] += scratch[lid + offset];
            barrier(CLK_LOCAL_MEM_FENCE);
        }
        if (lid == 0) result[get_group_id(0)] = scratch[0];
    }
    
    __kernel void lookup_batch(__global const float* table, __global float* result, __global const int* indices, int emb_size) {
        int batch_idx = get_global_id(0); 
        int feat_idx = get_global_id(1);  
        int token_id = indices[batch_idx];
        result[batch_idx * emb_size + feat_idx] = table[token_id * emb_size + feat_idx];
    }

    __kernel void accumulate_grad_batch(__global float* emb_grads, __global const float* batch_grads, __global const int* indices, int emb_size) {
        int batch_idx = get_global_id(0);
        int feat_idx = get_global_id(1);
        int token_id = indices[batch_idx];
        float val = batch_grads[batch_idx * emb_size + feat_idx];
        __global float* addr = &emb_grads[token_id * emb_size + feat_idx];
        float expected, next;
        union { float f; int i; } u_old, u_new;
        do {
            u_old.f = *addr;
            expected = u_old.f;
            next = expected + val;
            u_new.f = next;
        } while (atomic_cmpxchg((volatile __global int*)addr, u_old.i, u_new.i) != u_old.i);
    }

    __kernel void one_hot_batch(__global float* result, __global const int* indices, int vocab_size) {
        int batch_idx = get_global_id(0);
        int target_idx = indices[batch_idx];
        result[batch_idx * vocab_size + target_idx] = 1.0f;
    }
    ";

        #endregion

        // Helper Unwrap
        private GpuTensor Unwrap(IMathTensor tensor)
        {
            if (tensor is PooledTensor pooled)
                return (GpuTensor)pooled.InnerTensor;
            return (GpuTensor)tensor;
        }

        public GpuMathEngine()
        {
            ErrorCode error;
            Platform[] platforms = GetPlatformIDs(out error);
            CheckError(error);
            var platform = platforms.First();
            Device[] devices = GetDeviceIDs(platform, DeviceType.Gpu, out error);
            if (error != ErrorCode.Success || devices.Length == 0)
                devices = GetDeviceIDs(platform, DeviceType.Cpu, out error);

            _context = CreateContext(null, 1, new[] { devices[0] }, null, IntPtr.Zero, out error);
            _commandQueue = CreateCommandQueue(_context, devices[0], CommandQueueProperties.None, out error);
            _program = CreateProgramWithSource(_context, 1, new[] { ProgramSource }, null, out error);

            BuildProgram(_program, 1, new[] { devices[0] }, "-cl-fast-relaxed-math", null, IntPtr.Zero);
            if (error != ErrorCode.Success)
                throw new OpenClException(
                    "Build Error: " + GetProgramBuildInfo(_program, devices[0], ProgramBuildInfo.Log, out _), error);

            // Inicialização dos Kernels
            _matrixMultiplyKernel = CreateKernel(_program, "matrix_multiply", out _);
            _addKernel = CreateKernel(_program, "add", out _);
            _addBroadcastKernel = CreateKernel(_program, "add_broadcast", out _);
            _multiplyKernel = CreateKernel(_program, "multiply", out _);
            _sigmoidKernel = CreateKernel(_program, "sigmoid", out _);
            _tanhKernel = CreateKernel(_program, "tanh_activation", out _);
            _cloneKernel = CreateKernel(_program, "clone_buffer", out _);
            _transposeKernel = CreateKernel(_program, "transpose", out _);
            _subtractKernel = CreateKernel(_program, "subtract", out _);
            _sigmoidDerivativeKernel = CreateKernel(_program, "sigmoid_derivative", out _);
            _tanhDerivativeKernel = CreateKernel(_program, "tanh_derivative", out _);
            _matrixMultiplyTransposeAKernel = CreateKernel(_program, "matrix_multiply_transpose_a", out _);
            _matrixMultiplyTransposeBKernel = CreateKernel(_program, "matrix_multiply_transpose_b", out _);
            _addScaledKernel = CreateKernel(_program, "add_scaled", out _);
            _subtractScaledKernel = CreateKernel(_program, "subtract_scaled", out _);
            _sliceKernel = CreateKernel(_program, "slice", out _);
            _setKernel = CreateKernel(_program, "set", out _);
            _clipKernel = CreateKernel(_program, "clip", out _);
            _scaleKernel = CreateKernel(_program, "scale", out _);
            _softmaxKernel = CreateKernel(_program, "softmax", out _);
            _lookupKernel = CreateKernel(_program, "lookup", out _);
            _accumulateGradientKernel = CreateKernel(_program, "accumulate_gradient_no_atomic", out _);
            _oneHotEncodeKernel = CreateKernel(_program, "one_hot_encode", out _);
            _adamUpdateKernel = CreateKernel(_program, "adam_update", out _);
            _layerNormKernel = CreateKernel(_program, "layer_norm", out _);
            _sanitizeAndClipKernel = CreateKernel(_program, "sanitize_and_clip", out _);
            _sumOfSquaresKernel = CreateKernel(_program, "sum_of_squares", out _);
            _fillKernel = CreateKernel(_program, "fill", out _);

            // Novos Kernels de Batch
            _lookupBatchKernel = CreateKernel(_program, "lookup_batch", out _);
            _accumulateGradientBatchKernel = CreateKernel(_program, "accumulate_grad_batch", out _);
            _oneHotEncodeBatchKernel = CreateKernel(_program, "one_hot_batch", out _);

            _syncGuard = new GpuSyncGuard(_commandQueue);
            _eventPool = new EventPool();
        }

        public void Synchronize()
        {
            _syncGuard.SynchronizeBeforeRead("ManualSync");
            _operationsSinceLastSync = 0;
        }

        public void FlushQueue() => Flush(_commandQueue);

        public IMathTensor CreateTensor(int[] shape) => new GpuTensor(shape, _context, _commandQueue, _syncGuard);

        public IMathTensor CreateTensor(float[] data, int[] shape)
        {
            long expectedLength = shape.Aggregate(1L, (a, b) => a * b);
            if (data.Length != expectedLength) throw new ArgumentException($"Tamanho incompatível.");
            return new GpuTensor(data, shape, _context, _commandQueue, _syncGuard);
        }

        // ✅ CORREÇÃO APLICADA AQUI: Passar o tamanho do buffer também como argumento do Kernel
        public void Fill(IMathTensor tensor, float value)
        {
            long len = Unwrap(tensor).Length;
            ExecuteKernel1D(_fillKernel, len, Unwrap(tensor).Buffer, value, (int)len);
        }
        
        public void CloneTo(IMathTensor source, IMathTensor destination)
        {
            if (source.Length != destination.Length)
                throw new ArgumentException($"Tamanhos incompatíveis no CloneTo: {source.Length} vs {destination.Length}");

            // Usa o kernel 'clone_buffer' já compilado no construtor
            ExecuteKernel1D(_cloneKernel, source.Length, Unwrap(source).Buffer, Unwrap(destination).Buffer);
        }

        public void LookupBatch(IMathTensor embeddingMatrix, int[] indices, IMathTensor result)
        {
            var tEmb = Unwrap(embeddingMatrix);
            var tRes = Unwrap(result);
            int batchSize = indices.Length;
            int embSize = embeddingMatrix.Shape[1];
            Mem memIndices = (Mem)CreateBuffer(_context, MemFlags.ReadOnly | MemFlags.CopyHostPtr,
                (IntPtr)(batchSize * sizeof(int)), indices, out _);
            try
            {
                SetKernelArg(_lookupBatchKernel, 0, tEmb.Buffer);
                SetKernelArg(_lookupBatchKernel, 1, tRes.Buffer);
                SetKernelArg(_lookupBatchKernel, 2, memIndices);
                SetKernelArg(_lookupBatchKernel, 3, embSize);
                ExecuteKernel2D(_lookupBatchKernel, batchSize, embSize);
            }
            finally
            {
                ReleaseMemObject(memIndices);
            }
        }

        // ... O RESTO DOS MÉTODOS PERMANECE INALTERADO ...
        // Vou omitir os outros métodos padrão para economizar espaço, 
        // mas eles devem ser mantidos como estavam no código anterior.
        // Apenas lembre-se que 'Fill' foi o foco da correção.
        
        public void AccumulateGradientBatch(IMathTensor embeddingGradients, IMathTensor batchGradients, int[] indices)
        {
            var tEmbGrad = Unwrap(embeddingGradients);
            var tBatchGrad = Unwrap(batchGradients);
            int batchSize = indices.Length;
            int embSize = embeddingGradients.Shape[1];
            Mem memIndices = (Mem)CreateBuffer(_context, MemFlags.ReadOnly | MemFlags.CopyHostPtr, (IntPtr)(batchSize * sizeof(int)), indices, out _);
            try {
                SetKernelArg(_accumulateGradientBatchKernel, 0, tEmbGrad.Buffer); SetKernelArg(_accumulateGradientBatchKernel, 1, tBatchGrad.Buffer);
                SetKernelArg(_accumulateGradientBatchKernel, 2, memIndices); SetKernelArg(_accumulateGradientBatchKernel, 3, embSize);
                ExecuteKernel2D(_accumulateGradientBatchKernel, batchSize, embSize);
            } finally { ReleaseMemObject(memIndices); }
        }

        public void CreateOneHotTensorBatch(IMathTensor resultTensor, int[] indices)
        {
            var tRes = Unwrap(resultTensor);
            int batchSize = indices.Length;
            int vocabSize = resultTensor.Shape[1];
            Mem memIndices = (Mem)CreateBuffer(_context, MemFlags.ReadOnly | MemFlags.CopyHostPtr, (IntPtr)(batchSize * sizeof(int)), indices, out _);
            try {
                SetKernelArg(_oneHotEncodeBatchKernel, 0, tRes.Buffer); SetKernelArg(_oneHotEncodeBatchKernel, 1, memIndices);
                SetKernelArg(_oneHotEncodeBatchKernel, 2, vocabSize); ExecuteKernel1D(_oneHotEncodeBatchKernel, batchSize);
            } finally { ReleaseMemObject(memIndices); }
        }

        public void MatrixMultiply(IMathTensor a, IMathTensor b, IMathTensor result) { var tA=Unwrap(a); var tB=Unwrap(b); var tC=Unwrap(result); ExecuteKernel2D(_matrixMultiplyKernel, tA.Shape[0], tB.Shape[1], tA.Buffer, tB.Buffer, tC.Buffer, tA.Shape[0], tA.Shape[1], tB.Shape[1]); CheckPeriodicSync("MatMul"); }
        public void Add(IMathTensor a, IMathTensor b, IMathTensor result) { ExecuteKernel1D(_addKernel, Unwrap(a).Length, Unwrap(a).Buffer, Unwrap(b).Buffer, Unwrap(result).Buffer); CheckPeriodicSync("Add"); }
        public void AddBroadcast(IMathTensor a, IMathTensor b, IMathTensor r) { if(!ReferenceEquals(Unwrap(a),Unwrap(r))) ExecuteKernel1D(_cloneKernel,Unwrap(a).Length,Unwrap(a).Buffer,Unwrap(r).Buffer); ExecuteKernel1D(_addBroadcastKernel,Unwrap(r).Length,Unwrap(r).Buffer,Unwrap(b).Buffer,(int)Unwrap(b).Length); }
        public void Multiply(IMathTensor a, IMathTensor b, IMathTensor r) => ExecuteKernel1D(_multiplyKernel, Unwrap(a).Length, Unwrap(a).Buffer, Unwrap(b).Buffer, Unwrap(r).Buffer);
        public void Sigmoid(IMathTensor a, IMathTensor r) => ExecuteKernel1D(_sigmoidKernel, Unwrap(a).Length, Unwrap(a).Buffer, Unwrap(r).Buffer);
        public void Tanh(IMathTensor a, IMathTensor r) => ExecuteKernel1D(_tanhKernel, Unwrap(a).Length, Unwrap(a).Buffer, Unwrap(r).Buffer);
        public void Subtract(IMathTensor a, IMathTensor b, IMathTensor r) => ExecuteKernel1D(_subtractKernel, Unwrap(a).Length, Unwrap(a).Buffer, Unwrap(b).Buffer, Unwrap(r).Buffer);
        public void SigmoidDerivative(IMathTensor o, IMathTensor r) => ExecuteKernel1D(_sigmoidDerivativeKernel, Unwrap(o).Length, Unwrap(o).Buffer, Unwrap(r).Buffer);
        public void TanhDerivative(IMathTensor o, IMathTensor r) => ExecuteKernel1D(_tanhDerivativeKernel, Unwrap(o).Length, Unwrap(o).Buffer, Unwrap(r).Buffer);
        public void Softmax(IMathTensor i, IMathTensor r) => ExecuteKernel1D(_softmaxKernel, Unwrap(i).Shape[0], Unwrap(i).Buffer, Unwrap(r).Buffer, Unwrap(i).Shape[1]);
        public void AddScaled(IMathTensor t, IMathTensor s, float v) => ExecuteKernel1D(_addScaledKernel, Unwrap(t).Length, Unwrap(t).Buffer, Unwrap(s).Buffer, v);
        public void SubtractScaled(IMathTensor t, IMathTensor s, float v) => ExecuteKernel1D(_subtractScaledKernel, Unwrap(t).Length, Unwrap(t).Buffer, Unwrap(s).Buffer, v);
        public void Clip(IMathTensor t, float min, float max) => ExecuteKernel1D(_clipKernel, Unwrap(t).Length, Unwrap(t).Buffer, min, max);
        public void Scale(IMathTensor t, float s) => ExecuteKernel1D(_scaleKernel, Unwrap(t).Length, Unwrap(t).Buffer, s);
        public void SanitizeAndClip(IMathTensor t, float v) => ExecuteKernel1D(_sanitizeAndClipKernel, Unwrap(t).Length, Unwrap(t).Buffer, v);
        public void MatrixMultiplyTransposeA(IMathTensor a, IMathTensor b, IMathTensor r) { var tA=Unwrap(a); var tB=Unwrap(b); var tC=Unwrap(r); ExecuteKernel2D(_matrixMultiplyTransposeAKernel, tA.Shape[1], tB.Shape[1], tA.Buffer, tB.Buffer, tC.Buffer, tA.Shape[1], tA.Shape[0], tB.Shape[1]); }
        public void MatrixMultiplyTransposeB(IMathTensor a, IMathTensor b, IMathTensor r) { var tA=Unwrap(a); var tB=Unwrap(b); var tC=Unwrap(r); ExecuteKernel2D(_matrixMultiplyTransposeBKernel, tA.Shape[0], tB.Shape[0], tA.Buffer, tB.Buffer, tC.Buffer, tA.Shape[0], tA.Shape[1], tB.Shape[0]); }
        public void AdamUpdate(IMathTensor p, IMathTensor g, IMathTensor m, IMathTensor v, float lr, float b1, float b2, float eps, int t) => ExecuteKernel1D(_adamUpdateKernel, Unwrap(p).Length, Unwrap(p).Buffer, Unwrap(g).Buffer, Unwrap(m).Buffer, Unwrap(v).Buffer, lr, b1, b2, eps, t);
        public IMathTensor Clone(IMathTensor t) { var n=CreateTensor(t.Shape); ExecuteKernel1D(_cloneKernel, t.Length, Unwrap(t).Buffer, Unwrap(n).Buffer); return n; }
        public void Transpose(IMathTensor i, IMathTensor r) => ExecuteKernel2D(_transposeKernel, i.Shape[0], i.Shape[1], Unwrap(i).Buffer, Unwrap(r).Buffer, i.Shape[0], i.Shape[1]);
        
        public double CalculateSumOfSquares(IMathTensor tensor)
        {
            if (tensor.Length == 0) return 0.0;
            var gpu = Unwrap(tensor);
            int groupSize = 256;
            int numGroups = (int)((gpu.Length + groupSize - 1) / groupSize);
            using var partial = CreateTensor(new[] { numGroups });
            var partialGpu = Unwrap(partial);
            SetKernelArg(_sumOfSquaresKernel, 0, gpu.Buffer); SetKernelArg(_sumOfSquaresKernel, 1, (IntPtr)(groupSize * sizeof(float)), null);
            SetKernelArg(_sumOfSquaresKernel, 2, partialGpu.Buffer); SetKernelArg(_sumOfSquaresKernel, 3, (uint)tensor.Length);
            EnqueueNDRangeKernel(_commandQueue, _sumOfSquaresKernel, 1, null, new[] { (IntPtr)(numGroups * groupSize) }, new[] { (IntPtr)groupSize }, 0, null, out _);
            Finish(_commandQueue);
            return partial.ToCpuTensor().GetData().Sum();
        }

        // Stubs
        public IMathTensor CreateOneHotTensor(int[] idx, int sz) { int l=idx.Length; var r=CreateTensor(new[]{l,sz}); Mem b=default; try{b=(Mem)CreateBuffer(_context,MemFlags.ReadOnly|MemFlags.CopyHostPtr,(IntPtr)(l*4),idx,out _);ExecuteKernel1D(_oneHotEncodeKernel,l,Unwrap(r).Buffer,b,sz);}finally{ReleaseMemObject(b);}return r;}
        public void Lookup(IMathTensor m, int idx, IMathTensor r) => ExecuteKernel1D(_lookupKernel, m.Shape[1], Unwrap(m).Buffer, Unwrap(r).Buffer, idx, m.Shape[1]);
        public void AccumulateGradient(IMathTensor g, IMathTensor v, int idx) => ExecuteKernel1D(_accumulateGradientKernel, g.Shape[1], Unwrap(g).Buffer, Unwrap(v).Buffer, idx, g.Shape[1]);
        public void Slice(IMathTensor s, int r, IMathTensor d) { var fs=(int)d.Length; ExecuteKernel1D(_sliceKernel, fs, Unwrap(s), Unwrap(d), r*fs, fs); }
        public void Set(IMathTensor d, int r, IMathTensor s) { var fs=(int)s.Length; ExecuteKernel1D(_setKernel, fs, Unwrap(d), Unwrap(s), r*fs, fs); }
        public void LayerNorm(IMathTensor i, IMathTensor g, IMathTensor b, float e=1e-5f) => ExecuteKernel1D(_layerNormKernel, i.Shape[0], Unwrap(i).Buffer, Unwrap(g).Buffer, Unwrap(b).Buffer, i.Shape[1], e);

        private void ExecuteKernel1D(Kernel k, long size, params object[] args)
        {
            SetArgs(k, args);
            EnqueueNDRangeKernel(_commandQueue, k, 1, null, new[] { (IntPtr)size }, null, 0, null, out _);
        }

        private void ExecuteKernel2D(Kernel k, long x, long y, params object[] args)
        {
            SetArgs(k, args);
            EnqueueNDRangeKernel(_commandQueue, k, 2, null, new[] { (IntPtr)x, (IntPtr)y }, null, 0, null, out _);
        }

        private void SetArgs(Kernel k, object[] args)
        {
            for (int i = 0; i < args.Length; i++)
            {
                if (args[i] is Mem m) SetKernelArg(k, (uint)i, m);
                else if (args[i] is int v) SetKernelArg(k, (uint)i, v);
                else if (args[i] is float f) SetKernelArg(k, (uint)i, f);
            }
        }

        private void CheckPeriodicSync(string op)
        {
            if (++_operationsSinceLastSync >= SYNC_INTERVAL) { _syncGuard.SynchronizeBeforeRead($"Sync_{op}"); _eventPool.Cleanup(); _operationsSinceLastSync = 0; }
        }

        private void CheckError(ErrorCode err) { if (err != ErrorCode.Success) throw new Exception($"OpenCL Error: {err}"); }

        public void Dispose()
        {
            if (!_disposed) { Finish(_commandQueue); ReleaseContext(_context); _disposed = true; GC.SuppressFinalize(this); }
        }

        public GpuSyncGuard GetSyncGuard() => _syncGuard;
    }