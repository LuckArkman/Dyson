using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Dtos;
using Records;

namespace Services;

/// <summary>
/// Cliente híbrido que suporta comunicação via WebSocket (conexões persistentes)
/// e HTTP (requisições pontuais para nós remotos).
/// </summary>
public class NodeClient : IDisposable
{
    private readonly byte[] EncryptionKey;
    private readonly SemaphoreSlim _sendLock = new SemaphoreSlim(1, 1);
    public readonly Guid id;
    private readonly ChatService _chatService;
    public readonly WebSocket? _webSocket;
    private readonly HttpClient _httpClient;
    private readonly CancellationTokenSource _listenerCts;
    public User? user { get; set; }
    public UserSession _session { get; set; }
    private Task? _listenerTask;
    private Task? _keepAliveTask;
    private bool _disposed;

    // Armazena requisições pendentes: Key = CorrelationId, Value = TaskCompletionSource para a resposta
    private readonly ConcurrentDictionary<Guid, TaskCompletionSource<_Message>> _pendingRequests = new();

    // Opções de serialização JSON
    private readonly JsonSerializerOptions _jsonOptions;

    /// <summary>
    /// Construtor para NodeClient com WebSocket (conexão persistente).
    /// </summary>
    public NodeClient(Guid nodeId, UserSession? session, WebSocket webSocket, ChatService chatService)
    {
        _chatService = chatService;
        EncryptionKey = Encoding.UTF8.GetBytes(session.SessionToken);
        id = nodeId;
        _session = session;
        _webSocket = webSocket ?? throw new ArgumentNullException(nameof(webSocket));
        _httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
        _listenerCts = new CancellationTokenSource();

        _jsonOptions = CreateJsonOptions();
        Console.WriteLine($"[NodeClient {id}] Criado com WebSocket.");
    }

    /// <summary>
    /// Construtor para NodeClient apenas HTTP (sem WebSocket).
    /// Usado por serviços como GossipService e HealthCheckService.
    /// </summary>
    public NodeClient()
    {
        id = Guid.NewGuid();
        _webSocket = null;
        _httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
        _listenerCts = new CancellationTokenSource();

        _jsonOptions = CreateJsonOptions();
        Console.WriteLine($"[NodeClient {id}] Criado para comunicação HTTP.");
    }

    private JsonSerializerOptions CreateJsonOptions()
    {
        return new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            WriteIndented = true,
            Converters = { new JsonStringEnumConverter() }
        };
    }

    #region WebSocket Methods

    /// <summary>
    /// Inicia a escuta de mensagens do WebSocket em background.
    /// </summary>
    public async Task ListenForMessagesAsync(CancellationToken cancellationToken)
{
    if (_webSocket == null) return;

    var buffer = new byte[1024 * 16];

    try
    {
        while (_webSocket.State == WebSocketState.Open && !cancellationToken.IsCancellationRequested)
        {
            using var ms = new MemoryStream();
            WebSocketReceiveResult result;

            // 1. Coleta todos os fragmentos da mensagem binária
            do
            {
                result = await _webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), cancellationToken);

                if (result.MessageType == WebSocketMessageType.Close)
                {
                    await HandleSocketClose(result);
                    return;
                }

                ms.Write(buffer, 0, result.Count);
            } while (!result.EndOfMessage);

            // 2. Extrai os bytes e valida se não está vazio (keep-alives podem ser vazios)
            var encryptedData = ms.ToArray();
            if (encryptedData.Length == 0) continue;

            try 
            {
                // 3. Descriptografa usando a chave derivada do Token da Sessão
                byte[] key = GetEncryptionKey();
                string jsonMessage = Decrypt(encryptedData, key);

                // 4. Log e Processamento
                var logPreview = jsonMessage.Length > 100 ? jsonMessage[..100] + "..." : jsonMessage;
                Console.WriteLine($"[NodeClient {id}] 🔓 Mensagem decifrada: {logPreview}");

                await ProcessReceivedMessageAsync(jsonMessage);
            }
            catch (Exception cryptEx)
            {
                Console.WriteLine($"[NodeClient {id}] ⚠️ Falha ao descriptografar: {cryptEx.Message}");
            }
        }
    }
    catch (OperationCanceledException) { /* Silencioso */ }
    catch (Exception ex)
    {
        Console.WriteLine($"[NodeClient {id}] ERRO crítico na escuta: {ex.Message}");
    }
}

