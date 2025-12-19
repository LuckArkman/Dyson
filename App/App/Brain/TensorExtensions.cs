using Core;

namespace Brain;

public static class TensorExtensions
{
    public static TensorData ToTensorData(this Tensor tensor)
    {
        return new TensorData { data = tensor.GetData(), shape = tensor.shape };
    }
}