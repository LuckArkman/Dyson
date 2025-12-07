namespace Records;

public record JoinResponse(Guid CorrelationId, bool Success, string ParentNodeId, string Message) : _Message;