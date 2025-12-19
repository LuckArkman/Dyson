using OpenCL.NetCore;

namespace Gpu;

public class OpenClException : Exception
{
    public OpenClException(string message, ErrorCode error) : base($"{message} (Código de Erro: {error})") { }
}