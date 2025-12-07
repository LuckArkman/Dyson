namespace Records;

public record ForwardJoinRequest(Guid CorrelationId, JoinRequest OriginalRequest) : _Message;