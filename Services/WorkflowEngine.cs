using Dtos;
using Interfaces;
using System.Text.Json;
using System.Text.Json.Nodes;
using Jint;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Services;

public class WorkflowEngine : IWorkflowEngine
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<WorkflowEngine> _logger;
    private readonly IConfiguration _configuration; // Para ler API Keys

    public WorkflowEngine(IHttpClientFactory httpClientFactory, ILogger<WorkflowEngine> logger, IConfiguration configuration)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
        _configuration = configuration;
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
                else if (currentNode.Type.Contains("OpenAI", StringComparison.OrdinalIgnoreCase))
                {
                    currentData = await ExecuteOpenAINode(currentNode, logs);
                }
                else if (currentNode.Type.Contains("code", StringComparison.OrdinalIgnoreCase))
                {
                    currentData = ExecuteCodeNode(currentNode, currentData, logs);
                }
                else if (currentNode.Type.Contains("if", StringComparison.OrdinalIgnoreCase))
                {
                    bool condition = ExecuteIfNode(currentNode, currentData, logs);
                    currentNode = FindNextNode(agent, currentNode.Id, condition ? 0 : 1);
                    continue; 
                }

                // Vai para o próximo nó (Saída padrão 0)
                currentNode = FindNextNode(agent, currentNode.Id, 0);
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
                    node.Parameters = JsonNode.Parse(jsonStr);
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

    // ... (Manter ExecuteCodeNode e ExecuteIfNode da resposta anterior) ...
    // ... (Manter ExecuteOpenAINode, mas usando _configuration["OpenAI:ApiKey"]) ...
    
    // Pequeno ajuste no FindNextNode para ser seguro
    private WorkflowNode? FindNextNode(SmartAgent agent, string currentId, int outputIndex)
    {
        var conn = agent.Workflow.Connections.FirstOrDefault(c => 
            c.SourceNodeId == currentId && 
            (c.SourceOutputIndex == outputIndex || (outputIndex == 0 && c.SourceOutputIndex == 0))); // Fallback

        if (conn == null) return null;
        return agent.Workflow.Nodes.FirstOrDefault(n => n.Id == conn.TargetNodeId);
    }
}