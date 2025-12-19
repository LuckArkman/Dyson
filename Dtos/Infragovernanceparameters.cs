namespace Dtos;

// ==================== CI/CD & DEPLOY ====================

/// <summary>
/// Parâmetros para pipeline de build
/// </summary>
public class BuildPipelineParameters : BaseNodeParameters
{
    public List<string> Stages { get; set; } = new(); // Compile, Test, Package
    public string BuildTool { get; set; } // MSBuild, Maven, Gradle, npm
    public Dictionary<string, string> Variables { get; set; } = new();
    public bool ParallelExecution { get; set; } = false;
    public int TimeoutMinutes { get; set; } = 30;
}

/// <summary>
/// Parâmetros para pipeline de testes
/// </summary>
public class TestPipelineParameters : BaseNodeParameters
{
    public List<string> TestSuites { get; set; } = new();
    public bool RunInParallel { get; set; } = true;
    public bool FailFast { get; set; } = false;
    public decimal CoverageThreshold { get; set; } = 80m;
    public string ReportFormat { get; set; } = "JUnit";
}

/// <summary>
/// Parâmetros para gestor de releases
/// </summary>
public class ReleaseManagerParameters : BaseNodeParameters
{
    public string Version { get; set; }
    public string VersioningScheme { get; set; } // SemVer, CalVer
    public bool AutoIncrement { get; set; } = true;
    public bool CreateTag { get; set; } = true;
    public string ReleaseNotes { get; set; }
}

/// <summary>
/// Parâmetros para deploy automatizado
/// </summary>
public class AutoDeployParameters : BaseNodeParameters
{
    public string Environment { get; set; } // Dev, Staging, Production
    public string DeploymentType { get; set; } // BlueGreen, Canary, Rolling
    public bool RequireApproval { get; set; } = false;
    public int HealthCheckTimeout { get; set; } = 300;
    public bool AutoRollback { get; set; } = true;
}

/// <summary>
/// Parâmetros para deploy em desenvolvimento
/// </summary>
public class DeployDevParameters : BaseNodeParameters
{
    public string Config { get; set; }
    public bool SkipTests { get; set; } = false;
    public bool PreserveData { get; set; } = true;
    public List<string> Services { get; set; } = new();
}

/// <summary>
/// Parâmetros para deploy em staging
/// </summary>
public class DeployStagingParameters : BaseNodeParameters
{
    public string Config { get; set; }
    public bool RunSmokeTests { get; set; } = true;
    public bool NotifyTeam { get; set; } = true;
    public string DataSource { get; set; } // Production, Synthetic
}

/// <summary>
/// Parâmetros para deploy em produção
/// </summary>
public class DeployProductionParameters : BaseNodeParameters
{
    public string Config { get; set; }
    public List<string> Approvers { get; set; } = new();
    public bool RequireAllApprovals { get; set; } = true;
    public int CanaryPercentage { get; set; } = 10;
    public bool EnableMonitoring { get; set; } = true;
}

/// <summary>
/// Parâmetros para rollback automático
/// </summary>
public class RollbackManagerParameters : BaseNodeParameters
{
    public string Version { get; set; }
    public string Reason { get; set; }
    public bool PreserveLogs { get; set; } = true;
    public bool NotifyStakeholders { get; set; } = true;
    public int RollbackTimeout { get; set; } = 600;
}

/// <summary>
/// Parâmetros para configuração de ambiente
/// </summary>
public class EnvironmentConfigParameters : BaseNodeParameters
{
    public Dictionary<string, string> EnvVars { get; set; } = new();
    public string ConfigSource { get; set; } // File, Vault, AWS Secrets
    public bool EncryptSensitive { get; set; } = true;
    public bool ValidateBeforeApply { get; set; } = true;
}

// ==================== INFRAESTRUTURA & ESCALABILIDADE ====================

