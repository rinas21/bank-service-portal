using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace BankService.Api.Helpers;

/// <summary>
/// Stamps the current request's trace id onto <see cref="ErrorPayload"/> results
/// produced by controllers, keeping every error response self-describing.
/// </summary>
public class ErrorTraceIdFilter : IResultFilter
{
    public void OnResultExecuting(ResultExecutingContext context)
    {
        if (context.Result is ObjectResult { Value: ErrorPayload payload })
        {
            payload.TraceId = context.HttpContext.TraceIdentifier;
        }
    }

    public void OnResultExecuted(ResultExecutedContext context)
    {
    }
}
