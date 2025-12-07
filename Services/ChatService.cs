using System.Collections.Concurrent;
using System.Net.WebSockets;
using Interfaces;
using Records;
using Data;
using Dtos;
using Microsoft.Extensions.Configuration; // Necessário para NodeState

namespace Services;

public class ChatService : IChatService
{
    // Dicionário thread-safe: Key = NodeId (Guid), Value = NodeClient
    private readonly IConfiguration _configuration;
    private readonly ConcurrentDictionary<Guid, NodeClient> _connectedNodes = new();
    private Queue<NodeClient> _levelOrderQueue = new Queue<NodeClient>();
    public event EventHandler<NodeClient>? BlockAdded;
    private readonly RewardContractService _contractService;
    private readonly NodeState _nodeState; // NOVO: Armazena o estado do nó local
    private Action<object?, NodeClient> _blockAdded;
    private Action<object?, NodeClient> _blockAdded1;

    // Construtor modificado para injetar NodeState
    public ChatService(
        NodeState nodeState,
        IRepositorio<User> repositorio,
        IConfiguration configuration,
        RewardContractService contractService)
    {
        _nodeState = nodeState; // Armazena o NodeState injetado
        _configuration = configuration;
        _contractService = contractService;
        Console.WriteLine("═══════════════════════════════════════════════");
        Console.WriteLine("║  [ChatService] Serviço inicializado         ║");
        Console.WriteLine("═══════════════════════════════════════════════");
        
    }

    /// <summary>
    /// Adiciona um novo nó ao chat.
    /// Nota: O NodeClient já deve ter sido criado com o WebSocket.
    /// </summary>
    public Task AddNode(Guid nodeId, WebSocket webSocket)
    {
        if (webSocket == null)
        {
            throw new ArgumentNullException(nameof(webSocket), "WebSocket não pode ser nulo.");
        }
        // Verifica se já existe uma conexão para este NodeId
        if (_connectedNodes.ContainsKey(nodeId))
        {
            Console.WriteLine($"[ChatService] ⚠️ AVISO: Nó {nodeId} já está conectado. Removendo conexão anterior...");
            RemoveNode(nodeId.ToString(), "Nova conexão estabelecida").GetAwaiter().GetResult();
        }

        // Nota: O NodeClient será criado no Program.cs e gerenciará sua própria escuta
        // Aqui apenas registramos que este NodeId está ativo
        Console.WriteLine($"[ChatService] ✓ Nó {nodeId} registrado no serviço de chat.");
        Console.WriteLine($"[ChatService] 📊 Total de nós conectados: {_connectedNodes.Count + 1}");

        return Task.CompletedTask;
    }

    /// <summary>
    /// Adiciona um NodeClient completo ao serviço.
    /// </summary>
    public async Task<bool> AddNodeClient(NodeClient nodeClient)
    {
        if (nodeClient == null)
        {
            throw new ArgumentNullException(nameof(nodeClient));
        }
        if (_connectedNodes.TryAdd(nodeClient.id, nodeClient))
        {
            Console.WriteLine($"[ChatService] ✓ nodeClient.user {nodeClient.user == null} adicionado com sucesso.");
            Console.WriteLine($"[ChatService] ✓ NodeClient {nodeClient._session.UserId} adicionado com sucesso.");
            Console.WriteLine($"[ChatService] 📊 Total de nós conectados: {_connectedNodes.Count}");
            return await Task.FromResult(true);
        }

        Console.WriteLine($"[ChatService] ⚠️ NodeClient {nodeClient.id} já estava conectado. Substituindo...");
        _connectedNodes[nodeClient.id] = nodeClient;
        return await Task.FromResult(true);
    }

    /// <summary>
    /// Remove um nó do chat e fecha sua conexão WebSocket.
    /// </summary>
    public async Task<bool> RemoveNode(string chatId, string reason = "Conexão encerrada pelo servidor.")
    {
        if (string.IsNullOrWhiteSpace(chatId))
        {
            Console.WriteLine("[ChatService] ❌ ERRO: ChatId inválido ou vazio.");
            return false;
        }

        if (!Guid.TryParse(chatId, out var nodeId))
        {
            Console.WriteLine($"[ChatService] ❌ ERRO: ChatId '{chatId}' não é um GUID válido.");
            return false;
        }

        if (_connectedNodes.TryRemove(nodeId, out var nodeClient))
        {
            try
            {
                Console.WriteLine($"[ChatService] 🔌 Removendo nó {nodeId}. Motivo: {reason}");

                // Para a escuta de mensagens
                nodeClient.StopListening();

                // Fecha o WebSocket se ainda estiver aberto
                if (nodeClient._webSocket?.State == WebSocketState.Open)
                {
                    await nodeClient._webSocket.CloseAsync(
                        WebSocketCloseStatus.NormalClosure,
                        reason,
                        CancellationToken.None
                    );
                }

                // Descarta o NodeClient
                nodeClient.Dispose();

                Console.WriteLine($"[ChatService] ✓ Nó {nodeId} removido com sucesso.");
                Console.WriteLine($"[ChatService] 📊 Total de nós conectados: {_connectedNodes.Count}");
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ChatService] ❌ ERRO ao remover nó {nodeId}: {ex.Message}");
                return false;
            }
        }

