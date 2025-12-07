namespace Records;

public record AuthResponse(Guid CorrelationId, bool Success, string Message) : _Message;