/// <summary>
/// Parâmetros para análise de infraestrutura
/// </summary>
public class InfraAnalyzerParameters : BaseNodeParameters
{
    public string Provider { get; set; } // AWS, Azure, GCP, On-Premise
    public List<string> Resources { get; set; } = new();
    public bool IncludeCostAnalysis { get; set; } = true;
    public bool CheckCompliance { get; set; } = true;
}

/// <summary>
/// Parâmetros para monitoramento de servidores
/// </summary>
public class ServerMonitorParameters : BaseNodeParameters
{
    public List<string> Metrics { get; set; } = new(); // CPU, Memory, Disk, Network
    public int IntervalSeconds { get; set; } = 60;
    public Dictionary<string, decimal> Thresholds { get; set; } = new();
    public string AlertChannel { get; set; }
}

/// <summary>
/// Parâmetros para gestor de containers
/// </summary>
public class ContainerManagerParameters : BaseNodeParameters
{
    public string Orchestrator { get; set; } // Docker, Kubernetes, Docker Swarm
    public string Action { get; set; } // Start, Stop, Restart, Scale
    public int Replicas { get; set; } = 1;
    public Dictionary<string, string> Labels { get; set; } = new();
}

/// <summary>
/// Parâmetros para otimizador cloud
/// </summary>
public class CloudOptimizerParameters : BaseNodeParameters
{
    public string Provider { get; set; }
    public List<string> OptimizationTargets { get; set; } = new(); // Cost, Performance, Both
    public bool AutoApply { get; set; } = false;
    public decimal CostSavingsGoal { get; set; } = 20m; // Percentual
}

/// <summary>
/// Parâmetros para monitoramento de recursos
/// </summary>
public class ResourceMonitorParameters : BaseNodeParameters
{
    public List<string> Resources { get; set; } = new();
    public bool TrackTrends { get; set; } = true;
    public int RetentionDays { get; set; } = 30;
    public string AggregationInterval { get; set; } = "5m";
}

/// <summary>
/// Parâmetros para detecção de falhas
/// </summary>
public class FailureDetectorParameters : BaseNodeParameters
{
    public string HealthCheck { get; set; }
    public int IntervalSeconds { get; set; } = 30;
    public int FailureThreshold { get; set; } = 3;
    public bool AutoRestart { get; set; } = false;
}

/// <summary>
/// Parâmetros para gestor de escalabilidade
/// </summary>
public class ScalabilityManagerParameters : BaseNodeParameters
{
    public string Strategy { get; set; } // Horizontal, Vertical, Both
    public Dictionary<string, decimal> Triggers { get; set; } = new();
    public int MinInstances { get; set; } = 1;
    public int MaxInstances { get; set; } = 10;
    public int CooldownSeconds { get; set; } = 300;
}

/// <summary>
/// Parâmetros para teste de carga
/// </summary>
public class LoadTesterParameters : BaseNodeParameters
{
    public int Users { get; set; } = 100;
    public int Duration { get; set; } = 300; // segundos
    public string RampUpStrategy { get; set; } // Instant, Linear, Step
    public List<string> Scenarios { get; set; } = new();
    public string ReportFormat { get; set; } = "HTML";
}

/// <summary>
/// Parâmetros para auto scaling
/// </summary>
public class AutoScalerParameters : BaseNodeParameters
{
    public int MinInstances { get; set; } = 2;
    public int MaxInstances { get; set; } = 20;
    public decimal TargetCPU { get; set; } = 70m;
    public decimal TargetMemory { get; set; } = 80m;
    public int ScaleUpCooldown { get; set; } = 180;
    public int ScaleDownCooldown { get; set; } = 600;
}

/// <summary>
/// Parâmetros para otimizador de performance
/// </summary>
public class PerformanceOptimizerParameters : BaseNodeParameters
{
    public List<string> Targets { get; set; } = new(); // Database, API, Frontend
    public bool EnableCaching { get; set; } = true;
    public bool OptimizeQueries { get; set; } = true;
    public bool CompressResponses { get; set; } = true;
}

