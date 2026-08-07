namespace MoodPickup.Api.Infrastructure;

public class ApiProblemException(
    int status,
    string type,
    string title,
    string? code = null,
    string? detail = null,
    IReadOnlyDictionary<string, object?>? extensions = null) : Exception(detail ?? title)
{
    public int Status { get; } = status;

    public string Type { get; } = type;

    public string Title { get; } = title;

    public string? Code { get; } = code;

    public string? ProblemDetail { get; } = detail;

    public IReadOnlyDictionary<string, object?> Extensions { get; } =
        extensions ?? new Dictionary<string, object?>();
}

public sealed class ApiValidationException(
    IReadOnlyDictionary<string, string[]> errors)
    : ApiProblemException(
        StatusCodes.Status400BadRequest,
        "validation_error",
        "Request validation failed")
{
    public IReadOnlyDictionary<string, string[]> Errors { get; } = errors;
}
