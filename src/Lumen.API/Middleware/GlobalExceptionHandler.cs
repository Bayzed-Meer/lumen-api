namespace Lumen.API.Middleware;

using Lumen.Application.Common.Exceptions;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

public sealed class GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        if (exception is OperationCanceledException)
            return false;

        if (exception is not AppException appException)
        {
            logger.LogError(exception, "Unhandled exception: {Message}", exception.Message);

            httpContext.Response.StatusCode = StatusCodes.Status500InternalServerError;
            await httpContext.Response.WriteAsJsonAsync(
                new ProblemDetails
                {
                    Status = StatusCodes.Status500InternalServerError,
                    Title = "An unexpected error occurred.",
                }, cancellationToken);

            return true;
        }

        logger.LogWarning(exception, "Application exception: {Message}", exception.Message);

        httpContext.Response.StatusCode = appException.StatusCode;

        if (appException.Errors is not null)
        {
            await httpContext.Response.WriteAsJsonAsync(
                new ValidationProblemDetails(appException.Errors)
                {
                    Status = appException.StatusCode,
                }, cancellationToken);
        }
        else
        {
            await httpContext.Response.WriteAsJsonAsync(
                new ProblemDetails
                {
                    Status = appException.StatusCode,
                    Detail = appException.Message,
                }, cancellationToken);
        }

        return true;
    }
}
