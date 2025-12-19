using System.Collections.Concurrent;
using Interfaces;

namespace Brain;

public class IndividualFileTensorManager : IDisposable
{
    private readonly IMathEngine _mathEngine;
    private readonly string _tensorDirectory;

    // Cache L1: Mantém os tensores carregados na memória para acesso ultrarrápido
    private readonly ConcurrentDictionary<string, IMathTensor> _weightCache;

    private readonly ConcurrentDictionary<string, int[]> _tensorIndex;
    private readonly ConcurrentDictionary<string, object> _tensorUpdateLocks;
    private int _nextTensorId = 0;
    private bool _disposed = false;

    // Buffer de 64KB
    private const int FILE_BUFFER_SIZE = 64 * 1024;

    public IndividualFileTensorManager(IMathEngine mathEngine, string sessionId)
    {
        _mathEngine = mathEngine;
        _tensorDirectory = Path.Combine(Environment.CurrentDirectory, "Dayson", "TensorCache", sessionId);
        _tensorIndex = new ConcurrentDictionary<string, int[]>();
        _tensorUpdateLocks = new ConcurrentDictionary<string, object>();
        _weightCache = new ConcurrentDictionary<string, IMathTensor>();

        if (Directory.Exists(_tensorDirectory))
        {
            try
            {
                Directory.Delete(_tensorDirectory, recursive: true);
            }
            catch
            {
            }
        }

        Directory.CreateDirectory(_tensorDirectory);
        Console.WriteLine($"[TensorManager] Cache de pesos em: {_tensorDirectory}");
    }

    private string GetPathForId(string id) => Path.Combine(_tensorDirectory, $"{id}.bin");

    public string StoreTensor(IMathTensor tensor, string name)
    {
        if (tensor == null) throw new ArgumentNullException(nameof(tensor));

        int uniqueSequenceId = Interlocked.Increment(ref _nextTensorId);
        string id = $"{name.Replace(" ", "_")}_{uniqueSequenceId:D8}_{Guid.NewGuid():N}";
        string filePath = GetPathForId(id);

        try
        {
            // 1. Persistência em Disco
            using (var fileStream = new FileStream(filePath, FileMode.CreateNew, FileAccess.Write, FileShare.None,
                       FILE_BUFFER_SIZE))
            using (var writer = new BinaryWriter(fileStream))
            {
                writer.Write(tensor.Shape.Length);
                foreach (var dim in tensor.Shape) writer.Write(dim);
                writer.Write(tensor.Length);

                tensor.WriteToStream(writer);

                writer.Flush();
                fileStream.Flush(true);
            }

            if (!_tensorIndex.TryAdd(id, (int[])tensor.Shape.Clone()))
                throw new InvalidOperationException($"Falha ao registrar ID {id}");

            // 2. Cache em Memória (Lazy Loading vs Eager Loading)
            // Não adicionamos ao cache imediatamente aqui porque o tensor de origem 'tensor'
            // geralmente pertence a um escopo temporário e será descartado logo.
            // Deixamos o cache ser populado na primeira chamada de LoadTensor.

            return id;
        }
        catch (IOException ex)
        {
            throw new IOException($"Erro ao gravar tensor {filePath}", ex);
        }
    }

    public IMathTensor LoadTensor(string id)
    {
        // 1. Tenta recuperar do Cache de Memória (VRAM/RAM) - Caminho Rápido
        if (_weightCache.TryGetValue(id, out var cachedTensor))
        {
            // Retornamos um CLONE para garantir que o TensorScope do chamador
            // possa chamar Dispose() sem destruir a cópia mestre do cache.
            // Clonagem VRAM-VRAM é extremamente rápida (microssegundos).
            return _mathEngine.Clone(cachedTensor);
        }

        // 2. Caminho Lento: Carregar do Disco
        if (!_tensorIndex.TryGetValue(id, out var shapeFromIndex))
            throw new KeyNotFoundException($"Tensor {id} não encontrado no índice.");

        string filePath = GetPathForId(id);
        if (!File.Exists(filePath))
            throw new FileNotFoundException($"Arquivo físico sumiu: {filePath}");

        IMathTensor tensorFromDisk;
        using (var fileStream =
               new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, FILE_BUFFER_SIZE))
        using (var reader = new BinaryReader(fileStream))
        {
            int shapeRank = reader.ReadInt32();
            var shapeFromFile = new int[shapeRank];
            for (int i = 0; i < shapeRank; i++) shapeFromFile[i] = reader.ReadInt32();

            long lengthFromFile = reader.ReadInt64();

            tensorFromDisk = _mathEngine.CreateTensor(shapeFromFile);
            tensorFromDisk.ReadFromStream(reader, lengthFromFile);
        }

