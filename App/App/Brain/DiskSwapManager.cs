using System.Collections.Concurrent;
using Interfaces;

namespace Brain;

public class DiskSwapManager : IDisposable
    {
        private readonly IMathEngine _mathEngine;
        // Cache em memória (Hot Path)
        private readonly ConcurrentDictionary<string, IMathTensor> _ramCache;
        // Fallback em disco (Cold Path)
        private readonly DiskSwapBackend _diskBackend;
        
        private bool _disposed = false;
        private readonly long _memoryLimitBytes;
        private long _currentEstimatedBytes = 0;

        // Limite padrão de 12GB para garantir uso máximo da RAM disponível
        public DiskSwapManager(IMathEngine mathEngine, string sessionId, long memoryLimitMb = 12000)
        {
            _mathEngine = mathEngine ?? throw new ArgumentNullException(nameof(mathEngine));
            _ramCache = new ConcurrentDictionary<string, IMathTensor>();
            _diskBackend = new DiskSwapBackend(mathEngine, sessionId);
            _memoryLimitBytes = memoryLimitMb * 1024 * 1024;
            
            Console.WriteLine($"[DiskSwap] 🚀 Modo Híbrido Ativo. Limite RAM: {memoryLimitMb}MB");
        }

        public string SwapOut(IMathTensor tensor, string label)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(DiskSwapManager));

            string id = $"swap::{label}_{Guid.NewGuid():N}";

            long tensorSize = tensor.Length * 4; 
            
            // Lógica Híbrida: Se couber na RAM, fica na RAM.
            if (_currentEstimatedBytes + tensorSize < _memoryLimitBytes)
            {
                // CLONA o tensor para persistir no cache independentemente do escopo original.
                var storedTensor = _mathEngine.Clone(tensor); 
                
                if (_ramCache.TryAdd(id, storedTensor))
                {
                    System.Threading.Interlocked.Add(ref _currentEstimatedBytes, tensorSize);
                    return id;
                }
                else
                {
                    // Falha rara de concorrência, libera o clone
                    storedTensor.Dispose();
                }
            }

            // Fallback: Memória cheia -> Disco
            return _diskBackend.SwapOut(tensor, label);
        }

        public IMathTensor LoadFromSwap(string swapId)
        {
            // 1. Tenta RAM (Peek - NÃO REMOVE)
            if (_ramCache.TryGetValue(swapId, out var tensor))
            {
                // CRÍTICO: Retorna um Clone!
                // O chamador vai colocar isso num TensorScope e dar Dispose.
                // Se retornarmos a referência direta, o cache fica com um objeto Disposed.
                return _mathEngine.Clone(tensor);
            }

            // 2. Tenta Disco
            return _diskBackend.LoadFromSwap(swapId);
        }

        public void DeleteSwapFile(string swapId)
        {
            // A remoção real da memória só acontece aqui
            if (_ramCache.TryRemove(swapId, out var tensor))
            {
                System.Threading.Interlocked.Add(ref _currentEstimatedBytes, -(tensor.Length * 4));
                tensor.Dispose(); // Libera VRAM/RAM
            }
            else
            {
                _diskBackend.DeleteSwapFile(swapId);
            }
        }

        public void ClearAllSwap()
        {
            foreach (var t in _ramCache.Values) t.Dispose();
            _ramCache.Clear();
            _currentEstimatedBytes = 0;
            _diskBackend.ClearAllSwap();
        }

        public void Dispose()
        {
            if (_disposed) return;
            ClearAllSwap();
            _diskBackend.Dispose();
            _disposed = true;
        }

        // Backend de disco (mantido idêntico à lógica funcional original)
        private class DiskSwapBackend : IDisposable
        {
            private readonly IMathEngine _mathEngine;
            private readonly ConcurrentDictionary<string, string> _filePaths;
            private readonly string _tempDir;

            public DiskSwapBackend(IMathEngine mathEngine, string sessionId)
            {
                _mathEngine = mathEngine;
                _filePaths = new ConcurrentDictionary<string, string>();
                _tempDir = Path.Combine(Environment.CurrentDirectory, "Dayson", "Swap", sessionId);
                if (Directory.Exists(_tempDir)) Directory.Delete(_tempDir, true);
                Directory.CreateDirectory(_tempDir);
            }

            public string SwapOut(IMathTensor tensor, string label)
            {
                string id = $"disk::{label}_{Guid.NewGuid():N}";
                string path = Path.Combine(_tempDir, $"{id}.bin");
                
                using (var fs = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None, 4096))
                using (var bw = new BinaryWriter(fs))
                {
                    bw.Write(tensor.Shape.Length);
                    foreach (var s in tensor.Shape) bw.Write(s);
                    bw.Write(tensor.Length);
                    tensor.WriteToStream(bw);
                }
                
                _filePaths[id] = path;
                return id;
            }

            public IMathTensor LoadFromSwap(string id)
            {
                if (!_filePaths.TryGetValue(id, out var path)) 
                    throw new FileNotFoundException($"Swap não encontrado em disco ou RAM: {id}");

                using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read))
                using (var br = new BinaryReader(fs))
                {
                    int rank = br.ReadInt32();
                    int[] shape = new int[rank];
                    for (int i = 0; i < rank; i++) shape[i] = br.ReadInt32();
                    long len = br.ReadInt64();
                    
                    var t = _mathEngine.CreateTensor(shape);
                    t.ReadFromStream(br, len);
                    return t;
                }
            }

            public void DeleteSwapFile(string id)
            {
                if (_filePaths.TryRemove(id, out var path))
                {
                    try { File.Delete(path); } catch { }
                }
            }

            public void ClearAllSwap()
            {
                foreach(var p in _filePaths.Values) try { File.Delete(p); } catch {}
                _filePaths.Clear();
            }

            public void Dispose()
            {
                ClearAllSwap();
                try { Directory.Delete(_tempDir, true); } catch { }
            }
        }
    }