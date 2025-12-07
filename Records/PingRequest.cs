namespace Records;

public record PingRequest(Guid CorrelationId, string FromNodeId) : _Message;