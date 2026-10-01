using System.Text;

namespace OpportunityPilot.Application.Imports;

/// <summary>Minimal RFC 4180 reader and writer: quoted fields, doubled quotes, embedded commas and line breaks, CRLF or LF.</summary>
public static class Csv
{
    /// <summary>
    /// Parses every record. A leading UTF-8 BOM is ignored; completely empty lines are skipped.
    /// Returns false (with a reason) for an unterminated quoted field rather than guessing where it ends.
    /// </summary>
    public static bool TryParse(string text, out List<string[]> records, out string? error)
    {
        records = [];
        error = null;
        if (text.Length > 0 && text[0] == (char)0xFEFF) text = text[1..];

        var field = new StringBuilder();
        var record = new List<string>();
        var inQuotes = false;
        var fieldWasQuoted = false;
        var i = 0;

        void EndField()
        {
            record.Add(fieldWasQuoted ? field.ToString() : field.ToString().Trim());
            field.Clear();
            fieldWasQuoted = false;
        }

        void EndRecord(List<string[]> into)
        {
            EndField();
            if (!(record.Count == 1 && record[0].Length == 0)) into.Add(record.ToArray());
            record.Clear();
        }

        while (i < text.Length)
        {
            var c = text[i];
            if (inQuotes)
            {
                if (c == '"')
                {
                    if (i + 1 < text.Length && text[i + 1] == '"')
                    {
                        field.Append('"');
                        i += 2;
                        continue;
                    }
                    inQuotes = false;
                    i++;
                    continue;
                }
                field.Append(c);
                i++;
                continue;
            }

            switch (c)
            {
                case '"' when field.ToString().Trim().Length == 0:
                    field.Clear();
                    inQuotes = true;
                    fieldWasQuoted = true;
                    i++;
                    break;
                case ',':
                    EndField();
                    i++;
                    break;
                case '\r':
                    EndRecord(records);
                    i += i + 1 < text.Length && text[i + 1] == '\n' ? 2 : 1;
                    break;
                case '\n':
                    EndRecord(records);
                    i++;
                    break;
                case ' ' or '\t' when fieldWasQuoted:
                    // Padding between a closing quote and the next comma.
                    i++;
                    break;
                default:
                    // Text after a closing quote ("a"b) is kept rather than rejected; spreadsheets do the same.
                    field.Append(c);
                    i++;
                    break;
            }
        }

        if (inQuotes)
        {
            error = "A quoted field is not closed (missing \").";
            return false;
        }
        if (field.Length > 0 || record.Count > 0 || fieldWasQuoted) EndRecord(records);
        return true;
    }

    /// <summary>
    /// Writes one cell. Values a spreadsheet would run as a formula (starting with = + - @, tab or CR) get a
    /// leading apostrophe, and every cell is quoted.
    /// </summary>
    public static string Cell(string? value)
    {
        value ??= string.Empty;
        if (value.Length > 0 && value[0] is '=' or '+' or '-' or '@' or '\t' or '\r') value = "'" + value;
        return "\"" + value.Replace("\"", "\"\"") + "\"";
    }

    public static string Row(IEnumerable<string?> values) => string.Join(',', values.Select(Cell)) + "\r\n";
}
