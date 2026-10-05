using System.Text.RegularExpressions;

namespace OpportunityPilot.Api.Hosting;

/// <summary>
/// Which browser origins may call the API: an exact list plus narrow patterns for hosts that mint a new
/// address per deployment (Vercel gives every build its own <c>&lt;project&gt;-&lt;id&gt;-&lt;team&gt;.vercel.app</c>).
/// In a pattern, <c>*</c> stands for one hostname fragment of letters, digits and hyphens — never a dot,
/// so a pattern cannot be satisfied by a look-alike such as <c>….vercel.app.evil.com</c>. Only https patterns count.
/// </summary>
public static class CorsOrigins
{
    public static Func<string, bool> Matcher(IEnumerable<string> exact, IEnumerable<string> patterns)
    {
        var exactSet = new HashSet<string>(exact.Select(o => o.TrimEnd('/')), StringComparer.OrdinalIgnoreCase);
        var regexes = patterns
            .Where(p => p.StartsWith("https://", StringComparison.OrdinalIgnoreCase) && p.Contains('*'))
            .Select(p => new Regex(
                "^" + Regex.Escape(p.TrimEnd('/')).Replace(@"\*", "[a-z0-9-]{1,63}") + "$",
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(50)))
            .ToList();
        return origin => exactSet.Contains(origin) || regexes.Any(r => r.IsMatch(origin));
    }
}
