namespace Enums;

// ==================== ENUMS ====================

/// <summary>
/// Tipos de blockchain suportados
/// </summary>
public enum BlockchainNetwork
{
    Ethereum,
    BNBChain,
    Polygon,
    Solana,
    Avalanche,
    Arbitrum,
    Optimism,
    Base
}

/// <summary>
/// Tipos de ambiente de deploy
/// </summary>
public enum DeploymentEnvironment
{
    Development,
    Staging,
    Production,
    QA,
    UAT
}

/// <summary>
/// Tipos de estratégia de deploy
/// </summary>
public enum DeploymentStrategy
{
    BlueGreen,
    Canary,
    Rolling,
    Recreate
}

/// <summary>
/// Níveis de severidade
/// </summary>
public enum SeverityLevel
{
    Info = 1,
    Low = 2,
    Medium = 3,
    High = 4,
    Critical = 5
}

/// <summary>
/// Status de execução de workflow
/// </summary>
public enum WorkflowExecutionStatus
{
    Pending,
    Running,
    Completed,
    Failed,
    Cancelled,
    Paused
}

/// <summary>
/// Tipos de autenticação
/// </summary>
public enum AuthenticationType
{
    None,
    Basic,
    Bearer,
    ApiKey,
    OAuth2,
    JWT,
    HMAC
}

/// <summary>
/// Modelos de LLM suportados
/// </summary>
public enum LLMModel
{
    GPT4,
    GPT35Turbo,
    Claude3Opus,
    Claude3Sonnet,
    Claude3Haiku,
    GeminiPro,
    Llama3,
    Mistral
}

/// <summary>
/// Tipos de análise de código
/// </summary>
public enum CodeAnalysisType
{
    Static,
    Dynamic,
    Security,
    Performance,
    Quality,
    Complexity
}

/// <summary>
/// Tipos de testes
/// </summary>
public enum TestType
{
    Unit,
    Integration,
    EndToEnd,
    Performance,
    Security,
    Regression,
    Smoke,
    Contract
}

/// <summary>
/// Tipos de métricas de observabilidade
/// </summary>
public enum ObservabilityMetric
{
    CPU,
    Memory,
    Disk,
    Network,
    Latency,
    Throughput,
    ErrorRate,
    RequestCount
}

/// <summary>
/// Provedores de cloud
/// </summary>
public enum CloudProvider
{
    AWS,
    Azure,
    GCP,
    DigitalOcean,
    Linode,
    OnPremise
}

/// <summary>
/// Tipos de banco de dados
/// </summary>
public enum DatabaseType
{
    MySQL,
    PostgreSQL,
    MongoDB,
    Redis,
    SQLServer,
    Oracle,
    Cassandra,
    DynamoDB
}

// ==================== CLASSES AUXILIARES ====================

/// <summary>
/// Item de memória para agentes
/// </summary>
public class MemoryItem
{
    public string Content { get; set; }
    public DateTime Timestamp { get; set; }
    public Dictionary<string, object> Metadata { get; set; } = new();
}

/// <summary>
/// Resultado de análise de código
/// </summary>
public class CodeAnalysisResult
{
    public int IssuesFound { get; set; }
    public List<CodeIssue> Issues { get; set; } = new();
    public Dictionary<string, int> IssuesBySeverity { get; set; } = new();
    public decimal QualityScore { get; set; }
}

/// <summary>
/// Issue de código
/// </summary>
public class CodeIssue
{
    public string Id { get; set; }
    public string File { get; set; }
    public int Line { get; set; }
    public string Severity { get; set; }
    public string Category { get; set; }
    public string Message { get; set; }
    public string Rule { get; set; }
    public string SuggestedFix { get; set; }
}

/// <summary>
/// Resultado de teste
/// </summary>
public class TestResult
{
    public int Total { get; set; }
    public int Passed { get; set; }
    public int Failed { get; set; }
    public int Skipped { get; set; }
    public decimal Coverage { get; set; }
    public TimeSpan Duration { get; set; }
    public List<TestFailure> Failures { get; set; } = new();
}

