namespace Dtos;

// ==================== ACESSO AO CÓDIGO-FONTE ====================

/// <summary>
/// Parâmetros para clonagem de repositório Git
/// </summary>
public class GitCloneParameters : BaseNodeParameters
{
    public string RepoUrl { get; set; }
    public string Branch { get; set; } = "main";
    public string AccessToken { get; set; }
    public string LocalPath { get; set; }
    public bool Shallow { get; set; } = false;
}

/// <summary>
/// Parâmetros para navegação entre branches
/// </summary>
public class BranchNavigatorParameters : BaseNodeParameters
{
    public string TargetBranch { get; set; }
    public bool CreateIfNotExists { get; set; } = false;
    public bool Pull { get; set; } = true;
}

/// <summary>
/// Parâmetros para leitura de código
/// </summary>
public class CodeReaderParameters : BaseNodeParameters
{
    public string FilePath { get; set; }
    public string FilePattern { get; set; } // *.cs, *.js
    public bool Recursive { get; set; } = false;
    public List<string> ExcludePaths { get; set; } = new();
}

/// <summary>
/// Parâmetros para análise estática de código
/// </summary>
public class CodeAnalyzerParameters : BaseNodeParameters
{
    public string Analyzer { get; set; } // SonarQube, ESLint, ReSharper
    public List<string> Rules { get; set; } = new();
    public string SeverityLevel { get; set; } // Info, Warning, Error
    public bool FailOnError { get; set; } = false;
}

/// <summary>
/// Parâmetros para análise de complexidade
/// </summary>
public class ComplexityAnalyzerParameters : BaseNodeParameters
{
    public int Threshold { get; set; } = 10; // Complexidade ciclomática
    public bool IncludeCognitiveComplexity { get; set; } = true;
    public string ReportFormat { get; set; } // JSON, HTML, XML
}

/// <summary>
/// Parâmetros para detecção de code smells
/// </summary>
public class CodeSmellDetectorParameters : BaseNodeParameters
{
    public List<string> Patterns { get; set; } = new(); // DuplicateCode, LongMethod, etc
    public bool AutoFix { get; set; } = false;
    public string Language { get; set; }
}

/// <summary>
/// Parâmetros para escaneamento de dependências
/// </summary>
public class DependencyScannerParameters : BaseNodeParameters
{
    public string PackageManager { get; set; } // npm, nuget, pip, maven
    public bool CheckOutdated { get; set; } = true;
    public bool CheckVulnerabilities { get; set; } = true;
    public string OutputFormat { get; set; } = "JSON";
}

/// <summary>
/// Parâmetros para mapeamento de arquitetura
/// </summary>
public class ArchitectureMapperParameters : BaseNodeParameters
{
    public int DepthLevel { get; set; } = 3;
    public bool GenerateDiagram { get; set; } = true;
    public string DiagramFormat { get; set; } = "PlantUML";
    public List<string> ExcludeNamespaces { get; set; } = new();
}

/// <summary>
/// Parâmetros para análise de acoplamento
/// </summary>
public class CouplingAnalyzerParameters : BaseNodeParameters
{
    public decimal Threshold { get; set; } = 0.3m;
    public bool AnalyzeAfferentCoupling { get; set; } = true;
    public bool AnalyzeEfferentCoupling { get; set; } = true;
    public string ReportType { get; set; } = "Detailed";
}

// ==================== TESTES & QUALIDADE ====================

/// <summary>
/// Parâmetros para execução de testes unitários
/// </summary>
public class UnitTestRunnerParameters : BaseNodeParameters
{
    public string TestSuite { get; set; }
    public string Framework { get; set; } // xUnit, NUnit, Jest, Pytest
    public decimal CoverageThreshold { get; set; } = 80m;
    public bool FailOnThresholdNotMet { get; set; } = true;
    public List<string> TestCategories { get; set; } = new();
}

/// <summary>
/// Parâmetros para geração de testes
/// </summary>
public class TestGeneratorParameters : BaseNodeParameters
{
    public string TargetFile { get; set; }
    public string Framework { get; set; }
    public bool GenerateMocks { get; set; } = true;
    public string TestNamingPattern { get; set; } = "{MethodName}_Should_{Scenario}_When_{Condition}";
    public bool UseAI { get; set; } = true;
}

/// <summary>
/// Parâmetros para testes de integração
/// </summary>
public class IntegrationTestParameters : BaseNodeParameters
{
    public List<string> Services { get; set; } = new();
    public string Environment { get; set; } = "Staging";
    public int TimeoutSeconds { get; set; } = 300;
    public bool RollbackOnFailure { get; set; } = true;
}

/// <summary>
/// Parâmetros para testes de regressão
/// </summary>
public class RegressionTestParameters : BaseNodeParameters
{
    public string BaselineVersion { get; set; }
    public List<string> TestScenarios { get; set; } = new();
    public bool CompareWithBaseline { get; set; } = true;
}

/// <summary>
/// Parâmetros para testes de contrato
/// </summary>
public class ContractTestParameters : BaseNodeParameters
{
    public string Contract { get; set; }
    public string ContractType { get; set; } // OpenAPI, GraphQL, gRPC
    public bool ValidateSchema { get; set; } = true;
    public string ProviderUrl { get; set; }
}

/// <summary>
/// Parâmetros para análise de cobertura de testes
/// </summary>
public class CodeCoverageParameters : BaseNodeParameters
{
    public decimal MinCoverage { get; set; } = 80m;
    public List<string> ExcludePaths { get; set; } = new();
    public string ReportFormat { get; set; } = "HTML";
    public bool IncludeBranchCoverage { get; set; } = true;
}

/// <summary>
/// Parâmetros para relatório de qualidade
/// </summary>
public class QualityReportParameters : BaseNodeParameters
{
    public string Format { get; set; } // JSON, HTML, PDF
    public List<string> Metrics { get; set; } = new();
    public bool SendEmail { get; set; } = false;
    public List<string> Recipients { get; set; } = new();
}

/// <summary>
/// Parâmetros para gatilho automático de testes
/// </summary>
public class TestTriggerParameters : BaseNodeParameters
{
    public string Event { get; set; } // OnCommit, OnPR, OnSchedule
    public string Condition { get; set; }
    public List<string> Branches { get; set; } = new();
}