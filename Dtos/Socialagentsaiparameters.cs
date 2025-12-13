namespace Dtos;

// ==================== REDES SOCIAIS ====================

/// <summary>
/// Parâmetros para conector Facebook
/// </summary>
public class FacebookConnectorParameters : BaseNodeParameters
{
    public string PageId { get; set; }
    public string AccessToken { get; set; }
    public List<string> Permissions { get; set; } = new();
    public string Action { get; set; } // Post, Get, Delete
    public Dictionary<string, object> Content { get; set; } = new();
}

/// <summary>
/// Parâmetros para conector Instagram
/// </summary>
public class InstagramConnectorParameters : BaseNodeParameters
{
    public string AccountId { get; set; }
    public string AccessToken { get; set; }
    public List<string> MediaTypes { get; set; } = new(); // Photo, Video, Reel, Story
    public bool AutoHashtags { get; set; } = false;
    public List<string> Hashtags { get; set; } = new();
}

/// <summary>
/// Parâmetros para conector WhatsApp
/// </summary>
public class WhatsAppConnectorParameters : BaseNodeParameters
{
    public string PhoneNumber { get; set; }
    public string ApiKey { get; set; }
    public string WebhookUrl { get; set; }
    public bool EnableTemplates { get; set; } = true;
    public Dictionary<string, string> Templates { get; set; } = new();
}

/// <summary>
/// Parâmetros para conector Twitter/X
/// </summary>
public class TwitterConnectorParameters : BaseNodeParameters
{
    public string ApiKey { get; set; }
    public string ApiSecret { get; set; }
    public string BearerToken { get; set; }
    public string Action { get; set; } // Tweet, Reply, Retweet, Like
    public int MaxLength { get; set; } = 280;
}

/// <summary>
/// Parâmetros para bot Telegram
/// </summary>
public class TelegramBotParameters : BaseNodeParameters
{
    public string BotToken { get; set; }
    public string ChatId { get; set; }
    public List<string> Commands { get; set; } = new();
    public bool EnableInlineKeyboard { get; set; } = false;
    public Dictionary<string, string> Responses { get; set; } = new();
}

/// <summary>
/// Parâmetros para agendador de posts
/// </summary>
public class PostSchedulerParameters : BaseNodeParameters
{
    public List<string> Platforms { get; set; } = new();
    public string Schedule { get; set; } // Cron expression
    public string Content { get; set; }
    public List<string> MediaUrls { get; set; } = new();
    public Dictionary<string, object> PlatformSpecific { get; set; } = new();
}

/// <summary>
/// Parâmetros para métricas de engajamento
/// </summary>
public class EngagementMetricsParameters : BaseNodeParameters
{
    public List<string> Platforms { get; set; } = new();
    public string MetricsType { get; set; } // Likes, Shares, Comments, Views
    public string Period { get; set; } // Day, Week, Month
    public bool IncludeGrowth { get; set; } = true;
}

/// <summary>
/// Parâmetros para análise de sentimento
/// </summary>
public class SentimentAnalysisParameters : BaseNodeParameters
{
    public string Source { get; set; }
    public string Language { get; set; } = "pt";
    public string Model { get; set; } // VADER, BERT, Custom
    public bool IncludeEmotions { get; set; } = false;
    public List<string> Keywords { get; set; } = new();
}

/// <summary>
/// Parâmetros para chatbot multicanal
/// </summary>
public class ChatbotMultiParameters : BaseNodeParameters
{
    public List<string> Channels { get; set; } = new();
    public string AIModel { get; set; }
    public string KnowledgeBase { get; set; }
    public bool EnableFallback { get; set; } = true;
    public string FallbackMessage { get; set; }
}

/// <summary>
/// Parâmetros para captura de leads
/// </summary>
public class LeadCaptureParameters : BaseNodeParameters
{
    public List<string> Fields { get; set; } = new();
    public string Destination { get; set; } // CRM, Database, Spreadsheet
    public Dictionary<string, decimal> Scoring { get; set; } = new();
    public bool ValidateEmail { get; set; } = true;
}

