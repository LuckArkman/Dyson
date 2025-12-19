using System.IdentityModel.Tokens.Jwt;
using Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using System.Net.WebSockets;
using System.Security.Claims;
using Data;
using Dtos;
using Interfaces;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Serializers;
using Repositories;

var builder = WebApplication.CreateBuilder(args);

// --- 1. Configuração dos Serviços ---
var port = args.Length > 0 && args[0] == "root" ? args.ElementAtOrDefault(1) ?? "5001" : args.ElementAtOrDefault(1) ?? "5002";
var myAddress = $"http://localhost:{port}";
var sessionConnection = builder.Configuration.GetConnectionString("SessionConnection")
                        ?? "Data Source=sessions.db"; 

var typeResolver = new PolymorphicTypeResolver();

BsonSerializer.RegisterSerializer(new GuidSerializer(GuidRepresentation.Standard));
builder.Services.Configure<MongoDbSettings>(builder.Configuration.GetSection("MongoDbSettings"));
builder.Services.AddSingleton<Microsoft.AspNetCore.Identity.IPasswordHasher<Dtos.User>, Microsoft.AspNetCore.Identity.PasswordHasher<Dtos.User>>();
builder.Services.AddSingleton(new NodeState(myAddress));
builder.Services.AddSingleton<PolymorphicTypeResolver>();
builder.Services.AddSingleton<TokenService>();
builder.Services.AddSingleton<AuthService>();
builder.Services.AddSingleton<SessionService>();
builder.Services.AddSingleton(typeResolver);
builder.Services.AddSingleton<WalletService>();
builder.Services.AddSingleton<RewardContractService>();
builder.Services.AddSingleton<NodeRegistryService>();
builder.Services.AddSingleton<JwtValidatorService>();
builder.Services.AddSingleton<ChatService>(); 
// Adiciona a interface mapeada para a mesma implementação
builder.Services.AddSingleton<IChatService, ChatService>(provider => 
    provider.GetRequiredService<ChatService>());
builder.Services.AddHostedService<RewardListner>();
builder.Services.AddSingleton(typeof(IRepositorio<>), typeof(Repositorio<>));

// --- Lógica do Akka.NET ---
builder.Services.AddHostedService<AkkaHostedService>();
builder.Services.AddSingleton<ActorSystemSingleton>();
builder.Services.AddHostedService<GossipService>();
var jwtSecretKey = builder.Configuration["Jwt:SecretKey"] ?? throw new InvalidOperationException("Jwt:SecretKey não configurada.");
var validIssuer = builder.Configuration["Jwt:Issuer"] ?? "DysonAPI";
var validAudience = builder.Configuration["Jwt:UserAudience"] ?? "DysonUsers";

var key = Encoding.UTF8.GetBytes(jwtSecretKey);
var tokenValidationKey = new SymmetricSecurityKey(key);
builder.Services.AddAuthentication(options =>
    {
        options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
    })
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = validIssuer,
            ValidAudience = validAudience,
            IssuerSigningKey = tokenValidationKey
        };
    });

builder.Services.AddAuthorization();
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();

builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo
    {
        Title = "Dyson P2P Node API",
        Version = "v1",
        Description = "API para interagir e monitorar um nó na rede distribuída Galileu."
    });
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "Dyson Node API V1");
        options.RoutePrefix = "swagger";
    });
}

app.UseAuthentication();
app.UseAuthorization();
app.UseWebSockets();
app.MapControllers();

app.MapGet("/", () => $"Galileu P2P Node (Gossip Protocol) is running at {myAddress}.");

