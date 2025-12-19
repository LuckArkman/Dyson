using Dtos;
using Interfaces;
using System.Text.Json;
using System.Text.Json.Nodes;
using Jint;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MongoDB.Bson;

namespace Services;

public class WorkflowEngine : IWorkflowEngine
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly AIProviderManager _aiProviderManager;
    private readonly ILogger<WorkflowEngine> _logger;
    private readonly IConfiguration _configuration; // Para ler API Keys

    public WorkflowEngine(
        IHttpClientFactory httpClientFactory,
        ILogger<WorkflowEngine> logger,
        IConfiguration configuration,
        AIProviderManager aiProviderManager)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
        _configuration = configuration;
        _aiProviderManager = aiProviderManager;
    }

    public async Task<string> RunWorkflowAsync(SmartAgent agent, string initialPayload = "{}")
    {
        if (agent.Workflow?.Nodes == null || !agent.Workflow.Nodes.Any())
            return "Workflow vazio.";

        var logs = new List<string>();
        logs.Add($"--> Iniciando Agente: {agent.Name}");

        // 1. Encontra o ponto de partida
        var currentNode = agent.Workflow.Nodes.FirstOrDefault(x => x.Type.Contains("webhook", StringComparison.OrdinalIgnoreCase)) 
                          ?? agent.Workflow.Nodes.First();

        // O dado corrente começa com o payload do Webhook
        string currentData = string.IsNullOrWhiteSpace(initialPayload) ? "{}" : initialPayload;
        logs.Add($"[Input Inicial] {currentData}");

        int steps = 0;
        
        while (currentNode != null && steps < 20)
        {
            steps++;
            logs.Add($"\n[Passo {steps}] Executando: {currentNode.Name}");

            try
            {
                // Substituição de Variáveis Simples (ex: {{ url }})
                // Em um sistema real, usaríamos o Jint para resolver expressões complexas aqui
                currentNode = ResolveVariables(currentNode, currentData);

                if (currentNode.Type.Contains("HttpRequest", StringComparison.OrdinalIgnoreCase))
                {
                    currentData = await ExecuteHttpNode(currentNode, currentData, logs);
                }
                else if (currentNode.Type.Contains("AI", StringComparison.OrdinalIgnoreCase) || 
                         currentNode.Type.Contains("OpenAI", StringComparison.OrdinalIgnoreCase) ||
                         currentNode.Type.Contains("Anthropic", StringComparison.OrdinalIgnoreCase) ||
                         currentNode.Type.Contains("Gemini", StringComparison.OrdinalIgnoreCase))
                {
                    currentData = await ExecuteAINode(currentNode, logs);
                }
                else if (currentNode.Type.Contains("code", StringComparison.OrdinalIgnoreCase))
                {
                    currentData = ExecuteCodeNode(currentNode, currentData, logs);
                }
                else if (currentNode.Type.Contains("if", StringComparison.OrdinalIgnoreCase))
                {
                    bool condition = ExecuteIfNode(currentNode, currentData, logs);
                    currentNode = FindNextNode(agent, currentNode.Id, condition ? "" : Guid.NewGuid().ToString());
                    continue; 
                }

                // Vai para o próximo nó (Saída padrão 0)
                currentNode = FindNextNode(agent, currentNode.Id, "");
            }
            catch (Exception ex)
            {
                logs.Add($"[ERRO] {ex.Message}");
                break;
            }
        }

        return string.Join("\n", logs);
    }

    // Helper: Tenta substituir {{propriedade}} nos parâmetros pelo valor do JSON atual
    private WorkflowNode ResolveVariables(WorkflowNode node, string jsonData)
    {
        // Implementação simplificada.
        // Se currentData for {"cidade": "Paris"} e a URL for "...?q={{cidade}}"
        // Substitui por "...?q=Paris"
        
        try 
        {
            var jsonStr = JsonSerializer.Serialize(node.Parameters);
            if (jsonStr.Contains("{{"))
            {
                var jsonObj = JsonNode.Parse(jsonData);
                if (jsonObj is JsonObject obj)
                {
                    foreach (var kvp in obj)
                    {
                        jsonStr = jsonStr.Replace($"{{{{{kvp.Key}}}}}", kvp.Value?.ToString() ?? "");
                    }
                    // Atualiza os parâmetros do nó (apenas em memória para execução)
                    var updatedParams = JsonSerializer.Deserialize<Dictionary<string, object>>(jsonStr);
                    if (updatedParams != null)
                    {
                        node.Parameters = updatedParams;
                    }
                }
            }
        }
        catch { /* Ignora falhas de replace */ }
        
        return node;
    }

    private async Task<string> ExecuteHttpNode(WorkflowNode node, string inputData, List<string> logs)
    {
        // Se o método for POST, usamos o inputData como Body
        var jsonParams = JsonSerializer.Serialize(node.Parameters);
        var p = JsonNode.Parse(jsonParams);
        
        string url = p?["url"]?.ToString();
        string method = p?["method"]?.ToString() ?? "GET";

        logs.Add($"HTTP {method} -> {url}");

        var client = _httpClientFactory.CreateClient();
        HttpResponseMessage response;

        if (method == "POST")
        {
            // Envia o dado do nó anterior como corpo
            var content = new StringContent(inputData, System.Text.Encoding.UTF8, "application/json");
            response = await client.PostAsync(url, content);
        }
        else
        {
            response = await client.GetAsync(url);
        }

        string result = await response.Content.ReadAsStringAsync();
        return result; // O resultado vira o input do próximo nó
    }

    // ============================================================
    // EXECUÇÃO DE NÓS ESPECÍFICOS
    // ============================================================

    private string ExecuteCodeNode(WorkflowNode node, string inputData, List<string> logs)
    {
        try
        {
            var jsonParams = JsonSerializer.Serialize(node.Parameters);
            var p = JsonNode.Parse(jsonParams);
            
            string code = p?["code"]?.ToString() ?? "";
            string mode = p?["mode"]?.ToString() ?? "runOnceForAllItems";

            logs.Add($"[Code] Mode: {mode}");

            // Configura engine Jint com segurança
            var engine = new Engine(cfg => cfg
                .LimitMemory(4_000_000)  // 4MB max
                .LimitRecursion(20)      // Evita stack overflow
                .TimeoutInterval(TimeSpan.FromSeconds(5)) // 5s timeout
            );

            // Injeta dados de entrada
            engine.SetValue("$input", inputData);
            
            // Injeta funções auxiliares do n8n
            engine.SetValue("$json", JsonNode.Parse(inputData));
            
            // Executa o código
            var result = engine.Evaluate($@"
                (function() {{
                    {code}
                }})()
            ");

            // Converte resultado para JSON
            string output = result?.ToString() ?? inputData;
            
            logs.Add($"[Code] Output: {output.Substring(0, Math.Min(100, output.Length))}...");
            
            return output;
        }
        catch (Jint.Runtime.JavaScriptException jsEx)
        {
            logs.Add($"[Code ERROR] JavaScript: {jsEx.Message} at line {jsEx.Location.Start.Line}");
            throw new Exception($"JavaScript Error: {jsEx.Message}");
        }
        catch (TimeoutException)
        {
            logs.Add($"[Code ERROR] Timeout excedido (5s)");
            throw new Exception("Code execution timeout (5 seconds)");
        }
        catch (Exception ex)
        {
            logs.Add($"[Code ERROR] {ex.Message}");
            throw;
        }
    }

    private bool ExecuteIfNode(WorkflowNode node, string inputData, List<string> logs)
    {
        try
        {
            var jsonParams = JsonSerializer.Serialize(node.Parameters);
            var p = JsonNode.Parse(jsonParams);
            
            string conditionType = p?["conditions"]?["combinator"]?.ToString() ?? "and";
            var conditions = p?["conditions"]?["conditions"]?.AsArray();

            if (conditions == null || conditions.Count == 0)
            {
                logs.Add("[IF] Nenhuma condição definida, retornando FALSE");
                return false;
            }

            var inputJson = JsonNode.Parse(inputData);
            bool result = conditionType == "and";

            foreach (var condition in conditions)
            {
                string leftValue = condition?["leftValue"]?.ToString() ?? "";
                string operation = condition?["operation"]?.ToString() ?? "equals";
                string rightValue = condition?["rightValue"]?.ToString() ?? "";

                // Resolve variáveis (ex: {{ $json.status }})
                if (leftValue.StartsWith("{{") && leftValue.EndsWith("}}"))
                {
                    string path = leftValue.Trim('{', '}', ' ').Replace("$json.", "");
                    leftValue = inputJson?[path]?.ToString() ?? "";
                }

                bool conditionResult = operation switch
                {
                    "equals" => leftValue == rightValue,
                    "notEquals" => leftValue != rightValue,
                    "contains" => leftValue.Contains(rightValue),
                    "notContains" => !leftValue.Contains(rightValue),
                    "startsWith" => leftValue.StartsWith(rightValue),
                    "endsWith" => leftValue.EndsWith(rightValue),
                    "larger" => double.TryParse(leftValue, out var l1) && double.TryParse(rightValue, out var r1) && l1 > r1,
                    "largerEqual" => double.TryParse(leftValue, out var l2) && double.TryParse(rightValue, out var r2) && l2 >= r2,
                    "smaller" => double.TryParse(leftValue, out var l3) && double.TryParse(rightValue, out var r3) && l3 < r3,
                    "smallerEqual" => double.TryParse(leftValue, out var l4) && double.TryParse(rightValue, out var r4) && l4 <= r4,
                    _ => false
                };

                logs.Add($"[IF] {leftValue} {operation} {rightValue} = {conditionResult}");

                if (conditionType == "and")
                {
                    result = result && conditionResult;
                }
                else
                {
                    result = result || conditionResult;
                }
            }

            logs.Add($"[IF] Resultado final: {result}");
            return result;
        }
        catch (Exception ex)
        {
            logs.Add($"[IF ERROR] {ex.Message}");
            return false;
        }
    }

    private async Task<string> ExecuteAINode(WorkflowNode node, List<string> logs)
    {
        try
        {
            var jsonParams = JsonSerializer.Serialize(node.Parameters);
            var p = JsonNode.Parse(jsonParams);
            
            // Novo formato: "Provider:Model" (ex: "OpenAI:gpt-4", "Anthropic Claude:claude-sonnet-4")
            string providerModel = p?["providerModel"]?.ToString() ?? "OpenAI:gpt-4";
            string prompt = p?["prompt"]?.ToString() ?? "";
            double temperature = double.Parse(p?["temperature"]?.ToString() ?? "0.7");
            int maxTokens = int.Parse(p?["maxTokens"]?.ToString() ?? "1000");

            logs.Add($"[AI] Provider: {providerModel}, Prompt: {prompt.Substring(0, Math.Min(50, prompt.Length))}...");

            // Usa o AIProviderManager para executar
            var aiManager = _aiProviderManager;
            
            if (aiManager == null)
            {
                throw new Exception("AIProviderManager não está registrado no DI");
            }

            var aiResponse = await aiManager.ExecuteAsync(
                providerModel, 
                prompt, 
                temperature, 
                maxTokens
            );

            logs.Add($"[AI] ✅ {aiResponse.Provider} | Tokens: {aiResponse.TokensUsed} | Custo: ${aiResponse.Cost:F4}");
            logs.Add($"[AI] Response: {aiResponse.Content.Substring(0, Math.Min(100, aiResponse.Content.Length))}...");

            // Retorna no formato esperado
            return JsonSerializer.Serialize(new 
            { 
                response = aiResponse.Content,
                provider = aiResponse.Provider,
                model = aiResponse.Model,
                tokensUsed = aiResponse.TokensUsed,
                cost = aiResponse.Cost
            });
        }
        catch (Exception ex)
        {
            logs.Add($"[AI ERROR] {ex.Message}");
            throw;
        }
    }

    // ============================================================
    // NAVEGAÇÃO NO WORKFLOW
    // ============================================================
    
    private WorkflowNode? FindNextNode(SmartAgent agent, string currentId, string outputIndex)
    {
        var conn = agent.Workflow.Connections.FirstOrDefault(c => 
            c.SourceNodeId == currentId && 
            c.SourceOutput == outputIndex);

        if (conn == null) return null;
        return agent.Workflow.Nodes.FirstOrDefault(n => n.Id == conn.TargetNodeId);
    }
}