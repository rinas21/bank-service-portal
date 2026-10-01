using Microsoft.AspNetCore.Mvc;

namespace BankService.Api.Helpers;

/// <summary>
/// Produces error payloads that match the shape emitted by
/// <see cref="Middleware.ExceptionHandlingMiddleware"/>, the model-state
/// response factory, and the authentication handlers, so clients only ever
/// have to parse one error format.
/// </summary>
public static class ApiErrors
{
    public static NotFoundObjectResult NotFound(string message = "The requested resource was not found.")
        => new(new ErrorPayload
        {
            StatusCode = StatusCodes.Status404NotFound,
            Message = message
        });

    public static BadRequestObjectResult BadRequest(string message)
        => new(new ErrorPayload
        {
            StatusCode = StatusCodes.Status400BadRequest,
            Message = message
        });
}