private async Task HandleSocketClose(WebSocketReceiveResult result)
{
    Console.WriteLine($"[NodeClient {id}] ✓ WebSocket fechado pelo cliente.");
    if (_webSocket.State == WebSocketState.Open || _webSocket.State == WebSocketState.CloseReceived)
    {
        await _webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Confirmando", CancellationToken.None);
    }
}

    /// <summary>
    /// Para a escuta de mensagens.
    /// </summary>
    public void StopListening()
    {
        if (_listenerTask == null)
        {
            return;
        }

        Console.WriteLine($"[NodeClient {id}] Parando escuta de mensagens...");
        
        try
        {
            _listenerCts.Cancel();
        }
        catch (ObjectDisposedException)
        {
            // CancellationTokenSource já foi disposed
        }
    }

    /// <summary>
    /// Mantém a conexão WebSocket viva enviando pings periódicos.
    /// </summary>
    private async Task KeepAliveAsync(CancellationToken cancellationToken)
    {
        try
        {
            // Aguarda um pouco antes de começar
            await Task.Delay(TimeSpan.FromSeconds(15), cancellationToken);

            while (_webSocket?.State == WebSocketState.Open && !cancellationToken.IsCancellationRequested)
            {
                try
                {
                    if (_webSocket.State == WebSocketState.Open)
                    {
                        // Envia um frame vazio como keep-alive
                        await _webSocket.SendAsync(
                            new ArraySegment<byte>(new byte[0]),
                            WebSocketMessageType.Text,
                            true,
                            cancellationToken
                        );
                        
                        Console.WriteLine($"[NodeClient {id}] 💓 Keep-alive enviado.");
                    }
                }
                catch (WebSocketException wsEx)
                {
                    Console.WriteLine($"[NodeClient {id}] ⚠️ Erro no keep-alive (WebSocket): {wsEx.Message}");
                    break;
                }
                catch (OperationCanceledException)
                {
                    // Operação cancelada, sair do loop
                    break;
                }

                // Aguarda 30 segundos antes do próximo ping
                await Task.Delay(TimeSpan.FromSeconds(30), cancellationToken);
            }

            Console.WriteLine($"[NodeClient {id}] Keep-alive finalizado.");
        }
        catch (OperationCanceledException)
        {
            Console.WriteLine($"[NodeClient {id}] Keep-alive cancelado.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[NodeClient {id}] Erro no keep-alive: {ex.Message}");
        }
    }

    /// <summary>
    /// Processa uma mensagem JSON recebida do WebSocket.
    /// </summary>
    private async Task ProcessReceivedMessageAsync(string jsonMessage)
    {
        try
        {
            var message = JsonSerializer.Deserialize<_Message>(jsonMessage, _jsonOptions);

            if (message == null)
            {
                Console.WriteLine($"[NodeClient {id}] ⚠️ Mensagem desserializada é null.");
                return;
            }

            Console.WriteLine($"[NodeClient {id}] Processando mensagem do tipo: {message.GetType().Name} (CorrelationId: {message.CorrelationId})");

            // Verifica se é uma resposta a uma requisição pendente
            if (_pendingRequests.TryRemove(message.CorrelationId, out var tcs))
            {
                Console.WriteLine($"[NodeClient {id}] ✓ Resposta recebida para requisição {message.CorrelationId}");
                tcs.SetResult(message);
                return;
            }

            // Trata mensagens não solicitadas
            await HandleUnsolicitedMessageAsync(message);
        }
        catch (JsonException jsonEx)
        {
            Console.WriteLine($"[NodeClient {id}] ERRO ao desserializar mensagem: {jsonEx.Message}");
            Console.WriteLine($"[NodeClient {id}] JSON: {jsonMessage}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[NodeClient {id}] ERRO ao processar mensagem: {ex.Message}");
        }
    }

    private async Task HandleUnsolicitedMessageAsync(_Message message)
    {
        Console.WriteLine($"[NodeClient {id}] ⚠️ Mensagem não solicitada: {message.GetType().Name}");

        switch (message)
        {
            case PingRequest pingRequest:
                await HandlePingRequestAsync(pingRequest);
                break;
            
            case HelloRequest helloRequest:
                await HandleHelloRequestAsync(helloRequest);
                break;
            
            default:
                Console.WriteLine($"[NodeClient {id}] Tipo não tratado: {message.GetType().Name}");
                break;
        }
    }

    private async Task HandlePingRequestAsync(PingRequest pingRequest)
    {
        Console.WriteLine($"[NodeClient {id}] Respondendo a PingRequest...");
        var pongResponse = new PongResponse(pingRequest.CorrelationId, "pong");
        await SendResponseAsync(pongResponse);
    }

    private async Task HandleHelloRequestAsync(HelloRequest helloRequest)
    {
        Console.WriteLine($"[NodeClient {id}] Respondendo a HelloRequest: {helloRequest.Content}");
        var pongResponse = new PongResponse(helloRequest.CorrelationId, $"Echo: {helloRequest.Content}");
        await SendResponseAsync(pongResponse);
    }

    /// <summary>
    /// Envia uma requisição via WebSocket e aguarda a resposta.
    /// Usado para comunicação com nós já conectados via WebSocket.
    /// </summary>
    public async Task<TResponse> SendRequestGenerateAsync<TResponse>(_Message request, CancellationToken cancellationToken) 
        where TResponse : _Message
    {
        if (_webSocket == null)
        {
            throw new InvalidOperationException("NodeClient não possui WebSocket. Use SendRequestAsync para HTTP.");
        }

        if (request == null)
        {
            throw new ArgumentNullException(nameof(request));
        }

        if (request.CorrelationId == Guid.Empty)
        {
            request.CorrelationId = Guid.NewGuid();
        }

        var tcs = new TaskCompletionSource<_Message>();
        
        if (!_pendingRequests.TryAdd(request.CorrelationId, tcs))
        {
            throw new InvalidOperationException($"Requisição duplicada: {request.CorrelationId}");
        }

        try
        {
            Console.WriteLine($"[NodeClient {id}] Requisição enviada (CorrelationId: {request.CorrelationId})");
            
            await SendMessageAsync(request, cancellationToken);
            
            var responseTask = tcs.Task;
            var timeoutTask = Task.Delay(TimeSpan.FromSeconds(30), cancellationToken);
            
            var completedTask = await Task.WhenAny(responseTask, timeoutTask);
            
            if (completedTask == timeoutTask)
            {
                throw new TimeoutException($"Timeout aguardando resposta (CorrelationId: {request.CorrelationId})");
            }
            
            var response = await responseTask;
            
            if (response is TResponse typedResponse)
            {
                Console.WriteLine($"[NodeClient {id}] ✓ Resposta recebida (CorrelationId: {request.CorrelationId})");
                return typedResponse;
            }
            
            throw new InvalidOperationException(
                $"Tipo incorreto. Esperado: {typeof(TResponse).Name}, Recebido: {response.GetType().Name}"
            );
        }
        finally
        {
            _pendingRequests.TryRemove(request.CorrelationId, out _);
        }
    }

    /// <summary>
    /// Envia uma resposta via WebSocket (sem aguardar resposta).
    /// </summary>
    public async Task SendResponseAsync(_Message message, CancellationToken cancellationToken = default)
    {
        if (message == null)
        {
            throw new ArgumentNullException(nameof(message));
        }

        await SendMessageAsync(message, cancellationToken);
    }

    /// <summary>
    /// Envia uma mensagem pelo WebSocket.
    /// </summary>
    private async Task SendMessageAsync(_Message message, CancellationToken cancellationToken)
    {
        if (_webSocket == null || _webSocket.State != WebSocketState.Open)
        {
            throw new InvalidOperationException($"WebSocket não disponível. Estado: {_webSocket?.State}"); //
        }

        await _sendLock.WaitAsync(cancellationToken);
        try
        {
            var json = JsonSerializer.Serialize(message, message.GetType(), _jsonOptions); //
        
            // Criptografia usando a chave derivada do Token
            byte[] key = GetEncryptionKey();
            byte[] encryptedBytes = Encrypt(json, key); // Usa o seu método Encrypt já existente

            await _webSocket.SendAsync(
                new ArraySegment<byte>(encryptedBytes),
                WebSocketMessageType.Binary, // Alterado para Binary por ser dado cifrado
                endOfMessage: true,
                cancellationToken
            );

            Console.WriteLine($"[NodeClient {id}] ✓ Enviado Cifrado (CorrelationId: {message.CorrelationId})"); //
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[NodeClient {id}] ❌ Erro ao enviar: {ex.Message}"); //
            throw;
        }
        finally
        {
            _sendLock.Release();
        }
    }
    
    private byte[] GetEncryptionKey()
    {
        // Usamos a sessão injetada no construtor
        var token = _session?.SessionToken ?? throw new InvalidOperationException("Sessão ou Token não encontrado.");
    
        using var sha256 = SHA256.Create();
        return sha256.ComputeHash(Encoding.UTF8.GetBytes(token));
    }
    
    public byte[] Encrypt(string plainText, byte[] key)
    {
        using var aes = Aes.Create();
        aes.Key = key;
        aes.GenerateIV(); // Cria um IV aleatório para esta mensagem específica
    
        using var encryptor = aes.CreateEncryptor(aes.Key, aes.IV);
        using var ms = new MemoryStream();
    
        // Escrevemos o IV no início do stream para que o destino saiba qual usar
        ms.Write(aes.IV, 0, aes.IV.Length); 

        using (var cs = new CryptoStream(ms, encryptor, CryptoStreamMode.Write))
        using (var sw = new StreamWriter(cs))
        {
            sw.Write(plainText);
        }
    
        return ms.ToArray();
    }
    
    public string Decrypt(byte[] cipherTextWithIv, byte[] key)
    {
        using var aes = Aes.Create();
        aes.Key = key;

        // O IV está nos primeiros 16 bytes (conforme seu método Encrypt gravou)
        byte[] iv = new byte[16];
        Array.Copy(cipherTextWithIv, 0, iv, 0, iv.Length);
        aes.IV = iv;

        using var decryptor = aes.CreateDecryptor(aes.Key, aes.IV);
        using var ms = new MemoryStream(cipherTextWithIv, iv.Length, cipherTextWithIv.Length - iv.Length);
        using var cs = new CryptoStream(ms, decryptor, CryptoStreamMode.Read);
        using var sr = new StreamReader(cs);
    
        return sr.ReadToEnd();
    }

    #endregion

    #region HTTP Methods

    /// <summary>
    /// Envia uma requisição HTTP para um nó remoto e aguarda a resposta.
    /// Usado por GossipService e HealthCheckService.
    /// </summary>
    public async Task<TResponse> SendRequestAsync<TResponse>(
        string peerAddress, 
        _Message request, 
        CancellationToken cancellationToken) 
        where TResponse : _Message
    {
        if (string.IsNullOrWhiteSpace(peerAddress))
        {
            throw new ArgumentException("Endereço do peer é obrigatório.", nameof(peerAddress));
        }

        if (request == null)
        {
            throw new ArgumentNullException(nameof(request));
        }

        if (request.CorrelationId == Guid.Empty)
        {
            request.CorrelationId = Guid.NewGuid();
        }

        try
        {
            // Determina o endpoint baseado no tipo da mensagem
            var endpoint = GetEndpointForMessage(request);
            var url = $"{peerAddress.TrimEnd('/')}/{endpoint}";

            Console.WriteLine($"[NodeClient HTTP] Enviando {request.GetType().Name} para {url}");

            // Serializa a requisição
            var json = JsonSerializer.Serialize(request, request.GetType(), _jsonOptions);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            // Envia a requisição HTTP POST
            var response = await _httpClient.PostAsync(url, content, cancellationToken);
            response.EnsureSuccessStatusCode();

            // Desserializa a resposta
            var responseJson = await response.Content.ReadAsStringAsync(cancellationToken);
            var responseMessage = JsonSerializer.Deserialize<TResponse>(responseJson, _jsonOptions);

            if (responseMessage == null)
            {
                throw new InvalidOperationException("Resposta HTTP é null.");
            }

            Console.WriteLine($"[NodeClient HTTP] ✓ Resposta recebida de {url}");
            
            return responseMessage;
        }
        catch (HttpRequestException httpEx)
        {
            Console.WriteLine($"[NodeClient HTTP] ❌ Erro HTTP: {httpEx.Message}");
            throw;
        }
        catch (TaskCanceledException)
        {
            Console.WriteLine($"[NodeClient HTTP] ❌ Timeout ao comunicar com {peerAddress}");
            throw new TimeoutException($"Timeout ao comunicar com {peerAddress}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[NodeClient HTTP] ❌ Erro: {ex.Message}");
            throw;
        }
    }

    /// <summary>
    /// Mapeia o tipo de mensagem para o endpoint HTTP correspondente.
    /// </summary>
    private string GetEndpointForMessage(_Message message)
    {
        return message switch
        {
            PingRequest => "api/health/ping",
            GossipSyncRequest => "api/gossip/sync",
            HelloRequest => "api/chat/respond",
            _ => throw new NotSupportedException($"Tipo de mensagem não suportado: {message.GetType().Name}")
        };
    }

    #endregion

    #region Dispose

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        Console.WriteLine($"[NodeClient {id}] Disposing...");

        StopListening();
        
        // Cancela todas as requisições pendentes
        foreach (var tcs in _pendingRequests.Values)
        {
            tcs.TrySetCanceled();
        }
        _pendingRequests.Clear();
        _chatService.RemoveNode(id.ToString());

        // Aguarda as tasks finalizarem (com timeout)
        try
        {
            var tasks = new List<Task>();
            if (_listenerTask != null) tasks.Add(_listenerTask);
            if (_keepAliveTask != null) tasks.Add(_keepAliveTask);
            
            if (tasks.Any())
            {
                Task.WaitAll(tasks.ToArray(), TimeSpan.FromSeconds(5));
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[NodeClient {id}] Aviso ao aguardar tasks: {ex.Message}");
        }

        try
        {
            _listenerCts?.Dispose();
        }
        catch (ObjectDisposedException)
        {
            // Já foi disposed
        }

        try
        {
            _webSocket?.Dispose();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[NodeClient {id}] Erro ao disposed WebSocket: {ex.Message}");
        }

        _httpClient?.Dispose();

        _disposed = true;
        Console.WriteLine($"[NodeClient {id}] ✓ Disposed.");
    }

    #endregion
}