/// <summary>
/// Parâmetros para otimizador de cache
/// </summary>
public class CacheOptimizerParameters : BaseNodeParameters
{
    public string CacheType { get; set; } // Redis, Memcached, In-Memory
    public int TTL { get; set; } = 3600; // segundos
    public string EvictionPolicy { get; set; } = "LRU";
    public bool WarmupCache { get; set; } = false;
}

/// <summary>
/// Parâmetros para otimizador de queries
/// </summary>
public class QueryOptimizerParameters : BaseNodeParameters
{
    public string Database { get; set; }
    public bool AddIndexes { get; set; } = true;
    public bool RewriteQueries { get; set; } = true;
    public bool AnalyzeExecutionPlans { get; set; } = true;
    public decimal SlowQueryThreshold { get; set; } = 1000m; // ms
}

/// <summary>
/// Parâmetros para load balancer
/// </summary>
public class LoadBalancerParameters : BaseNodeParameters
{
    public string Algorithm { get; set; } // RoundRobin, LeastConnections, IPHash
    public List<string> Backends { get; set; } = new();
    public bool EnableHealthCheck { get; set; } = true;
    public bool StickySession { get; set; } = false;
}

// ==================== GOVERNANÇA & SUPERVISÃO ====================

/// <summary>
/// Parâmetros para aprovação humana
/// </summary>
public class HumanApprovalParameters : BaseNodeParameters
{
    public List<string> Approvers { get; set; } = new();
    public int Timeout { get; set; } = 86400; // 24 horas em segundos
    public bool RequireAllApprovals { get; set; } = false;
    public string NotificationChannel { get; set; } // Email, Slack, Teams
    public bool EscalateOnTimeout { get; set; } = true;
}

/// <summary>
/// Parâmetros para aprovação de produção
/// </summary>
public class ProductionApprovalParameters : BaseNodeParameters
{
    public List<string> Approvers { get; set; } = new();
    public bool RequireChangeTicket { get; set; } = true;
    public string ChangeTicketId { get; set; }
    public bool RequireSecurityReview { get; set; } = false;
    public Dictionary<string, string> Metadata { get; set; } = new();
}

/// <summary>
/// Parâmetros para auditor de decisões
/// </summary>
public class DecisionAuditorParameters : BaseNodeParameters
{
    public string LogLevel { get; set; } = "Info";
    public bool CaptureInputs { get; set; } = true;
    public bool CaptureOutputs { get; set; } = true;
    public bool IncludeContext { get; set; } = true;
    public int RetentionDays { get; set; } = 90;
}

/// <summary>
/// Parâmetros para log de auditoria
/// </summary>
public class AuditLoggerParameters : BaseNodeParameters
{
    public string Destination { get; set; } // Database, S3, CloudWatch
    public bool EncryptLogs { get; set; } = true;
    public List<string> SensitiveFields { get; set; } = new();
    public bool EnableTampering { get; set; } = true;
}

/// <summary>
/// Parâmetros para rastreamento de mudanças
/// </summary>
public class ChangeTrackerParameters : BaseNodeParameters
{
    public string Scope { get; set; } // Code, Config, Infrastructure
    public bool TrackAuthor { get; set; } = true;
    public bool RequireJustification { get; set; } = false;
    public bool NotifyOnChange { get; set; } = true;
}

/// <summary>
/// Parâmetros para verificador de compliance
/// </summary>
public class ComplianceCheckerParameters : BaseNodeParameters
{
    public List<string> Standards { get; set; } = new(); // SOC2, GDPR, HIPAA
    public bool FailOnViolation { get; set; } = true;
    public string ReportFormat { get; set; } = "PDF";
    public List<string> ExcludeRules { get; set; } = new();
}

/// <summary>
/// Parâmetros para aplicador de políticas
/// </summary>
public class PolicyEnforcerParameters : BaseNodeParameters
{
    public List<string> Policies { get; set; } = new();
    public string EnforcementMode { get; set; } = "Strict"; // Strict, Permissive, Audit
    public bool AllowOverrides { get; set; } = false;
    public List<string> Exemptions { get; set; } = new();
}