using Akka.IO;
using Enums;
using Records;

namespace Dtos;

public record OperationRequest : _Message
{
    public string input { get; set; }
    public  OpType _type { get; set; }
}