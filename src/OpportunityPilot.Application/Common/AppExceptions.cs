namespace OpportunityPilot.Application.Common;

/// <summary>Maps to 404. Also used for records owned by someone else, so their existence is not leaked.</summary>
public sealed class NotFoundException(string message) : Exception(message);

/// <summary>Maps to 409, e.g. a stale version on an edit.</summary>
public sealed class ConflictException(string message) : Exception(message);

/// <summary>Maps to 400 with field-level errors.</summary>
public sealed class RequestValidationException(IDictionary<string, string[]> errors)
    : Exception("One or more validation errors occurred.")
{
    public IDictionary<string, string[]> Errors { get; } = errors;
}
