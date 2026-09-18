using Microsoft.AspNetCore.Http;

namespace AnyDeskMonitor.Web.Services;

public class DomainEnforcementMiddleware
{
    private readonly RequestDelegate _next;
    private const string AllowedDomain = "anyrotina.calculorotina.com";

    public DomainEnforcementMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var host = context.Request.Host.Host.ToLowerInvariant();

        // Se o pedido for feito diretamente por localhost / 127.0.0.1
        if (host == "localhost" || host == "127.0.0.1" || host == "::1")
        {
            // Se o utilizador configurou um cabeçalho X-Forwarded-Host com o domínio oficial, permitir
            var forwardedHost = context.Request.Headers["X-Forwarded-Host"].ToString();
            if (!string.IsNullOrEmpty(forwardedHost) && forwardedHost.Contains(AllowedDomain, StringComparison.OrdinalIgnoreCase))
            {
                await _next(context);
                return;
            }

            // Caso contrário, impedir o acesso direto no localhost e redirecionar para o domínio oficial
            var isDevelopment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") == "Development";
            
            // Em produção ou quando explicitamente ativado, redirecionar/bloquear acesso direto no localhost
            if (!isDevelopment || context.Request.Query.ContainsKey("enforce_domain"))
            {
                context.Response.Redirect($"https://{AllowedDomain}{context.Request.Path}{context.Request.QueryString}");
                return;
            }
        }

        await _next(context);
    }
}
