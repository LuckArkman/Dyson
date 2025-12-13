using Dtos;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Driver;

namespace Core;

public static class DataSeeder
{
    public static async Task SeedAsync(WebApplication app)
    {
        using var scope = app.Services.CreateScope();
        var mongo = scope.ServiceProvider.GetRequiredService<IMongoClient>();
        var db = mongo.GetDatabase("n8n_clone_db");
        var agents = db.GetCollection<SmartAgent>("smart_agents");

        // Verifica se já tem dados
        if (await agents.CountDocumentsAsync(_ => true) > 0) return;

        // Cria um Agente de Exemplo (Template)
        var demoAgent = new SmartAgent
        {
            Name = "Agente de Boas Vindas",
            Description = "Um exemplo simples que faz uma requisição HTTP.",
            Category = "Demo",
            IsPublic = true,
            price = 0,
            userId = "system",
            Workflow = new WorkflowData 
            {
                Nodes = new List<WorkflowNode>
                {
                    // ✅ CORRIGIDO: Usar Dictionary ao invés de JsonObject
                    new WorkflowNode 
                    { 
                        Id = "1", 
                        Name = "Start", 
                        Type = "n8n-nodes-base.webhook", 
                        Position = new Position { X = 100, Y = 200 }, 
                        Parameters = new Dictionary<string, object>
                        {
                            { "path", "/webhook/demo" },
                            { "method", "POST" }
                        }
                    },
                    new WorkflowNode 
                    { 
                        Id = "2", 
                        Name = "Get Bitcoin Price", 
                        Type = "MyClone.HttpRequest", 
                        Position = new Position { X = 400, Y = 200 }, 
                        Parameters = new Dictionary<string, object>
                        {
                            { "url", "https://api.coindesk.com/v1/bpi/currentprice.json" },
                            { "method", "GET" }
                        }
                    }
                },
                Connections = new List<WorkflowConnection>
                {
                    new WorkflowConnection 
                    { 
                        SourceNodeId = "1", 
                        TargetNodeId = "2",
                        SourceOutput = "output_1",
                        TargetInput = "input_1"
                    }
                }
            }
        };

        await agents.InsertOneAsync(demoAgent);
        Console.WriteLine("--> Banco de Dados populado com Agente Demo.");
    }
}