/// <summary>
/// Falha em teste
/// </summary>
public class TestFailure
{
    public string TestName { get; set; }
    public string ErrorMessage { get; set; }
    public string StackTrace { get; set; }
    public string Category { get; set; }
}

/// <summary>
/// Métrica de performance
/// </summary>
public class PerformanceMetric
{
    public string Name { get; set; }
    public double Value { get; set; }
    public string Unit { get; set; }
    public DateTime Timestamp { get; set; }
    public Dictionary<string, string> Tags { get; set; } = new();
}

/// <summary>
/// Vulnerabilidade detectada
/// </summary>
public class Vulnerability
{
    public string Id { get; set; }
    public string CVE { get; set; }
    public string Title { get; set; }
    public string Description { get; set; }
    public string Severity { get; set; }
    public string AffectedPackage { get; set; }
    public string FixedVersion { get; set; }
    public List<string> References { get; set; } = new();
}

/// <summary>
/// Resultado de deploy
/// </summary>
public class DeploymentResult
{
    public bool Success { get; set; }
    public string Version { get; set; }
    public string Environment { get; set; }
    public DateTime DeployedAt { get; set; }
    public TimeSpan Duration { get; set; }
    public string DeployedBy { get; set; }
    public List<string> Changes { get; set; } = new();
    public string RollbackVersion { get; set; }
}

/// <summary>
/// Métricas de infraestrutura
/// </summary>
public class InfrastructureMetrics
{
    public double CPUUsage { get; set; }
    public double MemoryUsage { get; set; }
    public double DiskUsage { get; set; }
    public double NetworkIn { get; set; }
    public double NetworkOut { get; set; }
    public int ActiveConnections { get; set; }
    public Dictionary<string, double> CustomMetrics { get; set; } = new();
}

/// <summary>
/// Configuração de escalabilidade
/// </summary>
public class ScalingConfig
{
    public int MinInstances { get; set; }
    public int MaxInstances { get; set; }
    public int CurrentInstances { get; set; }
    public Dictionary<string, decimal> ScalingTriggers { get; set; } = new();
    public int CooldownPeriod { get; set; }
}

/// <summary>
/// Resultado de análise de sentimento
/// </summary>
public class SentimentResult
{
    public string Text { get; set; }
    public string Sentiment { get; set; } // Positive, Negative, Neutral
    public decimal Confidence { get; set; }
    public Dictionary<string, decimal> Emotions { get; set; } = new();
    public List<string> Keywords { get; set; } = new();
}

/// <summary>
/// Resposta de LLM
/// </summary>
public class LLMResponse
{
    public string Content { get; set; }
    public string Model { get; set; }
    public int TokensUsed { get; set; }
    public decimal Temperature { get; set; }
    public TimeSpan ResponseTime { get; set; }
    public Dictionary<string, object> Metadata { get; set; } = new();
}

/// <summary>
/// Configuração de webhook
/// </summary>
public class WebhookConfig
{
    public string Url { get; set; }
    public string Method { get; set; } = "POST";
    public Dictionary<string, string> Headers { get; set; } = new();
    public string Secret { get; set; }
    public List<string> EventTypes { get; set; } = new();
    public int RetryAttempts { get; set; } = 3;
    public int TimeoutSeconds { get; set; } = 30;
}

/// <summary>
/// Entrada de log de auditoria
/// </summary>
public class AuditLogEntry
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public string AgentId { get; set; }
    public string NodeId { get; set; }
    public string Action { get; set; }
    public string User { get; set; }
    public Dictionary<string, object> Before { get; set; } = new();
    public Dictionary<string, object> After { get; set; } = new();
    public string IpAddress { get; set; }
    public Dictionary<string, string> Metadata { get; set; } = new();
}