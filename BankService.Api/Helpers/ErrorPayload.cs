using System.Text.Json.Serialization;

namespace BankService.Api.Helpers;

/// <summary>
/// The single error contract returned by every API failure path: unhandled
/// exceptions, authorization failures, and model validation.
/// </summary>
public sealed class ErrorPayload
{
    [JsonPropertyName("statusCode")]
    public int StatusCode { get; set; }

    [JsonPropertyName("message")]
    public string Message { get; set; } = string.Empty;

    [JsonPropertyName("errors")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IDictionary<string, string[]>? Errors { get; set; }

    [JsonPropertyName("traceId")]
    public string TraceId { get; set; } = string.Empty;
}
