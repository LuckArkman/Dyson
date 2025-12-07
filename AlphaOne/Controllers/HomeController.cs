using System.Diagnostics;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using AlphaOne.Models;
using AlphaOne.Services;
using Dtos;
using Microsoft.AspNetCore.Authorization;

namespace AlphaOne.Controllers;
public class HomeController : Controller
{
    private readonly ConversationService _conversationService;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly string _apiBaseUrl;
    private readonly IConfiguration _configuration;
    private readonly ILogger<HomeController> _logger;

    public HomeController(ConversationService conversationService, 
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        ILogger<HomeController> logger)
    {
        _conversationService = conversationService;
        _httpClientFactory = httpClientFactory;
        _configuration = configuration;
        _apiBaseUrl = _configuration["GenerativeAIService:ApiBaseUrl"] ?? 
                      throw new ArgumentNullException("GenerativeAIService:ApiBaseUrl not configured.");
        _logger = logger;
    }

    [HttpGet]
    public IActionResult Index()
    {
        ViewBag.ApiBaseUrl = _apiBaseUrl;
        ViewBag.BodyClass = "chat-layout";
        return View();
    }

    // --- Endpoints para Gerenciamento de Conversas (chamados via AJAX do frontend) ---
    [HttpGet("api/chat/GetConversations")]
    public async Task<IActionResult> GetConversations()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId == null) return Unauthorized();

        var conversations = await _conversationService.GetConversationsByUserIdAsync(userId);
        // Retorna apenas o ID e o Título para a barra lateral
        return Ok(conversations.Select(c => new { c.Id, c.Title }).ToList());
    }

    [HttpGet("api/chat/GetConversation/{conversationId}")]
    public async Task<IActionResult> GetConversation(string conversationId)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId == null) return Unauthorized();

        var conversation = await _conversationService.GetConversationByIdAsync(conversationId, userId);
        if (conversation == null) return NotFound();

        return Ok(conversation);
    }

    [HttpPost("api/chat/GenerateAndSave")]
    public async Task<IActionResult> GenerateAndSave([FromBody] Input request)
    {
        // Log detalhado para debug
        _logger.LogInformation("GenerateAndSave chamado");
        _logger.LogInformation("Request recebido: {@Request}", request);
        _logger.LogInformation("Text: {Text}", request?.Text);
        _logger.LogInformation("ConversationId: {ConversationId}", request?.ConversationId);
        
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId == null)
        {
            _logger.LogWarning("Usuário não autenticado");
            return Unauthorized();
        }

        if (request == null)
        {
            _logger.LogError("Request é null");
            return BadRequest(new { Error = "Request inválido" });
        }

        if (string.IsNullOrWhiteSpace(request.Text))
        {
            _logger.LogError("Text está vazio ou null");
            return BadRequest(new { Error = "Texto da mensagem não pode ser vazio" });
        }

        string userMessage = request.Text;
        string aiResponse = string.Empty;

        try
        {
            var httpClient = _httpClientFactory.CreateClient();
            var jsonContent = new StringContent(
                JsonSerializer.Serialize(request),
                Encoding.UTF8,
                "application/json"
            );

            _logger.LogInformation("Chamando API externa: {ApiUrl}", $"{_apiBaseUrl}api/Chat/Respond");
            
            // Chama a API GenerativeAI externa
            var apiResponse = await httpClient.PostAsync($"{_apiBaseUrl}api/Chat/Respond", jsonContent);
            apiResponse.EnsureSuccessStatusCode(); // Lança exceção para códigos de erro HTTP
            aiResponse = await apiResponse.Content.ReadAsStringAsync();

            _logger.LogInformation("Resposta da API recebida com sucesso");

            // Salva a conversa no MongoDB
            await _conversationService.AddMessageToConversationAsync(userId, request.ConversationId, userMessage, aiResponse);

            _logger.LogInformation("Mensagem salva no MongoDB");

            // Retorna a resposta da AI e talvez o ID da conversa (se for nova)
            return Ok(new { AiResponse = aiResponse, ConversationId = request.ConversationId });
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Erro ao chamar a API GenerativeAI: {Message}", ex.Message);
            return StatusCode(500, new { Error = $"Erro ao gerar resposta: {ex.Message}" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro inesperado em GenerateAndSave: {Message}", ex.Message);
            return StatusCode(500, new { Error = $"Erro interno: {ex.Message}" });
        }
    }
}