// ==================== AGENTES INTELIGENTES ====================

/// <summary>
/// Parâmetros para agente supervisor
/// </summary>
public class SupervisorAgentParameters : BaseNodeParameters
{
    public List<string> Agents { get; set; } = new();
    public string Strategy { get; set; } // Sequential, Parallel, Adaptive
    public Dictionary<string, int> Priorities { get; set; } = new();
    public bool EnableFailover { get; set; } = true;
    public int MaxRetries { get; set; } = 3;
}

/// <summary>
/// Parâmetros para coordenador de agentes
/// </summary>
public class AgentCoordinatorParameters : BaseNodeParameters
{
    public string Protocol { get; set; } // HTTP, WebSocket, gRPC
    public string MessageFormat { get; set; } // JSON, Protobuf, MessagePack
    public string Routing { get; set; } // Direct, Broadcast, Selective
    public bool EnableQueueing { get; set; } = true;
}

/// <summary>
/// Parâmetros para agente coletor de dados
/// </summary>
public class DataCollectorAgentParameters : BaseNodeParameters
{
    public List<string> Sources { get; set; } = new();
    public string Frequency { get; set; } // Realtime, Hourly, Daily
    public string Storage { get; set; }
    public bool EnableDeduplication { get; set; } = true;
    public Dictionary<string, string> Transformations { get; set; } = new();
}

/// <summary>
/// Parâmetros para agente analítico
/// </summary>
public class AnalyticsAgentParameters : BaseNodeParameters
{
    public string DataSource { get; set; }
    public string AnalysisType { get; set; } // Descriptive, Diagnostic, Predictive, Prescriptive
    public string Reporting { get; set; }
    public bool UseML { get; set; } = false;
    public List<string> Metrics { get; set; } = new();
}

/// <summary>
/// Parâmetros para agente de decisão
/// </summary>
public class DecisionAgentParameters : BaseNodeParameters
{
    public List<string> Rules { get; set; } = new();
    public string MLModel { get; set; }
    public decimal Threshold { get; set; } = 0.7m;
    public bool RequireHumanApproval { get; set; } = false;
    public string EscalationPolicy { get; set; }
}

/// <summary>
/// Parâmetros para agente de execução
/// </summary>
public class ExecutionAgentParameters : BaseNodeParameters
{
    public List<string> Actions { get; set; } = new();
    public string Validation { get; set; }
    public bool Rollback { get; set; } = true;
    public int TimeoutSeconds { get; set; } = 300;
    public bool LogExecutions { get; set; } = true;
}

/// <summary>
/// Parâmetros para agente de comunicação
/// </summary>
public class CommunicationAgentParameters : BaseNodeParameters
{
    public List<string> Channels { get; set; } = new();
    public string Personality { get; set; } // Formal, Casual, Friendly, Professional
    public string Knowledge { get; set; }
    public bool MultiLanguage { get; set; } = false;
    public List<string> Languages { get; set; } = new();
}

// ==================== IA & LLMs ====================

/// <summary>
/// Parâmetros para OpenAI
/// </summary>
public class OpenAIParameters : BaseNodeParameters
{
    public string Model { get; set; } = "gpt-4";
    public string ApiKey { get; set; }
    public int MaxTokens { get; set; } = 1000;
    public decimal Temperature { get; set; } = 0.7m;
    public string Prompt { get; set; }
    public List<string> StopSequences { get; set; } = new();
}

/// <summary>
/// Parâmetros para Anthropic Claude
/// </summary>
public class ClaudeParameters : BaseNodeParameters
{
    public string Model { get; set; } = "claude-3-sonnet-20240229";
    public string ApiKey { get; set; }
    public int MaxTokens { get; set; } = 1000;
    public decimal Temperature { get; set; } = 1.0m;
    public string Prompt { get; set; }
    public List<string> SystemPrompts { get; set; } = new();
}

/// <summary>
/// Parâmetros para Google Gemini
/// </summary>
public class GeminiParameters : BaseNodeParameters
{
    public string Model { get; set; } = "gemini-pro";
    public string ApiKey { get; set; }
    public int MaxTokens { get; set; } = 1000;
    public string Prompt { get; set; }
    public bool MultiModal { get; set; } = false;
}

