using MongoDB.Bson.Serialization.Attributes;

namespace Dtos;

/// <summary>
/// Representa o status de execução de um Smart Agent
/// </summary>
public class AgentExecutionStatus
{
    [BsonId]
    public string Id { get; set; } = Guid.NewGuid().ToString();
    
    public string AgentId { get; set; }
    public string AgentName { get; set; }
    public string Status { get; set; } // running, stopped, pending, failed
    public DateTime StartedAt { get; set; }
    public DateTime? FinishedAt { get; set; }
    public string? ErrorMessage { get; set; }
    public string? ExecutionLogs { get; set; }
    public int ExecutionCount { get; set; }
    public DateTime LastExecutedAt { get; set; }
    
    // Métricas de performance
    public double AverageExecutionTimeMs { get; set; }
    public int SuccessCount { get; set; }
    public int FailureCount { get; set; }
}

/// <summary>
/// Enum para status de agentes (tipagem forte)
/// </summary>
public enum AgentStatus
{
    Running,    // Em execução
    Stopped,    // Parado manualmente
    Pending,    // Aguardando gatilho
    Failed      // Falha crítica
}