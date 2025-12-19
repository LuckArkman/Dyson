using Dtos;
using MongoDB.Driver;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Services;

/// <summary>
/// Serviço executor de workflows de Smart Agents - Versão Simplificada
/// </summary>
public class WorkflowExecutorService
{
    private readonly IMongoCollection<SmartAgent> _agentsCollection;
    private readonly IMongoCollection<AgentExecutionStatus> _executionStatusCollection;

    public WorkflowExecutorService(
        IMongoClient mongoClient,
        string databaseName = "n8n_clone_db")
    {
        var database = mongoClient.GetDatabase(databaseName);
        _agentsCollection = database.GetCollection<SmartAgent>("smart_agents");
        _executionStatusCollection = database.GetCollection<AgentExecutionStatus>("agent_execution_status");
    }

    /// <summary>
    /// Executa um workflow completo
    /// </summary>
    public async Task<WorkflowExecutionResult> ExecuteWorkflowAsync(string agentId)
    {
        var result = new WorkflowExecutionResult { AgentId = agentId };
        var startTime = DateTime.UtcNow;

        try
        {
            // 1. Carregar o agente
            var agent = await _agentsCollection
                .Find(a => a.id == agentId)
                .FirstOrDefaultAsync();

            if (agent == null)
                throw new Exception($"Agente {agentId} não encontrado");

            result.AgentName = agent.Name;
            Console.WriteLine($"🚀 Executando workflow: {agent.Name}");

            // 2. Validar workflow
            var validationErrors = ValidateWorkflow(agent.Workflow);
            if (validationErrors.Any())
            {
                result.Success = false;
                result.Errors = validationErrors;
                return result;
            }

            // 3. Criar status de execução
            var executionStatus = new AgentExecutionStatus
            {
                AgentId = agentId,
                AgentName = agent.Name,
                Status = "Running",
                StartedAt = startTime
            };
            await _executionStatusCollection.InsertOneAsync(executionStatus);

            // 4. Ordenar nós por dependências
            var orderedNodes = OrderNodesByDependencies(
                agent.Workflow.Nodes, 
                agent.Workflow.Connections
            );

            // 5. Executar cada nó em sequência
            var context = new WorkflowContext();
            
            foreach (var node in orderedNodes)
            {
                Console.WriteLine($"  ⚙️ Executando nó: {node.Name} (ID: {node.Id})");
                
                var nodeResult = await ExecuteNodeAsync(node, context);
                result.NodeResults.Add(nodeResult);

                if (!nodeResult.Success)
                {
                    Console.WriteLine($"  ❌ Nó falhou: {nodeResult.ErrorMessage}");
                    result.Success = false;
                    result.Errors.Add($"Nó {node.Name} falhou: {nodeResult.ErrorMessage}");
                    break;
                }

                // Armazenar output para próximos nós
                context.SetNodeOutput(node.Id, nodeResult.Output);
                Console.WriteLine($"  ✅ Nó completado com sucesso");
            }

            // 6. Atualizar status final
            executionStatus.Status = result.Success ? "Completed" : "Failed";
            executionStatus.FinishedAt = DateTime.UtcNow;
            executionStatus.ExecutionLogs = string.Join("\n", result.NodeResults.Select(r => 
                $"{r.NodeName}: {(r.Success ? "✅" : "❌")}"));

            await _executionStatusCollection.ReplaceOneAsync(
                s => s.Id == executionStatus.Id,
                executionStatus
            );

            result.Duration = DateTime.UtcNow - startTime;
            Console.WriteLine($"🏁 Workflow finalizado em {result.Duration.TotalSeconds:F2}s");

            return result;
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.Errors.Add($"Erro fatal: {ex.Message}");
            result.Duration = DateTime.UtcNow - startTime;
            return result;
        }
    }

    /// <summary>
    /// Executa um nó individual
    /// </summary>
    private async Task<NodeExecutionResult> ExecuteNodeAsync(WorkflowNode node, WorkflowContext context)
    {
        var result = new NodeExecutionResult
        {
            NodeId = node.Id,
            NodeName = node.Name,
            StartTime = DateTime.UtcNow
        };

        try
        {
            // Executar lógica específica do nó
            result.Output = await ExecuteNodeLogic(node, context);
            result.Success = true;
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.ErrorMessage = ex.Message;
            result.StackTrace = ex.StackTrace;
        }
        finally
        {
            result.EndTime = DateTime.UtcNow;
            result.Duration = result.EndTime - result.StartTime;
        }

        return result;
    }