        // 3. Salva no Cache para leituras futuras
        // Armazenamos este tensor diretamente. Ele se torna a cópia "Mestre" na VRAM.
        _weightCache.TryAdd(id, tensorFromDisk);

        // Retornamos um CLONE para o chamador usar e descartar
        return _mathEngine.Clone(tensorFromDisk);
    }

    public void OverwriteTensor(string id, IMathTensor sourceTensor)
    {
        if (!_tensorIndex.ContainsKey(id))
            throw new KeyNotFoundException($"Tentativa de sobrescrever tensor inexistente: {id}");

        var tensorLock = _tensorUpdateLocks.GetOrAdd(id, _ => new object());
        lock (tensorLock)
        {
            // 1. Atualiza Disco (Persistência)
            string filePath = GetPathForId(id);
            using (var fileStream = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None,
                       FILE_BUFFER_SIZE))
            using (var writer = new BinaryWriter(fileStream))
            {
                writer.Write(sourceTensor.Shape.Length);
                foreach (var dim in sourceTensor.Shape) writer.Write(dim);
                writer.Write(sourceTensor.Length);

                sourceTensor.WriteToStream(writer);

                writer.Flush();
                fileStream.Flush(true);
            }

            _tensorIndex[id] = (int[])sourceTensor.Shape.Clone();

            // 2. Atualiza Cache
            // Precisamos substituir a entrada no cache pela nova versão.
            // Importante: Clonamos o sourceTensor porque ele provavelmente será descartado pelo chamador.
            var newCachedVersion = _mathEngine.Clone(sourceTensor);

            // Se já existia algo no cache, removemos e damos Dispose para liberar VRAM antiga
            if (_weightCache.TryRemove(id, out var oldTensor))
            {
                oldTensor.Dispose();
            }

            _weightCache.TryAdd(id, newCachedVersion);
        }
    }

    // Métodos de suporte
    public string CreateAndStore(float[] data, int[] shape, string name)
    {
        using var tensor = _mathEngine.CreateTensor(data, shape);
        return StoreTensor(tensor, name);
    }

    public string CreateAndStoreZeros(int[] shape, string name)
    {
        using var tensor = _mathEngine.CreateTensor(shape);
        return StoreTensor(tensor, name);
    }

    public void UpdateTensor(string id, Action<IMathTensor> operation)
    {
        var tensorLock = _tensorUpdateLocks.GetOrAdd(id, _ => new object());
        lock (tensorLock)
        {
            // Carrega (do cache ou disco)
            // Nota: LoadTensor retorna um Clone. Se usarmos ele, a operação ocorre no Clone.
            // Para UpdateTensor funcionar corretamente com cache, precisamos atualizar o cache depois.
            using IMathTensor tensor = LoadTensor(id);

            operation(tensor);

            // Salva de volta (Disco + Cache)
            OverwriteTensor(id, tensor);
        }
    }

    public int[] GetShape(string id)
    {
        return _tensorIndex.TryGetValue(id, out var shape) ? (int[])shape.Clone() : throw new KeyNotFoundException(id);
    }

    public void DeleteTensor(string id)
    {
        if (string.IsNullOrEmpty(id)) return;

        // Remove do índice e locks
        _tensorIndex.TryRemove(id, out _);
        _tensorUpdateLocks.TryRemove(id, out _);

        // Remove do Cache e libera memória
        if (_weightCache.TryRemove(id, out var cachedTensor))
        {
            cachedTensor.Dispose();
        }

        // Remove do Disco
        try
        {
            if (File.Exists(GetPathForId(id))) File.Delete(GetPathForId(id));
        }
        catch
        {
        }
    }

    public (int Count, long DiskMB, long RamMB, int TotalAccesses) GetStatistics()
    {
        return (_tensorIndex.Count, 0, 0, 0);
    }

    public void Dispose()
    {
        if (_disposed) return;

        // Limpa cache e libera memória de GPU
        foreach (var tensor in _weightCache.Values)
        {
            tensor.Dispose();
        }

        _weightCache.Clear();

        _tensorIndex.Clear();
        _tensorUpdateLocks.Clear();

        // Limpa arquivos temporários
        try
        {
            if (Directory.Exists(_tensorDirectory)) Directory.Delete(_tensorDirectory, recursive: true);
        }
        catch
        {
        }

        _disposed = true;
    }
}