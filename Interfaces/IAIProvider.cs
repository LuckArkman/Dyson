using Dtos;

namespace Interfaces;

/// <summary>
/// Interface para provedores de IA (OpenAI, Anthropic, Google, etc)
/// </summary>
public interface IAIProvider
{
    /// <summary>
    /// Nome do provedor (ex: "OpenAI", "Anthropic", "Google Gemini")
    /// </summary>
    string ProviderName { get; }
    
    /// <summary>
    /// Modelos disponíveis para este provedor
    /// </summary>
    List<AIModel> AvailableModels { get; }
    
    /// <summary>
    /// Executa uma completação de texto
    /// </summary>
    Task<AIResponse> CompleteAsync(AIRequest request);
    
    /// <summary>
    /// Valida se as credenciais estão configuradas
    /// </summary>
    bool IsConfigured();
}