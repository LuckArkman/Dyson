using Gpu;
using Interfaces;

namespace Brain;

public class TensorScope : IDisposable
{
    private readonly List<IMathTensor> _managedTensors;
    private readonly List<Action> _cleanupActions;
    private bool _disposed = false;

    private readonly IMathEngine _mathEngine;
    private readonly IndividualFileTensorManager? _tensorManager;
    private readonly TensorPool? _pool;
    private readonly GpuSyncGuard? _syncGuard;
    
    private readonly string _scopeName;

    public TensorScope(string scopeName, IMathEngine mathEngine, 
        IndividualFileTensorManager? tensorManager = null, TensorPool? pool = null)
    {
        _scopeName = scopeName;
        _mathEngine = mathEngine ?? throw new ArgumentNullException(nameof(mathEngine));
        _tensorManager = tensorManager;
        _pool = pool; // O Pool agora será usado!
        
        if (mathEngine is GpuMathEngine gpuEngine)
        {
            _syncGuard = gpuEngine.GetSyncGuard();
        }

        _managedTensors = new List<IMathTensor>();
        _cleanupActions = new List<Action>();
    }
    
    public IMathTensor CreateTensor(int[] shape)
    {
        IMathTensor tensor;
        if (_pool != null)
        {
            tensor = _pool.Rent(shape);
        }
        else
        {
            tensor = _mathEngine.CreateTensor(shape);
        }
        return Track(tensor);
    }

    public IMathTensor CreateTensor(float[] data, int[] shape)
    {
        var tensor = _mathEngine.CreateTensor(data, shape);
        return Track(tensor);
    }

    public IMathTensor LoadTensor(string id)
    {
        if (_tensorManager == null)
            throw new InvalidOperationException("TensorManager não fornecido ao TensorScope.");

        var tensor = _tensorManager.LoadTensor(id);
        return Track(tensor);
    }
    
    public TensorScope CreateSubScope(string name)
    {
        return new TensorScope($"{_scopeName}.{name}", _mathEngine, _tensorManager, _pool);
    }

    public T Track<T>(T tensor) where T : IMathTensor
    {
        if (_disposed) throw new ObjectDisposedException(nameof(TensorScope));
        if (tensor == null) throw new ArgumentNullException(nameof(tensor));
        _managedTensors.Add(tensor);
        return tensor;
    }
    
    public void Track(Action cleanupAction)
    {
        if (_disposed) throw new ObjectDisposedException(nameof(TensorScope));
        if (cleanupAction == null) throw new ArgumentNullException(nameof(cleanupAction));
        _cleanupActions.Add(cleanupAction);
    }
    
    public void Dispose()
    {
        if (_disposed) return;
        
        if (_syncGuard != null)
        {
            try { _syncGuard.SynchronizeBeforeRead($"ScopeDispose_{_scopeName}"); } catch { }
        }
        
        // Limpeza reversa
        for (int i = _cleanupActions.Count - 1; i >= 0; i--)
        {
            try { _cleanupActions[i].Invoke(); } catch { }
        }
        _cleanupActions.Clear();

        // Devolução ao Pool
        for (int i = _managedTensors.Count - 1; i >= 0; i--)
        {
            var tensor = _managedTensors[i];
            try
            {
                if (_pool != null && tensor.IsGpu)
                {
                    // Devolve ao pool em vez de destruir
                    _pool.Return(tensor);
                }
                else
                {
                    tensor.Dispose();
                }
            }
            catch { }
        }
        _managedTensors.Clear();
        
        _disposed = true;
    }
}