app.MapGet("/ws", async (HttpContext context) =>
{
    if (!context.WebSockets.IsWebSocketRequest)
    {
        context.Response.StatusCode = 400;
        return;
    }

    // --- 1. Lógica de Autenticação via Cabeçalho HTTP (Handshake) ---
    string? token = null;
    string authHeaderName = "";

    // Tenta obter o token do cabeçalho de AUTENTICAÇÃO DE NÓ (X-Node-Auth)
    if (context.Request.Headers.TryGetValue("X-Node-Auth", out var nodeHeaderValues))
    {
        token = nodeHeaderValues.FirstOrDefault();
        authHeaderName = "X-Node-Auth";
    }
    
    // Tenta obter o token do cabeçalho de AUTENTICAÇÃO de USUÁRIO (X-User-Auth)
    if (string.IsNullOrEmpty(token) && context.Request.Headers.TryGetValue("X-User-Auth", out var userHeaderValues))
    {
        token = userHeaderValues.FirstOrDefault();
        authHeaderName = "X-User-Auth";
    }

    if (string.IsNullOrEmpty(token))
    {
        context.Response.StatusCode = 401; 
        Console.WriteLine($"[Server] Tentativa de conexão sem cabeçalho de autenticação (X-Node-Auth ou X-User-Auth) de {context.Connection.RemoteIpAddress}.");
        return;
    }

    Guid entityId = Guid.Empty; // Irá armazenar o NodeId ou o UserId
    string entityName;
    bool isNodeConnection = false;
    UserSession? userSession = null;

    if (authHeaderName == "X-User-Auth")
    {
        // --- 2. Lógica Específica para Autenticação de USUÁRIO (SessionService) ---
        var sessionService = context.RequestServices.GetRequiredService<SessionService>();
        
        // O token JWT do usuário é o SessionToken armazenado no DB (ou cache)
        userSession = await sessionService.GetAsync(token);

        if (userSession == null || userSession.ExpiresAtUtc < DateTime.UtcNow)
        {
            context.Response.StatusCode = 403; 
            Console.WriteLine($"[Server] Sessão de usuário inválida, expirada ou não encontrada para token em {context.Connection.RemoteIpAddress}. Conexão rejeitada.");
            // Opcional: Remover sessão expirada/inválida do banco de dados.
            if (userSession != null) await sessionService.RemoveAsync(token);
            return;
        }
        Guid.TryParse(userSession.UserId, out var id);
        entityId = id;
        entityName = $"User:{userSession.UserId}"; // Nome de exibição para logs
        Console.WriteLine($"[Server] Autenticação de USUÁRIO bem-sucedida. UserId: {userSession.UserId}.");
    }
    else // authHeaderName == "X-Node-Auth" (Lógica CORRIGIDA para Nó - JwtValidatorService)
    {
        // --- 3. Lógica CORRIGIDA para Autenticação de NÓ (JwtValidatorService) ---
        var jwtValidator = context.RequestServices.GetRequiredService<JwtValidatorService>();
        // Corrigido: Usa JwtValidatorService para validar o JWT de nó.
        var principal = jwtValidator.ValidateToken(token);

        // Se o principal for nulo ou não tiver a role 'node', rejeita
        if (principal == null || !principal.IsInRole("node"))
        {
            context.Response.StatusCode = 403; 
            Console.WriteLine($"[Server] Token JWT inválido ou sem a role 'node' para {context.Connection.RemoteIpAddress}. Conexão rejeitada.");
            return;
        }

        // Extrai o NodeId (Guid) do token JWT (Subject/NameIdentifier)
        var nodeIdClaim = principal.FindFirst(ClaimTypes.NameIdentifier)?.Value 
                          ?? principal.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
        
        // Garante que o NodeId é um Guid válido
        if (string.IsNullOrEmpty(nodeIdClaim) || !Guid.TryParse(nodeIdClaim, out var nodeId))
        {
            // Se não conseguir extrair ou parsear o ID, rejeita
            context.Response.StatusCode = 403; 
            Console.WriteLine($"[Server] JWT de Nó válido, mas sem NodeId ou ID inválido para {context.Connection.RemoteIpAddress}. Conexão rejeitada.");
            return;
        }

        entityId = nodeId;
        // O nome do nó pode ser o Subject ou o NodeId para logs
        entityName = principal?.Identity?.Name ?? $"Node:{nodeId}";
        isNodeConnection = true;
        Console.WriteLine($"[Server] Autenticação de NÓ bem-sucedida. NodeId: {nodeId}.");
    }

    // A partir daqui, entityId contém o NodeId ou UserId autenticado.

    // ✅ Aceita a conexão WebSocket (Handshake)
    var ws = await context.WebSockets.AcceptWebSocketAsync();
    Console.WriteLine($"[Server] Nova conexão de {context.Connection.RemoteIpAddress}. Autenticação via {authHeaderName} bem-sucedida para '{entityName}' (ID: {entityId}).");

    // Resolve os serviços necessários
    var chatService = context.RequestServices.GetRequiredService<IChatService>();
    
    // Cria o NodeClient que gerenciará sua própria conexão e escuta
    // Nota: Se a lógica do seu ChatService/NodeClient precisar diferenciar User de Node, 
    // você deve adaptar a criação da classe aqui (ex: criar UserClient)
    var client = new NodeClient(entityId, userSession, ws, chatService as ChatService);
    
    // Adiciona o cliente ao serviço de chat
    if (chatService is ChatService chatSvc)
    {
        // O ChatService precisa lidar com ambos (NodeClient ou um futuro UserClient)
        await chatSvc.AddNodeClient(client);
    }
    
    // Inicia a escuta de mensagens
    await client.ListenForMessagesAsync(CancellationToken.None);
    
    Console.WriteLine($"[Server] ✅ Handshake completado para ID: {entityId}. Conexão estabelecida e ativa.");
    
    // ✅ Monitoramento assíncrono da conexão (código de cleanup original mantido)
    _ = Task.Run(async () =>
    {
        // ... (código de monitoramento e cleanup original)
        // O cleanup deve usar o ID da entidade (entityId)
        
        try
        {
            Console.WriteLine($"[Server] Monitorando conexão de '{entityId}' em background...");
            
            // Aguarda até a conexão WebSocket fechar
            while (ws.State == WebSocketState.Open)
            {
                await Task.Delay(TimeSpan.FromSeconds(5));
            }
            
            Console.WriteLine($"[Server] Conexão com '{entityId}' finalizada. Estado final: {ws.State}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Server] ⚠️ Erro ao monitorar conexão '{entityId}': {ex.Message}");
        }
        finally
        {
            // Cleanup: Remove a entidade do ChatService
            try
            {
                await chatService.RemoveNode(entityId.ToString(), "Conexão finalizada");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Server] ⚠️ Erro ao remover cliente '{entityId}': {ex.Message}");
            }
            
            // Fecha e dispõe o WebSocket (código original mantido)
            if (ws.State == WebSocketState.Open || ws.State == WebSocketState.CloseReceived)
            {
                try
                {
                    using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                    await ws.CloseAsync(
                        WebSocketCloseStatus.NormalClosure, 
                        "Conexão finalizada pelo servidor", 
                        cts.Token
                    );
                }
                catch { /* Ignora erros ao fechar */ }
            }
            ws.Dispose();
            Console.WriteLine($"[Server] WebSocket '{entityId}' disposed.");
        }
    });
});

