using System.Diagnostics;

namespace TranzaPay.Api.Middleware;

public sealed class RequestLoggingMiddleware(RequestDelegate next, ILogger<RequestLoggingMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        var start = Stopwatch.StartNew();
        var correlationId = context.Request.Headers["X-Correlation-Id"].FirstOrDefault() ?? context.TraceIdentifier;

        context.TraceIdentifier = correlationId;
        context.Response.OnStarting(() =>
        {
            if (!context.Response.Headers.ContainsKey("X-Correlation-Id"))
            {
                context.Response.Headers["X-Correlation-Id"] = correlationId;
            }

            return Task.CompletedTask;
        });

        await next(context);

        start.Stop();
        logger.LogInformation(
            "HTTP {Method} {Path} responded {StatusCode} in {ElapsedMs}ms with correlation {CorrelationId}",
            context.Request.Method,
            context.Request.Path.Value ?? "/",
            context.Response.StatusCode,
            start.ElapsedMilliseconds,
            correlationId);
    }
}
