using System.Text;

namespace AlphaOne.Middlewares
{
    public class RequestLoggingMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<RequestLoggingMiddleware> _logger;

        public RequestLoggingMiddleware(RequestDelegate next, ILogger<RequestLoggingMiddleware> logger)
        {
            _next = next;
            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            // Apenas loga requisições POST para nossa API de chat
            if (context.Request.Method == "POST" && 
                context.Request.Path.StartsWithSegments("/api/chat"))
            {
                // Habilita o buffering para poder ler o body múltiplas vezes
                context.Request.EnableBuffering();

                // Lê o body
                var body = await new StreamReader(context.Request.Body).ReadToEndAsync();
                
                // Reseta a posição do stream para que o controller possa ler
                context.Request.Body.Position = 0;

                _logger.LogInformation("=== REQUEST LOGGING ===");
                _logger.LogInformation("Path: {Path}", context.Request.Path);
                _logger.LogInformation("Method: {Method}", context.Request.Method);
                _logger.LogInformation("Content-Type: {ContentType}", context.Request.ContentType);
                _logger.LogInformation("Content-Length: {ContentLength}", context.Request.ContentLength);
                _logger.LogInformation("Body RAW: {Body}", body);
                _logger.LogInformation("Body Length: {BodyLength}", body.Length);
                _logger.LogInformation("Headers: {@Headers}", context.Request.Headers.ToDictionary(h => h.Key, h => h.Value.ToString()));
                _logger.LogInformation("======================");
            }

            await _next(context);
        }
    }
}