        Console.WriteLine($"[ChatService] ⚠️ Tentativa de remover nó {nodeId} que não estava conectado.");
        return false;
    }

    /// <summary>
    /// Retorna o número de nós atualmente conectados.
    /// </summary>
    public int ConnectedNodesCount => _connectedNodes.Count;

    /// <summary>
    /// Envia uma mensagem HelloRequest para um nó e aguarda a resposta (PongResponse).
    /// </summary>
    public async Task<string> GenerateMessage(HelloRequest input)
    {
        if (_connectedNodes.IsEmpty)
        {
            Console.WriteLine("[ChatService] Nenhum nó conectado para enviar mensagem.");
            return "ERROR: Nenhum nó conectado para enviar mensagem."; 
        }

        var nodes = _connectedNodes.Values.ToList();
        var index = Random.Shared.Next(nodes.Count);
        var nodeClient = nodes[index];
    
        Console.WriteLine($"[ChatService] Enviando HelloRequest para o nó {nodeClient.id}...");

        try
        {
            // O SendRequestGenerateAsync envia a requisição pelo WebSocket existente e aguarda a resposta.
            var response = await nodeClient.SendRequestGenerateAsync<PongResponse>(input, CancellationToken.None);
            if(input.CorrelationId == response.CorrelationId ) Rewards(nodeClient);

            return response.content; 
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ChatService] ERRO ao receber resposta do nó {nodeClient.id}: {ex.Message}");
            return $"ERROR: Falha ao receber resposta do nó {nodeClient.id}: {ex.Message}";
        }
    }

    private void Rewards(NodeClient nodeClient)
    {
        _levelOrderQueue.Enqueue(nodeClient);
        BlockAdded?.Invoke(this, nodeClient);
    }

    /// <summary>
    /// Obtém todos os IDs dos nós conectados.
    /// </summary>
    public IEnumerable<Guid> GetConnectedNodeIds()
    {
        return _connectedNodes.Keys.ToList();
    }

    /// <summary>
    /// Verifica se um nó específico está conectado.
    /// </summary>
    public bool IsNodeConnected(Guid nodeId)
    {
        return _connectedNodes.ContainsKey(nodeId);
    }

    /// <summary>
    /// Obtém um NodeClient específico pelo ID.
    /// </summary>
    public NodeClient? GetNodeClient(Guid nodeId)
    {
        _connectedNodes.TryGetValue(nodeId, out var nodeClient);
        return nodeClient;
    }

    /// <summary>
    /// Envia uma mensagem broadcast para todos os nós conectados.
    /// </summary>
    public async Task BroadcastMessageAsync(Records._Message message)
    {
        var tasks = new List<Task>();

        foreach (var nodeClient in _connectedNodes.Values)
        {
            tasks.Add(Task.Run(async () =>
            {
                try
                {
                    await nodeClient.SendResponseAsync(message);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[ChatService] Erro ao enviar broadcast para {nodeClient.id}: {ex.Message}");
                }
            }));
        }

        await Task.WhenAll(tasks);
        Console.WriteLine($"[ChatService] 📢 Broadcast enviado para {_connectedNodes.Count} nós.");
    }

    /// <summary>
    /// Lista todos os nós conectados com suas informações.
    /// </summary>
    public void PrintConnectedNodes()
    {
        Console.WriteLine("═══════════════════════════════════════════════");
        Console.WriteLine($"║  Nós Conectados: {_connectedNodes.Count}");
        Console.WriteLine("═══════════════════════════════════════════════");
        
        foreach (var (nodeId, nodeClient) in _connectedNodes)
        {
            var wsState = nodeClient._webSocket?.State.ToString() ?? "N/A";
            Console.WriteLine($"║  • NodeId: {nodeId}");
            Console.WriteLine($"║    Estado WebSocket: {wsState}");
        }
        
        Console.WriteLine("═══════════════════════════════════════════════");
    }
}