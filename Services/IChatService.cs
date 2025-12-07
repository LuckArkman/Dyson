using System.Net.WebSockets;
using Records;
using Services;

namespace Interfaces;

public interface IChatService
{
    /// <summary>
    /// Adiciona um novo nó ao chat, gerando um ChatId único para a conexão.
    /// </summary>
    /// <param name="nodeId">O ID do nó obtido do JWT (Subject).</param>
    /// <param name="webSocket">A conexão WebSocket ativa.</param>
    /// <returns>O ChatId único gerado para esta sessão.</returns>
    Task AddNode(Guid nodeId, WebSocket webSocket);

    /// <summary>
    /// Remove um nó do chat e fecha sua conexão WebSocket.
    /// </summary>
    /// <param name="chatId">O ChatId único da sessão a ser removida.</param>
    /// <param name="reason">O motivo do fechamento (opcional).</param>
    /// <returns>True se o nó foi removido, false caso contrário.</returns>
    Task<bool> RemoveNode(string chatId, string reason = "Conexão encerrada pelo servidor.");
    
    // Propriedade para acessar o número atual de nós conectados (opcional, mas útil para debug/monitoramento)
    int ConnectedNodesCount { get; }
    Task<string> GenerateMessage(HelloRequest input);
}