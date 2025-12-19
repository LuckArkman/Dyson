namespace Dtos;

// ==================== DEBUG & OBSERVABILIDADE ====================

/// <summary>
/// Parâmetros para debug automatizado
/// </summary>
public class AutoDebuggerParameters : BaseNodeParameters
{
    public List<string> Breakpoints { get; set; } = new();
    public string LogLevel { get; set; } = "Debug";
    public bool CaptureVariables { get; set; } = true;
    public bool StepThrough { get; set; } = false;
    public int MaxExecutionTime { get; set; } = 300; // segundos
}

/// <summary>
/// Parâmetros para análise de exceções
/// </summary>
public class ExceptionAnalyzerParameters : BaseNodeParameters
{
    public string ExceptionType { get; set; }
    public bool IncludeStackTrace { get; set; } = true;
    public bool GroupSimilar { get; set; } = true;
    public int TimeWindow { get; set; } = 24; // horas
}

/// <summary>
/// Parâmetros para análise de stack trace
/// </summary>
public class StackTraceAnalyzerParameters : BaseNodeParameters
{
    public int Depth { get; set; } = 10;
    public bool HighlightUserCode { get; set; } = true;
    public List<string> ExcludeNamespaces { get; set; } = new();
    public bool SuggestFixes { get; set; } = true;
}

/// <summary>
/// Parâmetros para análise de logs
/// </summary>
public class LogAnalyzerParameters : BaseNodeParameters
{
    public string LogPath { get; set; }
    public string Pattern { get; set; }
    public string LogLevel { get; set; } // Info, Warning, Error
    public int TailLines { get; set; } = 1000;
    public bool UseRegex { get; set; } = false;
}

/// <summary>
/// Parâmetros para coleta de métricas
/// </summary>
public class MetricsCollectorParameters : BaseNodeParameters
{
    public List<string> Metrics { get; set; } = new(); // CPU, Memory, Latency, Throughput
    public int IntervalSeconds { get; set; } = 60;
    public string Destination { get; set; } // Prometheus, InfluxDB, CloudWatch
    public Dictionary<string, string> Tags { get; set; } = new();
}

/// <summary>
/// Parâmetros para profiler de performance
/// </summary>
public class PerformanceProfilerParameters : BaseNodeParameters
{
    public int Duration { get; set; } = 60; // segundos
    public bool ProfileCPU { get; set; } = true;
    public bool ProfileMemory { get; set; } = true;
    public bool ProfileIO { get; set; } = false;
    public string OutputFormat { get; set; } = "FlameGraph";
}

/// <summary>
/// Parâmetros para detecção de gargalos
/// </summary>
public class BottleneckDetectorParameters : BaseNodeParameters
{
    public decimal Threshold { get; set; } = 0.8m; // 80% de utilização
    public List<string> Resources { get; set; } = new(); // CPU, Memory, Network, Disk
    public bool AutoSuggestFixes { get; set; } = true;
}

/// <summary>
/// Parâmetros para análise de incidentes
/// </summary>
public class IncidentAnalyzerParameters : BaseNodeParameters
{
    public string TimeWindow { get; set; } = "1h";
    public bool CorrelateEvents { get; set; } = true;
    public int MinSeverity { get; set; } = 3; // 1-5
    public List<string> DataSources { get; set; } = new();
}

/// <summary>
/// Parâmetros para análise de causa raiz
/// </summary>
public class RootCauseAnalysisParameters : BaseNodeParameters
{
    public string CorrelationId { get; set; }
    public bool UseAI { get; set; } = true;
    public int AnalysisDepth { get; set; } = 3;
    public List<string> IncludeSystems { get; set; } = new();
}

// ==================== SEGURANÇA ====================

/// <summary>
/// Parâmetros para escaneamento de vulnerabilidades
/// </summary>
public class VulnerabilityScannerParameters : BaseNodeParameters
{
    public string Scanner { get; set; } // OWASP ZAP, Snyk, Trivy
    public string Severity { get; set; } = "Medium"; // Low, Medium, High, Critical
    public bool IncludeDependencies { get; set; } = true;
    public bool FailOnCritical { get; set; } = true;
}

/// <summary>
/// Parâmetros para detecção de CVEs
/// </summary>
public class CVEDetectorParameters : BaseNodeParameters
{
    public string Database { get; set; } = "NVD"; // NVD, OSV, GitHub
    public int MaxAge { get; set; } = 365; // dias
    public List<string> ExcludeCVEs { get; set; } = new();
    public bool AutoUpdate { get; set; } = true;
}

/// <summary>
/// Parâmetros para auditoria de dependências
/// </summary>
public class DependencyAuditorParameters : BaseNodeParameters
{
    public string PackageManager { get; set; }
    public bool CheckLicenses { get; set; } = true;
    public List<string> AllowedLicenses { get; set; } = new();
    public bool BlockRestrictiveLicenses { get; set; } = true;
}

/// <summary>
/// Parâmetros para análise de autenticação
/// </summary>
public class AuthAnalyzerParameters : BaseNodeParameters
{
    public string AuthMethod { get; set; } // OAuth, JWT, API Key
    public bool CheckTokenExpiration { get; set; } = true;
    public bool ValidatePermissions { get; set; } = true;
    public List<string> RequiredScopes { get; set; } = new();
}