/// <summary>
/// Parâmetros para Llama Local
/// </summary>
public class LlamaLocalParameters : BaseNodeParameters
{
    public string ModelPath { get; set; }
    public string Device { get; set; } = "cpu"; // cpu, cuda, mps
    public int ContextLength { get; set; } = 2048;
    public decimal Temperature { get; set; } = 0.8m;
    public string Prompt { get; set; }
}

/// <summary>
/// Parâmetros para geração de embeddings
/// </summary>
public class EmbeddingGenParameters : BaseNodeParameters
{
    public string Model { get; set; } = "text-embedding-ada-002";
    public string Text { get; set; }
    public int Dimensions { get; set; } = 1536;
    public bool Normalize { get; set; } = true;
}

/// <summary>
/// Parâmetros para busca semântica
/// </summary>
public class SemanticSearchParameters : BaseNodeParameters
{
    public string Query { get; set; }
    public string VectorDb { get; set; } // Qdrant, Pinecone, Weaviate
    public int TopK { get; set; } = 5;
    public decimal MinScore { get; set; } = 0.7m;
    public Dictionary<string, object> Filters { get; set; } = new();
}

/// <summary>
/// Parâmetros para geração de texto
/// </summary>
public class TextGenerationParameters : BaseNodeParameters
{
    public string Prompt { get; set; }
    public string Model { get; set; }
    public int MaxLength { get; set; } = 500;
    public decimal Temperature { get; set; } = 0.7m;
    public decimal TopP { get; set; } = 0.9m;
    public int TopK { get; set; } = 50;
}

/// <summary>
/// Parâmetros para geração de imagem
/// </summary>
public class ImageGenerationParameters : BaseNodeParameters
{
    public string Prompt { get; set; }
    public string Model { get; set; } = "dall-e-3";
    public string Size { get; set; } = "1024x1024";
    public string Style { get; set; } = "vivid"; // vivid, natural
    public int Count { get; set; } = 1;
}

/// <summary>
/// Parâmetros para síntese de voz
/// </summary>
public class VoiceSynthesisParameters : BaseNodeParameters
{
    public string Text { get; set; }
    public string Voice { get; set; }
    public string Language { get; set; } = "pt-BR";
    public decimal Speed { get; set; } = 1.0m;
    public string OutputFormat { get; set; } = "mp3";
}

/// <summary>
/// Parâmetros para geração de código
/// </summary>
public class CodeGenerationParameters : BaseNodeParameters
{
    public string Language { get; set; }
    public string Requirements { get; set; }
    public bool IncludeTests { get; set; } = true;
    public bool IncludeDocumentation { get; set; } = true;
    public string Framework { get; set; }
}

// ==================== DADOS & ANALYTICS ====================

/// <summary>
/// Parâmetros para conector de API
/// </summary>
public class APIConnectorParameters : BaseNodeParameters
{
    public string Url { get; set; }
    public string Method { get; set; } = "GET";
    public Dictionary<string, string> Headers { get; set; } = new();
    public object Body { get; set; }
    public string Authentication { get; set; } // None, Basic, Bearer, OAuth
    public int TimeoutSeconds { get; set; } = 30;
}

/// <summary>
/// Parâmetros para query de banco de dados
/// </summary>
public class DatabaseQueryParameters : BaseNodeParameters
{
    public string ConnectionString { get; set; }
    public string DbType { get; set; } // SQL, MongoDB, PostgreSQL
    public string Query { get; set; }
    public Dictionary<string, object> Parameters { get; set; } = new();
    public int TimeoutSeconds { get; set; } = 30;
}

/// <summary>
/// Parâmetros para webhook listener
/// </summary>
public class WebhookListenerParameters : BaseNodeParameters
{
    public string EndpointUrl { get; set; }
    public string AuthMethod { get; set; } // None, HMAC, Bearer
    public string Secret { get; set; }
    public List<string> EventTypes { get; set; } = new();
    public bool ValidatePayload { get; set; } = true;
}