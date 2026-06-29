using Domain.Common;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Net;
using System.Text.Json;

namespace Eventy.WebApi.Middlewares;

public sealed class GlobalExceptionHandlingMiddleware(
    RequestDelegate next,
    ILogger<GlobalExceptionHandlingMiddleware> logger)
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            logger.LogWarning(ex, "Concurrency conflict on {Path}", context.Request.Path);
            await WriteProblemAsync(context, HttpStatusCode.Conflict,
                "concurrency.conflict",
                "The resource was modified by another request. Please retry.");
        }
        catch (DbUpdateException ex)
        {
            logger.LogError(ex, "Database update failed on {Path}", context.Request.Path);
            await WriteProblemAsync(context, HttpStatusCode.InternalServerError,
                "database.error",
                "A database error occurred.");
        }
        catch (OperationCanceledException ex) when (ex is not TaskCanceledException)
        {
            logger.LogInformation("Request cancelled on {Path}", context.Request.Path);
            context.Response.StatusCode = 499; // Client Closed Request
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unhandled exception on {Path}", context.Request.Path);
            await WriteProblemAsync(context, HttpStatusCode.InternalServerError,
                "server.error",
                "An unexpected error occurred.");
        }
    }

    private static async Task WriteProblemAsync(
        HttpContext context,
        HttpStatusCode statusCode,
        string errorCode,
        string detail)
    {
        var statusInt = (int)statusCode;

        var problem = new ProblemDetails
        {
            Status = statusInt,
            Title = statusCode.ToString(),
            Detail = detail,
            Extensions = { ["errorCode"] = errorCode }
        };

        context.Response.StatusCode = statusInt;
        context.Response.ContentType = "application/problem+json";

        await context.Response.WriteAsync(
            JsonSerializer.Serialize(problem, JsonOptions));
    }
}