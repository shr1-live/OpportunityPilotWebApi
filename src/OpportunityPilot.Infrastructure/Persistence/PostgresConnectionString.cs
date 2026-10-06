using Npgsql;

namespace OpportunityPilot.Infrastructure.Persistence;

/// <summary>
/// Npgsql only understands key=value connection strings, but Supabase and Render hand out
/// postgres:// URIs. Accept both so a pasted dashboard value works as-is — including a raw (unencoded) password
/// with characters like # @ / ? that System.Uri cannot parse, and Supabase's "[YOUR-PASSWORD]" brackets left in.
/// </summary>
public static class PostgresConnectionString
{
    public static string Normalize(string connectionString)
    {
        var value = connectionString.Trim().Trim('"', '\'');
        var schemeEnd = value.IndexOf("://", StringComparison.Ordinal);
        if (!value.StartsWith("postgres://", StringComparison.OrdinalIgnoreCase) &&
            !value.StartsWith("postgresql://", StringComparison.OrdinalIgnoreCase))
            return value;

        var rest = value[(schemeEnd + 3)..];
        // The host never contains '@', so the last one separates the credentials — whatever the password holds.
        var at = rest.LastIndexOf('@');
        var userInfo = at >= 0 ? rest[..at] : "";
        var hostPart = at >= 0 ? rest[(at + 1)..] : rest;

        var query = "";
        var q = hostPart.IndexOf('?');
        if (q >= 0) { query = hostPart[(q + 1)..]; hostPart = hostPart[..q]; }
        var database = "";
        var slash = hostPart.IndexOf('/');
        if (slash >= 0) { database = Uri.UnescapeDataString(hostPart[(slash + 1)..]); hostPart = hostPart[..slash]; }

        var port = 5432;
        var colon = hostPart.LastIndexOf(':');
        if (colon >= 0)
        {
            if (!int.TryParse(hostPart[(colon + 1)..], out port) || port <= 0)
                throw new InvalidOperationException("Setup required: ConnectionStrings:Main has an invalid port.");
            hostPart = hostPart[..colon];
        }
        if (hostPart.Length == 0)
            throw new InvalidOperationException("Setup required: ConnectionStrings:Main has no host.");

        var builder = new NpgsqlConnectionStringBuilder
        {
            Host = hostPart,
            Port = port,
            Database = database.Length > 0 ? database : "postgres"
        };

        if (userInfo.Length > 0)
        {
            var sep = userInfo.IndexOf(':');
            builder.Username = Uri.UnescapeDataString(sep >= 0 ? userInfo[..sep] : userInfo);
            if (sep >= 0)
            {
                var password = userInfo[(sep + 1)..];
                if (password.Length > 2 && password[0] == '[' && password[^1] == ']') password = password[1..^1];
                builder.Password = Uri.UnescapeDataString(password);
            }
        }

        foreach (var pair in query.Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var kv = pair.Split('=', 2);
            if (kv.Length == 2 && kv[0].Equals("sslmode", StringComparison.OrdinalIgnoreCase) &&
                Enum.TryParse<SslMode>(kv[1].Replace("-", ""), ignoreCase: true, out var ssl))
                builder.SslMode = ssl;
        }

        return builder.ConnectionString;
    }
}
