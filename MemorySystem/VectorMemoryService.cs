using Microsoft.Extensions.Configuration;
using Qdrant.Client;
using Qdrant.Client.Grpc;

namespace MemorySystem;

public class VectorMemoryService
{
    private readonly QdrantClient _client;
    private readonly string _collectionName = "dyson_knowledge_base";

    public VectorMemoryService(IConfiguration config)
    {
        // Conecta no container do Qdrant
        _client = new QdrantClient(host: "localhost", port: 6334); 
    }

    public async Task InitializeAsync()
    {
        // Cria a coleção se não existir (Vetores de 1536 dimensões para OpenAI Ada-002)
        var collections = await _client.ListCollectionsAsync();
        if (!collections.Contains(_collectionName))
        {
            await _client.CreateCollectionAsync(_collectionName, new VectorParams { Size = 1536, Distance = Distance.Cosine });
        }
    }

    public async Task SaveMemoryAsync(string agentId, string text, float[] vector)
    {
        var point = new PointStruct
        {
            Id = Guid.NewGuid(),
            Vectors = vector,
            Payload = { 
                ["agentId"] = agentId,
                ["content"] = text 
            }
        };

        await _client.UpsertAsync(_collectionName, new[] { point });
    }

    public async Task<string> SearchMemoryAsync(string agentId, float[] queryVector)
    {
        var results = await _client.SearchAsync(
            _collectionName, 
            queryVector, 
            limit: 3, 
            filter: QdrantFilter.Must(QdrantFilter.Match("agentId", agentId)) // Filtra apenas memórias deste agente
        );

        return string.Join("\n", results.Select(r => r.Payload["content"].StringValue));
    }
}