    /// <summary>
    /// Executa a lógica específica de cada tipo de nó
    /// </summary>
    private async Task<object> ExecuteNodeLogic(WorkflowNode node, WorkflowContext context)
    {
        // Obter parâmetros do nó
        var parameters = node.Parameters ?? new Dictionary<string, object>();

        return node.Name switch
        {
            // Web3 & Blockchain
            "BlockchainConnect" => await ExecuteBlockchainConnect(parameters),
            "CryptoPriceData" => await ExecuteCryptoPriceData(parameters),
            
            // Code & Development
            "GitClone" => await ExecuteGitClone(parameters),
            "CodeAnalyzer" => await ExecuteCodeAnalyzer(parameters),
            
            // Tests
            "UnitTestRunner" => await ExecuteUnitTestRunner(parameters),
            
            // Security
            "VulnerabilityScanner" => await ExecuteVulnerabilityScanner(parameters),
            
            // Deploy
            "AutoDeploy" => await ExecuteAutoDeploy(parameters, context),
            
            // IA
            "OpenAI" => await ExecuteOpenAI(parameters, context),
            "Claude" => await ExecuteClaude(parameters, context),
            
            // Default
            _ => await ExecuteGenericNode(node.Name, parameters, context)
        };
    }

    // ==================== IMPLEMENTAÇÕES DE EXEMPLO ====================

    private async Task<object> ExecuteBlockchainConnect(Dictionary<string, object> parameters)
    {
        var network = GetParameter<string>(parameters, "network", "Ethereum");
        var rpcUrl = GetParameter<string>(parameters, "rpcUrl", "");
        
        Console.WriteLine($"    🔗 Conectando em {network} via {rpcUrl}");
        await Task.Delay(500);
        
        return new
        {
            connected = true,
            network = network,
            blockNumber = 12345678
        };
    }

    private async Task<object> ExecuteCryptoPriceData(Dictionary<string, object> parameters)
    {
        var symbol = GetParameter<string>(parameters, "symbol", "BTC");
        
        Console.WriteLine($"    💰 Obtendo preço de {symbol}");
        await Task.Delay(300);
        
        return new
        {
            symbol = symbol,
            price = 42000.50m,
            timestamp = DateTime.UtcNow
        };
    }

    private async Task<object> ExecuteGitClone(Dictionary<string, object> parameters)
    {
        var repoUrl = GetParameter<string>(parameters, "repoUrl", "");
        var branch = GetParameter<string>(parameters, "branch", "main");
        
        Console.WriteLine($"    📦 Clonando {repoUrl} (branch: {branch})");
        await Task.Delay(1000);
        
        return new
        {
            cloned = true,
            repository = repoUrl,
            branch = branch,
            filesCount = 150
        };
    }

    private async Task<object> ExecuteCodeAnalyzer(Dictionary<string, object> parameters)
    {
        var analyzer = GetParameter<string>(parameters, "analyzer", "SonarQube");
        
        Console.WriteLine($"    🔍 Analisando código com {analyzer}");
        await Task.Delay(2000);
        
        return new
        {
            issuesFound = 5,
            qualityScore = 85.5m
        };
    }

    private async Task<object> ExecuteUnitTestRunner(Dictionary<string, object> parameters)
    {
        var testSuite = GetParameter<string>(parameters, "testSuite", "all");
        
        Console.WriteLine($"    🧪 Executando {testSuite}");
        await Task.Delay(3000);
        
        return new
        {
            total = 100,
            passed = 95,
            failed = 3,
            coverage = 87.5m
        };
    }

    private async Task<object> ExecuteVulnerabilityScanner(Dictionary<string, object> parameters)
    {
        var scanner = GetParameter<string>(parameters, "scanner", "OWASP");
        
        Console.WriteLine($"    🛡️ Escaneando vulnerabilidades com {scanner}");
        await Task.Delay(5000);
        
        return new
        {
            vulnerabilitiesFound = 2,
            severity = "High"
        };
    }

    private async Task<object> ExecuteAutoDeploy(Dictionary<string, object> parameters, WorkflowContext context)
    {
        var environment = GetParameter<string>(parameters, "environment", "Production");
        
        Console.WriteLine($"    🚀 Deploy para {environment}");
        await Task.Delay(10000);
        
        return new
        {
            success = true,
            environment = environment,
            version = "1.0.5",
            deployedAt = DateTime.UtcNow
        };
    }

