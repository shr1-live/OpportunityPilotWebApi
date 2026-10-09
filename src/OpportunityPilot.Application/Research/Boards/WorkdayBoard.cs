using System.Text.RegularExpressions;

namespace OpportunityPilot.Application.Research.Boards;

/// <summary>
/// A company's public Workday careers site: <c>https://{tenant}.{instance}.myworkdayjobs.com/[{locale}/]{site}</c>.
/// Stored on the source as <c>{tenant}.{instance}/{site}</c> (e.g. <c>nvidia.wd5/NVIDIAExternalCareerSite</c>). The site
/// name keeps its case because Workday's paths are case-sensitive. Only the documented public host is accepted, and
/// sign-in, account and job-detail paths are refused, so a source can never point the fetcher anywhere else.
/// </summary>
public sealed partial record WorkdayBoard(string Tenant, string Instance, string Site)
{
    public const string HostSuffix = ".myworkdayjobs.com";

    /// <summary>Path segments that are pages of a careers site, not the site itself.</summary>
    private static readonly HashSet<string> NotSites = new(StringComparer.OrdinalIgnoreCase)
    {
        "login", "logout", "userhome", "job", "jobs", "details", "apply", "wday", "cxs", "signin", "createaccount", "introduceyourself"
    };

    public string Host => $"{Tenant}.{Instance}{HostSuffix}";

    /// <summary>The careers page people open (also the apply root).</summary>
    public string SiteUrl => $"https://{Host}/{Site}";

    /// <summary>The value stored in <c>Source.Url</c>.</summary>
    public override string ToString() => $"{Tenant}.{Instance}/{Site}";

    /// <summary>A careers URL (with or without a locale segment) or the stored <c>tenant.wdN/site</c> form; null otherwise.</summary>
    public static WorkdayBoard? Parse(string? input)
    {
        var value = input?.Trim();
        if (string.IsNullOrEmpty(value) || value.Length > 300) return null;

        if (Stored().Match(value) is { Success: true } stored)
            return Valid(stored.Groups["tenant"].Value, stored.Groups["instance"].Value, stored.Groups["site"].Value);

        var candidate = value.Contains("://", StringComparison.Ordinal) ? value : "https://" + value;
        if (!Uri.TryCreate(candidate, UriKind.Absolute, out var uri)) return null;
        if (uri.Scheme != Uri.UriSchemeHttps) return null;
        if (!string.IsNullOrEmpty(uri.UserInfo) || !uri.IsDefaultPort) return null;
        var host = uri.Host.ToLowerInvariant();
        if (!host.EndsWith(HostSuffix, StringComparison.Ordinal)) return null;
        var labels = host[..^HostSuffix.Length].Split('.');
        if (labels.Length != 2) return null;

        var segments = uri.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length > 0 && Locale().IsMatch(segments[0])) segments = segments[1..];
        if (segments.Length == 0) return null;
        // A job link (/site/job/...) still names the site; a sign-in or account page does not.
        if (segments.Length > 1 && !segments[1].Equals("job", StringComparison.OrdinalIgnoreCase) &&
            !segments[1].Equals("details", StringComparison.OrdinalIgnoreCase)) return null;
        return Valid(labels[0], labels[1], Uri.UnescapeDataString(segments[0]));
    }

    private static WorkdayBoard? Valid(string tenant, string instance, string site)
    {
        tenant = tenant.ToLowerInvariant();
        instance = instance.ToLowerInvariant();
        if (!TenantPattern().IsMatch(tenant) || !InstancePattern().IsMatch(instance) || !SitePattern().IsMatch(site) || NotSites.Contains(site)) return null;
        return new WorkdayBoard(tenant, instance, site);
    }

    [GeneratedRegex(@"^(?<tenant>[A-Za-z0-9-]{1,63})\.(?<instance>wd\d{1,3})/(?<site>[A-Za-z0-9_-]{1,100})$")]
    private static partial Regex Stored();

    [GeneratedRegex(@"^[a-z0-9](?:[a-z0-9-]{0,61}[a-z0-9])?$")]
    private static partial Regex TenantPattern();

    [GeneratedRegex(@"^wd\d{1,3}$")]
    private static partial Regex InstancePattern();

    [GeneratedRegex(@"^[A-Za-z0-9_-]{1,100}$")]
    private static partial Regex SitePattern();

    [GeneratedRegex(@"^[a-z]{2}-[A-Za-z]{2}$")]
    private static partial Regex Locale();
}
