using System.Net;
using TranzaPay.Application.Common;

namespace TranzaPay.Api.Middleware;

public sealed class ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (AppException exception)
        {
            await WriteErrorAsync(context, exception.StatusCode, exception.Message);
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Unhandled request error");
            await WriteErrorAsync(context, (int)HttpStatusCode.InternalServerError, "An unexpected error occurred.");
        }
    }

    private static async Task WriteErrorAsync(HttpContext context, int statusCode, string message)
    {
        context.Response.StatusCode = statusCode;
        context.Response.ContentType = "application/json";
        await context.Response.WriteAsJsonAsync(new { error = message });
    }
}
