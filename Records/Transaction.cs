namespace Records;

public record Transaction(
    Guid Id,
    DateTime Timestamp,
    string FromAddress, 
    string ToAddress,
    decimal Amount,
    string? Notes,
    string Hash 
);