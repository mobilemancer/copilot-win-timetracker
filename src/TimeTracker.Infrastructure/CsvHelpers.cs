using System.Text;

namespace TimeTracker.Infrastructure;

internal static class CsvHelpers
{
    public static string SerializeRow(IEnumerable<string?> values)
    {
        return string.Join(
            ",",
            values.Select(Escape));
    }

    public static IReadOnlyList<string> ParseRow(string line)
    {
        var values = new List<string>();
        var builder = new StringBuilder();
        var insideQuotes = false;

        for (var index = 0; index < line.Length; index++)
        {
            var current = line[index];
            if (insideQuotes)
            {
                if (current == '"' && index + 1 < line.Length && line[index + 1] == '"')
                {
                    builder.Append('"');
                    index++;
                }
                else if (current == '"')
                {
                    insideQuotes = false;
                }
                else
                {
                    builder.Append(current);
                }
            }
            else
            {
                if (current == ',')
                {
                    values.Add(builder.ToString());
                    builder.Clear();
                }
                else if (current == '"')
                {
                    insideQuotes = true;
                }
                else
                {
                    builder.Append(current);
                }
            }
        }

        values.Add(builder.ToString());
        return values;
    }

    private static string Escape(string? value)
    {
        value ??= string.Empty;
        if (!value.Contains('"') && !value.Contains(',') && !value.Contains('\n') && !value.Contains('\r'))
        {
            return value;
        }

        return $"\"{value.Replace("\"", "\"\"", StringComparison.Ordinal)}\"";
    }
}