/// <summary>
/// Parâmetros para testes de segurança
/// </summary>
public class SecurityTesterParameters : BaseNodeParameters
{
    public string TestType { get; set; } // Penetration, Fuzzing, Static
    public List<string> Targets { get; set; } = new();
    public int MaxDuration { get; set; } = 3600; // segundos
    public bool GenerateReport { get; set; } = true;
}

/// <summary>
/// Parâmetros para SAST (Static Application Security Testing)
/// </summary>
public class SASTRunnerParameters : BaseNodeParameters
{
    public string Tool { get; set; } // SonarQube, Checkmarx, Fortify
    public List<string> Rules { get; set; } = new();
    public string ReportFormat { get; set; } = "SARIF";
    public bool BlockOnFindings { get; set; } = false;
}

/// <summary>
/// Parâmetros para DAST (Dynamic Application Security Testing)
/// </summary>
public class DASTRunnerParameters : BaseNodeParameters
{
    public string Url { get; set; }
    public string ScanProfile { get; set; } // Quick, Standard, Deep
    public bool AuthenticatedScan { get; set; } = false;
    public Dictionary<string, string> Headers { get; set; } = new();
}

/// <summary>
/// Parâmetros para teste de injeção
/// </summary>
public class InjectionTesterParameters : BaseNodeParameters
{
    public List<string> Vectors { get; set; } = new(); // SQL, NoSQL, Command, LDAP
    public bool TestBlindInjection { get; set; } = true;
    public int TimeoutMs { get; set; } = 5000;
}

/// <summary>
/// Parâmetros para teste de XSS
/// </summary>
public class XSSTesterParameters : BaseNodeParameters
{
    public List<string> Payloads { get; set; } = new();
    public bool TestReflected { get; set; } = true;
    public bool TestStored { get; set; } = true;
    public bool TestDOM { get; set; } = true;
}

/// <summary>
/// Parâmetros para correção automática de vulnerabilidades
/// </summary>
public class AutoPatcherParameters : BaseNodeParameters
{
    public string PatchStrategy { get; set; } // Conservative, Aggressive, Manual
    public bool TestAfterPatch { get; set; } = true;
    public bool CreateBackup { get; set; } = true;
    public bool RequireApproval { get; set; } = false;
}

/// <summary>
/// Parâmetros para atualização de dependências
/// </summary>
public class DependencyUpdaterParameters : BaseNodeParameters
{
    public string UpdateStrategy { get; set; } // Patch, Minor, Major
    public bool UpdateDevDependencies { get; set; } = false;
    public bool RunTests { get; set; } = true;
    public List<string> ExcludePackages { get; set; } = new();
}

// ==================== CORREÇÃO DE BUGS & REFATORAÇÃO ====================

/// <summary>
/// Parâmetros para detecção de bugs
/// </summary>
public class BugDetectorParameters : BaseNodeParameters
{
    public string Method { get; set; } // Static, Dynamic, AI
    public List<string> BugPatterns { get; set; } = new();
    public decimal Confidence { get; set; } = 0.7m;
    public bool IncludeCodeSmells { get; set; } = false;
}

/// <summary>
/// Parâmetros para corretor automático
/// </summary>
public class AutoFixerParameters : BaseNodeParameters
{
    public string Strategy { get; set; } // Conservative, Moderate, Aggressive
    public bool CreatePR { get; set; } = true;
    public bool RequireReview { get; set; } = true;
    public List<string> AllowedFixTypes { get; set; } = new();
}

/// <summary>
/// Parâmetros para validação de correção
/// </summary>
public class BugValidatorParameters : BaseNodeParameters
{
    public List<string> ValidationTests { get; set; } = new();
    public bool RunFullTestSuite { get; set; } = false;
    public bool CompareWithBaseline { get; set; } = true;
    public int RetryCount { get; set; } = 3;
}

/// <summary>
/// Parâmetros para refatoração de código
/// </summary>
public class CodeRefactorParameters : BaseNodeParameters
{
    public string RefactorType { get; set; } // ExtractMethod, RenameVariable, SimplifyExpression
    public bool PreserveBehavior { get; set; } = true;
    public bool UpdateTests { get; set; } = true;
    public string TargetPattern { get; set; }
}

/// <summary>
/// Parâmetros para simplificação de código
/// </summary>
public class SimplifyCodeParameters : BaseNodeParameters
{
    public int TargetComplexity { get; set; } = 5;
    public bool RemoveDeadCode { get; set; } = true;
    public bool SimplifyConditionals { get; set; } = true;
    public bool InlineTemporaryVariables { get; set; } = false;
}

/// <summary>
/// Parâmetros para remoção de código duplicado
/// </summary>
public class DeduplicateCodeParameters : BaseNodeParameters
{
    public decimal Similarity { get; set; } = 0.9m; // 90%
    public int MinLines { get; set; } = 5;
    public bool ExtractToMethod { get; set; } = true;
    public string Scope { get; set; } // File, Project, Solution
}

/// <summary>
/// Parâmetros para reorganização de módulos
/// </summary>
public class ModuleReorganizerParameters : BaseNodeParameters
{
    public string Pattern { get; set; } // ByFeature, ByLayer, ByDomain
    public bool PreserveDependencies { get; set; } = true;
    public bool UpdateImports { get; set; } = true;
    public List<string> ExcludeModules { get; set; } = new();
}