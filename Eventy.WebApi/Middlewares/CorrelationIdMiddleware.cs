namespace Eventy.WebApi.Middlewares;

public sealed class CorrelationIdMiddleware(
    RequestDelegate next,
    ILogger<CorrelationIdMiddleware> logger)
{
    private const string CorrelationIdKey = "CorrelationId";
    private const string HeaderName = "X-Correlation-Id";

    public async Task InvokeAsync(HttpContext context)
    {
        var correlationId = context.Request.Headers.TryGetValue(HeaderName, out var headerValue)
            ? headerValue.FirstOrDefault()
            : null;

        if (string.IsNullOrWhiteSpace(correlationId))
        {
            correlationId = Guid.NewGuid().ToString();
        }

        context.Items[CorrelationIdKey] = correlationId;
        context.Response.Headers.TryAdd(HeaderName, correlationId);

        logger.LogDebug("Request {Path} assigned correlation {CorrelationId}",
            context.Request.Path, correlationId);

        await next(context);
    }
}
