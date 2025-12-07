namespace Records;

public record AuthRequest(Guid CorrelationId, string NodeJwt) : _Message;