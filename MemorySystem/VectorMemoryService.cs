using Dtos;
using Microsoft.Extensions.Configuration;
using Qdrant.Client;
using Qdrant.Client.Grpc;

namespace MemorySystem;

/// <summary>
/// Serviço de memória vetorial usando Qdrant
/// Permite aos agentes armazenar e recuperar memórias usando embeddings
/// </summary>
public class VectorMemoryService
{
    private readonly QdrantClient _client;
    private readonly string _collectionName = "dyson_knowledge_base";

    public VectorMemoryService(IConfiguration config)
    {
        // Conecta no container do Qdrant
        var host = config["Qdrant:Host"] ?? "localhost";
        var port = int.Parse(config["Qdrant:Port"] ?? "6334");
        
        _client = new QdrantClient(host: host, port: port);
    }

    /// <summary>
    /// Inicializa a coleção no Qdrant (chame no startup)
    /// </summary>
    public async Task InitializeAsync()
    {
        try
        {
            // Verifica se a coleção já existe
            var collections = await _client.ListCollectionsAsync();
            var collectionExists = collections.Any(c => c == _collectionName);

            if (!collectionExists)
            {
                // Cria a coleção (Vetores de 1536 dimensões para OpenAI text-embedding-ada-002)
                await _client.CreateCollectionAsync(
                    collectionName: _collectionName,
                    vectorsConfig: new VectorParams 
                    { 
                        Size = 1536, 
                        Distance = Distance.Cosine 
                    }
                );
            }
        }
        catch (Exception ex)
        {
            throw new Exception($"Erro ao inicializar Qdrant: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Salva uma memória para um agente específico
    /// </summary>
    public async Task SaveMemoryAsync(string agentId, string text, float[] vector)
    {
        var pointId = Guid.NewGuid();
        
        var point = new PointStruct
        {
            Id = new PointId { Uuid = pointId.ToString() },
            Vectors = vector,
            Payload = 
            { 
                ["agentId"] = agentId,
                ["content"] = text,
                ["timestamp"] = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
            }
        };

        await _client.UpsertAsync(
            collectionName: _collectionName, 
            points: new[] { point }
        );
    }

    /// <summary>
    /// Busca memórias relevantes usando similaridade vetorial
    /// </summary>
    public async Task<string> SearchMemoryAsync(string agentId, float[] queryVector, int limit = 3)
    {
        try
        {
            // Cria filtro para buscar apenas memórias deste agente
            var filter = new Filter
            {
                Must = 
                {
                    new Condition
                    {
                        Field = new FieldCondition
                        {
                            Key = "agentId",
                            Match = new Match { Keyword = agentId }
                        }
                    }
                }
            };

            // Busca por similaridade
            var searchResult = await _client.SearchAsync(
                collectionName: _collectionName,
                vector: queryVector,
                limit: (ulong)limit,
                filter: filter
            );

            if (searchResult == null || !searchResult.Any())
            {
                return string.Empty;
            }

            // Extrai e concatena os conteúdos
            var memories = searchResult
                .Select(r => r.Payload["content"].StringValue)
                .Where(content => !string.IsNullOrEmpty(content));

            return string.Join("\n\n---\n\n", memories);
        }
        catch (Exception ex)
        {
            throw new Exception($"Erro ao buscar memórias: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Retorna todas as memórias de um agente
    /// </summary>
    public async Task<List<MemoryItem>> GetAllMemoriesAsync(string agentId)
    {
        try
        {
            var filter = new Filter
            {
                Must = 
                {
                    new Condition
                    {
                        Field = new FieldCondition
                        {
                            Key = "agentId",
                            Match = new Match { Keyword = agentId }
                        }
                    }
                }
            };

            var scrollResult = await _client.ScrollAsync(
                collectionName: _collectionName,
                filter: filter,
                limit: 100
            );

            var memories = new List<MemoryItem>();

            foreach (var point in scrollResult.Result)
            {
                memories.Add(new MemoryItem
                {
                    Content = point.Payload["content"].StringValue,
                    Timestamp = point.Payload.ContainsKey("timestamp") 
                        ? DateTimeOffset.FromUnixTimeSeconds(point.Payload["timestamp"].IntegerValue).DateTime
                        : DateTime.MinValue
                });
            }

            return memories.OrderByDescending(m => m.Timestamp).ToList();
        }
        catch (Exception ex)
        {
            throw new Exception($"Erro ao listar memórias: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Deleta todas as memórias de um agente
    /// </summary>
    public async Task DeleteAgentMemoriesAsync(string agentId)
    {
        try
        {
            var filter = new Filter
            {
                Must = 
                {
                    new Condition
                    {
                        Field = new FieldCondition
                        {
                            Key = "agentId",
                            Match = new Match { Keyword = agentId }
                        }
                    }
                }
            };

            await _client.DeleteAsync(
                collectionName: _collectionName,
                filter: filter
            );
        }
        catch (Exception ex)
        {
            throw new Exception($"Erro ao deletar memórias: {ex.Message}", ex);
        }
    }
}