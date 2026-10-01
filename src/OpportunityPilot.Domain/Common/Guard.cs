namespace OpportunityPilot.Domain.Common;

internal static class Guard
{
    public static string Required(string? value, int max, string name)
    {
        if (string.IsNullOrWhiteSpace(value)) throw new ArgumentException($"{name} is required.", name);
        var trimmed = value.Trim();
        if (trimmed.Length > max) throw new ArgumentException($"{name} must be at most {max} characters.", name);
        return trimmed;
    }

    public static string? Optional(string? value, int max, string name)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var trimmed = value.Trim();
        if (trimmed.Length > max) throw new ArgumentException($"{name} must be at most {max} characters.", name);
        return trimmed;
    }

    /// <summary>For system-written summaries (events, reasons): cut rather than fail.</summary>
    public static string Truncate(string? value, int max)
    {
        var trimmed = value?.Trim() ?? string.Empty;
        return trimmed.Length <= max ? trimmed : trimmed[..(max - 1)] + "…";
    }

    public static string? TruncateOptional(string? value, int max) =>
        string.IsNullOrWhiteSpace(value) ? null : Truncate(value, max);
}