    private async Task<object> ExecuteOpenAI(Dictionary<string, object> parameters, WorkflowContext context)
    {
        var model = GetParameter<string>(parameters, "model", "gpt-4");
        var prompt = GetParameter<string>(parameters, "prompt", "");
        
        Console.WriteLine($"    🤖 Chamando OpenAI {model}");
        await Task.Delay(2000);
        
        return new
        {
            content = "Resposta simulada do GPT-4",
            model = model,
            tokensUsed = 150
        };
    }

    private async Task<object> ExecuteClaude(Dictionary<string, object> parameters, WorkflowContext context)
    {
        var model = GetParameter<string>(parameters, "model", "claude-3-sonnet");
        
        Console.WriteLine($"    🧠 Chamando Claude {model}");
        await Task.Delay(1500);
        
        return new
        {
            content = "Resposta simulada do Claude",
            model = model,
            tokensUsed = 120
        };
    }

    private async Task<object> ExecuteGenericNode(string nodeType, Dictionary<string, object> parameters, WorkflowContext context)
    {
        Console.WriteLine($"    ⚙️ Executando nó genérico: {nodeType}");
        await Task.Delay(1000);
        
        return new
        {
            nodeType = nodeType,
            executed = true,
            timestamp = DateTime.UtcNow
        };
    }

    // ==================== HELPERS ====================

    private T GetParameter<T>(Dictionary<string, object> parameters, string key, T defaultValue)
    {
        if (parameters.TryGetValue(key, out var value))
        {
            try
            {
                return (T)Convert.ChangeType(value, typeof(T));
            }
            catch
            {
                return defaultValue;
            }
        }
        return defaultValue;
    }

    private List<string> ValidateWorkflow(WorkflowData workflow)
    {
        var errors = new List<string>();

        if (workflow?.Nodes == null || !workflow.Nodes.Any())
        {
            errors.Add("Workflow não contém nós");
            return errors;
        }

        foreach (var node in workflow.Nodes)
        {
            if (string.IsNullOrWhiteSpace(node.Id))
                errors.Add($"Nó sem ID");

            if (string.IsNullOrWhiteSpace(node.Name))
                errors.Add($"Nó {node.Id} sem nome");
        }

        return errors;
    }

    private List<WorkflowNode> OrderNodesByDependencies(
        List<WorkflowNode> nodes, 
        List<WorkflowConnection> connections)
    {
        var ordered = new List<WorkflowNode>();
        var visited = new HashSet<string>();

        void Visit(string nodeId)
        {
            if (visited.Contains(nodeId)) return;
            visited.Add(nodeId);

            var dependentConnections = connections?.Where(c => c.TargetNodeId == nodeId) ?? Enumerable.Empty<WorkflowConnection>();
            foreach (var conn in dependentConnections)
            {
                Visit(conn.SourceNodeId);
            }

            var node = nodes.FirstOrDefault(n => n.Id == nodeId);
            if (node != null && !ordered.Contains(node))
            {
                ordered.Add(node);
            }
        }

        foreach (var node in nodes)
        {
            Visit(node.Id);
        }

        return ordered;
    }
}

// ==================== CLASSES DE RESULTADO ====================

public class WorkflowExecutionResult
{
    public string AgentId { get; set; }
    public string AgentName { get; set; }
    public bool Success { get; set; } = true;
    public List<NodeExecutionResult> NodeResults { get; set; } = new();
    public List<string> Errors { get; set; } = new();
    public TimeSpan Duration { get; set; }
}

public class NodeExecutionResult
{
    public string NodeId { get; set; }
    public string NodeName { get; set; }
    public bool Success { get; set; }
    public object Output { get; set; }
    public string ErrorMessage { get; set; }
    public string StackTrace { get; set; }
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public TimeSpan Duration { get; set; }
}

/// <summary>
/// Contexto compartilhado entre nós durante a execução
/// </summary>
public class WorkflowContext
{
    private readonly Dictionary<string, object> _nodeOutputs = new();

    public void SetNodeOutput(string nodeId, object output)
    {
        _nodeOutputs[nodeId] = output;
    }

    public T GetNodeOutput<T>(string nodeId)
    {
        if (_nodeOutputs.TryGetValue(nodeId, out var output))
        {
            return (T)output;
        }
        throw new KeyNotFoundException($"Output do nó {nodeId} não encontrado");
    }

    public bool HasNodeOutput(string nodeId) => _nodeOutputs.ContainsKey(nodeId);
}