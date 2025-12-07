namespace Records;

public record JoinRequest(Guid CorrelationId, string NewNodeId, string NewNodeAddress) : _Message;