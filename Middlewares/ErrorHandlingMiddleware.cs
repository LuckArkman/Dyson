using Microsoft.AspNetCore.Http;

namespace Middlewares;

public class ErrorHandlingMiddleware
{
    private readonly RequestDelegate _next;

    public ErrorHandlingMiddleware(RequestDelegate next) { _next = next; }

    public async Task Invoke(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            // Logar o erro real no console/arquivo
            Console.WriteLine($"[CRITICAL] {ex.Message}");

            // Se for requisição AJAX/API, retorna JSON
            if (context.Request.Path.StartsWithSegments("/api"))
            {
                context.Response.ContentType = "application/json";
                context.Response.StatusCode = 500;
                await context.Response.WriteAsync( "Erro interno no servidor Dyson.");
            }
            else 
            {
                // Se for página, redireciona para página de erro amigável
                context.Response.Redirect("/Home/Error");
            }
        }
    }
}