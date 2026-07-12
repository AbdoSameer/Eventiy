using Domain.Common; 
using Microsoft.AspNetCore.Mvc;

namespace Eventy.WebApi.Extensions;

public static class ResultExtensions
{

    public static IActionResult ToActionResult(this Result result)
    {
        if (result.IsSuccess)
            return new OkResult();

        var errorType = result.Errors.MaxBy(e => e.Type)?.Type ?? ErrorType.Failure;

        return errorType switch
        {
            ErrorType.Validation => new BadRequestObjectResult(new ProblemDetails
            {
                Title = "Validation Error",
                Detail = string.Join("; ", result.Errors.Select(e => e.Message)),
                Status = StatusCodes.Status400BadRequest,
                Extensions = { ["errors"] = result.Errors.Select(e => new { e.Code, e.Message, e.Type }) }
            }),

            ErrorType.NotFound => new NotFoundObjectResult(new ProblemDetails
            {
                Title = "Not Found",
                Detail = string.Join("; ", result.Errors.Select(e => e.Message)),
                Status = StatusCodes.Status404NotFound,
                Extensions = { ["errors"] = result.Errors.Select(e => new { e.Code, e.Message, e.Type }) }
            }),

            ErrorType.Conflict => new ConflictObjectResult(new ProblemDetails
            {
                Title = "Conflict",
                Detail = string.Join("; ", result.Errors.Select(e => e.Message)),
                Status = StatusCodes.Status409Conflict,
                Extensions = { ["errors"] = result.Errors.Select(e => new { e.Code, e.Message, e.Type }) }
            }),

            ErrorType.Unauthorized => new ObjectResult(new ProblemDetails
            {
                Title = "Unauthorized",
                Detail = string.Join("; ", result.Errors.Select(e => e.Message)),
                Status = StatusCodes.Status401Unauthorized,
                Extensions = { ["errors"] = result.Errors.Select(e => new { e.Code, e.Message, e.Type }) }
            })
            { StatusCode = StatusCodes.Status401Unauthorized },

            _ => new ObjectResult(new ProblemDetails
            {
                Title = "Internal Server Error",
                Detail = string.Join("; ", result.Errors.Select(e => e.Message)),
                Status = StatusCodes.Status500InternalServerError,
                Extensions = { ["errors"] = result.Errors.Select(e => new { e.Code, e.Message, e.Type }) }
            })
            { StatusCode = StatusCodes.Status500InternalServerError }
        };
    }

    public static IActionResult ToActionResult<T>(this Result<T> result)
    {
        if (result.IsSuccess)
            return new OkObjectResult(result.Value);

        return ((Result)result).ToActionResult();
    }

    public static IActionResult ToCreatedResult<T>(
        this Result<T> result,
        string routeName,
        object routeValues)
    {
        if (result.IsSuccess)
            return new CreatedAtRouteResult(routeName, routeValues, result.Value);

        return ((Result)result).ToActionResult();
    }
}