app.MapGet("/api/test-jwt", (IConfiguration config, NodeState state) =>
{
    var jwt = GenerateTestNodeJwt(config, state.Id); 
    return Results.Ok(new { 
        TestToken = jwt, 
        Expires = "30 minutos", 
        Role = "node", 
        Instructions = "Use este token no cabeçalho 'X-Node-Auth' para conectar ao WebSocket em /ws." 
    });
})
.WithName("GetTestJwt")
.WithDescription("Gera um JWT válido para testes de conexão de nó (role: 'node').");

// ==================================================================================
// --- FUNÇÃO AUXILIAR PARA GERAR O JWT DE TESTE ---
// ==================================================================================
string GenerateTestNodeJwt(IConfiguration config, string nodeId)
{
    var secretKey = config["Jwt:SecretKey"] ?? throw new InvalidOperationException("Jwt:SecretKey não configurada.");
    var issuer = config["Jwt:Issuer"] ?? "GalileuAPI";
    var audience = config["Jwt:NodeAudience"] ?? "GalileuNodes";

    var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey));
    var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

    var claims = new[]
    {
        new Claim(JwtRegisteredClaimNames.Sub, nodeId), 
        new Claim(ClaimTypes.NameIdentifier, nodeId),
        new Claim(ClaimTypes.Role, "node"), 
        new Claim(JwtRegisteredClaimNames.Aud, audience),
        new Claim(JwtRegisteredClaimNames.Iss, issuer)
    };

    var token = new JwtSecurityToken(
        issuer: issuer,
        audience: audience,
        claims: claims,
        expires: DateTime.UtcNow.AddMinutes(30),
        signingCredentials: credentials);

    return new JwtSecurityTokenHandler().WriteToken(token);
}

// --- 4. Lógica de Inicialização ATUALIZADA para Gossip ---
var nodeState = app.Services.GetRequiredService<NodeState>();
var isRoot = args.Length > 0 && args[0].Equals("root", StringComparison.OrdinalIgnoreCase);

Console.WriteLine($"Node {nodeState.Id} is running at {myAddress}");
nodeState.PrintStatus();

if (!isRoot)
{
    var bootstrapAddress = args.ElementAtOrDefault(0) ?? "http://localhost:5001";
    Console.WriteLine($"Bootstrapping into the network via: {bootstrapAddress}...");
    nodeState.MergePeers(new[] { bootstrapAddress });
}

// --- 5. Executar a Aplicação ---
app.Run();