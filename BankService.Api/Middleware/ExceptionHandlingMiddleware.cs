using System.Net;
using BankService.Api.Helpers;
using BankService.Application.Exceptions;

namespace BankService.Api.Middleware;

public class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;

    public ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An unhandled exception occurred while processing {Method} {Path}.",
                context.Request.Method, context.Request.Path);
            await HandleExceptionAsync(context, ex);
        }
    }

    private async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        var (statusCode, message) = exception switch
        {
            AuthenticationFailedException => (HttpStatusCode.Unauthorized, exception.Message),
            UnauthorizedAccessException => (HttpStatusCode.Forbidden, exception.Message),
            KeyNotFoundException => (HttpStatusCode.NotFound, "The requested resource was not found."),
            InvalidOperationException => (HttpStatusCode.BadRequest, exception.Message),
            InvalidDataException => (HttpStatusCode.BadRequest, exception.Message),
            ArgumentException => (HttpStatusCode.BadRequest, exception.Message),
            _ => (HttpStatusCode.InternalServerError, "An unexpected error occurred. Please try again later.")
        };

        if (context.Response.HasStarted)
        {
            _logger.LogWarning("The response has already started, so the error payload cannot be written.");
            return;
        }

        context.Response.Clear();
        context.Response.ContentType = "application/json";
        context.Response.StatusCode = (int)statusCode;

        var payload = new ErrorPayload
        {
            StatusCode = (int)statusCode,
            Message = message,
            TraceId = context.TraceIdentifier
        };

        await context.Response.WriteAsJsonAsync(payload);
    }
}
