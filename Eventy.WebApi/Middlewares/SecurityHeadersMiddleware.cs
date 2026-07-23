namespace Eventy.WebApi.Middlewares;

public sealed class SecurityHeadersMiddleware(RequestDelegate next)
{
    private static readonly IReadOnlyDictionary<string, string> Headers = new Dictionary<string, string>
    {
        ["X-Content-Type-Options"] = "nosniff",
        ["X-Frame-Options"] = "DENY",
        ["Referrer-Policy"] = "strict-origin-when-cross-origin",
        ["Content-Security-Policy"] = "default-src 'self'; img-src 'self' data:; script-src 'self'; style-src 'self' 'unsafe-inline'",
    };

    public async Task InvokeAsync(HttpContext context)
    {
        foreach (var (name, value) in Headers)
            context.Response.Headers.TryAdd(name, value);

        await next(context);
    }
}
