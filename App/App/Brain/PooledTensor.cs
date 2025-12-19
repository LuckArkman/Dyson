using Core;
using Interfaces;

namespace Brain;

public class PooledTensor : IMathTensor
{
    private readonly IMathTensor _innerTensor;
    private readonly TensorPool _pool;
    private bool _disposed = false;

    public PooledTensor(IMathTensor innerTensor, TensorPool pool)
    {
        _innerTensor = innerTensor;
        _pool = pool;
    }

    // Acesso ao tensor real (usado pelo MathEngine)
    public IMathTensor InnerTensor => _innerTensor;

    // Propriedades delegadas
    public int[] Shape => _innerTensor.Shape;
    public long Length => _innerTensor.Length;
    public bool IsGpu => _innerTensor.IsGpu;

    // Delegação de métodos
    public Tensor ToCpuTensor() => _innerTensor.ToCpuTensor();
    public void UpdateFromCpu(float[] data) => _innerTensor.UpdateFromCpu(data);
    public void WriteToStream(BinaryWriter writer) => _innerTensor.WriteToStream(writer);
    public void ReadFromStream(BinaryReader reader, long length) => _innerTensor.ReadFromStream(reader, length);

    public void Dispose()
    {
        if (_disposed) return;
            
        // 🔥 MÁGICA AQUI: Devolve ao pool em vez de destruir
        _pool.Return(_innerTensor);
            
        _disposed = true;
    }
}