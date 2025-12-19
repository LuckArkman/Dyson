using Brain;
using Interfaces;

namespace Brain;

public class TensorPool : IDisposable
{
    private readonly IMathEngine _mathEngine;
    private readonly Dictionary<string, Queue<IMathTensor>> _pools;
    private readonly HashSet<IMathTensor> _inUse; // Rastreia os tensores REAIS (Inner)
    private bool _disposed = false;

    // Parâmetros de memória
    private const int MAX_POOL_SIZE_PER_SHAPE = 64;
    private const long MAX_TOTAL_MEMORY_MB = 4096;
    private int _operationsSinceLastTrim = 0;
    private const int TRIM_INTERVAL = 1000;

    public TensorPool(IMathEngine mathEngine)
    {
        _mathEngine = mathEngine;
        _pools = new Dictionary<string, Queue<IMathTensor>>();
        _inUse = new HashSet<IMathTensor>();
    }

    public IMathTensor Rent(int[] shape)
    {
        string key = GetKey(shape);
        
        if (!_pools.ContainsKey(key))
            _pools[key] = new Queue<IMathTensor>();
        
        var pool = _pools[key];
        IMathTensor tensor = null;

        // 1. Tenta reusar (com verificação de saúde)
        while (pool.Count > 0)
        {
            tensor = pool.Dequeue();
            // Se por algum motivo o tensor real morreu (Dispose externo), descarta e tenta outro
            // (Isso requer try-catch ou checagem de propriedade se disponível)
            // Assumimos vivo se saiu da fila.
            break;
        }

        // 2. Cria novo se necessário
        if (tensor == null)
        {
            if (GetTotalMemoryUsageMB() > MAX_TOTAL_MEMORY_MB)
            {
                // Console.WriteLine($"[TensorPool] Trim automático (Memória cheia)...");
                TrimExcessMemory();
            }
            tensor = _mathEngine.CreateTensor(shape);
        }

        // 3. Registra uso
        _inUse.Add(tensor);

        // 4. Auto-trim
        _operationsSinceLastTrim++;
        if (_operationsSinceLastTrim >= TRIM_INTERVAL)
        {
            TrimExcessMemory();
            _operationsSinceLastTrim = 0;
        }

        // 🔥 RETORNA O WRAPPER!
        return new PooledTensor(tensor, this);
    }

    /// <summary>
    /// Chamado pelo PooledTensor.Dispose()
    /// </summary>
    public void Return(IMathTensor tensor)
    {
        // Se tentarem devolver o wrapper, extraímos o interno (segurança)
        if (tensor is PooledTensor pooled)
            tensor = pooled.InnerTensor;

        if (tensor == null || !_inUse.Contains(tensor))
            return; // Já devolvido ou não é nosso

        _inUse.Remove(tensor);
        string key = GetKey(tensor.Shape);

        if (!_pools.ContainsKey(key)) _pools[key] = new Queue<IMathTensor>();
        var pool = _pools[key];

        // Limite por shape
        if (pool.Count >= MAX_POOL_SIZE_PER_SHAPE)
        {
            tensor.Dispose(); // Destrói fisicamente pois o pool está cheio
        }
        else
        {
            pool.Enqueue(tensor); // Guarda para reuso
        }
    }

    private void TrimExcessMemory()
    {
        long currentMemoryMB = GetTotalMemoryUsageMB();
        long targetMemoryMB = MAX_TOTAL_MEMORY_MB / 2;
        if (currentMemoryMB <= targetMemoryMB) return;

        var sortedPools = _pools.OrderByDescending(kvp => kvp.Value.Count * GetShapeMemoryMB(kvp.Key)).ToList();
        
        foreach (var (shape, pool) in sortedPools)
        {
            int keepCount = Math.Max(2, pool.Count / 4);
            while (pool.Count > keepCount)
            {
                var tensor = pool.Dequeue();
                tensor.Dispose();
            }
            if (GetTotalMemoryUsageMB() <= targetMemoryMB) break;
        }
        GC.Collect(2, GCCollectionMode.Forced, true, true);
    }

    public void Trim()
    {
        foreach (var pool in _pools.Values)
        {
            while (pool.Count > 0) pool.Dequeue().Dispose();
        }
        _pools.Clear();
    }

    private long GetTotalMemoryUsageMB()
    {
        long totalBytes = _inUse.Sum(t => t.Length * 4);
        foreach (var (key, pool) in _pools) totalBytes += GetShapeMemoryBytes(key) * pool.Count;
        return totalBytes / (1024 * 1024);
    }

    private long GetShapeMemoryBytes(string shapeKey)
    {
        var dims = shapeKey.Split('x');
        long len = 1;
        foreach(var d in dims) len *= long.Parse(d);
        return len * 4;
    }
    
    private long GetShapeMemoryMB(string shapeKey) => GetShapeMemoryBytes(shapeKey) / (1024 * 1024);
    private string GetKey(int[] shape) => string.Join("x", shape);

    public void Dispose()
    {
        if (_disposed) return;
        foreach (var t in _inUse) t.Dispose();
        Trim();
        _inUse.Clear();
        _disposed = true;
    }
}