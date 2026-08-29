using System.Text;

namespace Advertified.Commercial.Infrastructure.Inventory;

internal static class DelimitedTableParser
{
    internal static IReadOnlyList<IReadOnlyList<string>> Parse(string text)
    {
        var delimiter = DetectDelimiter(text);
        var rows = new List<IReadOnlyList<string>>();
        var row = new List<string>();
        var field = new StringBuilder();
        var quoted = false;
        for (var index = 0; index < text.Length; index++)
        {
            var character = text[index];
            if (character == '"')
            {
                if (quoted && index + 1 < text.Length && text[index + 1] == '"')
                {
                    field.Append('"');
                    index++;
                }
                else
                {
                    quoted = !quoted;
                }
            }
            else if (!quoted && character == delimiter)
            {
                AddField(row, field);
            }
            else if (!quoted && character is '\r' or '\n')
            {
                if (character == '\r' && index + 1 < text.Length && text[index + 1] == '\n')
                {
                    index++;
                }
                AddField(row, field);
                AddRow(rows, row);
                row = [];
            }
            else
            {
                field.Append(character);
            }
        }
        AddField(row, field);
        AddRow(rows, row);
        return rows;
    }

    private static char DetectDelimiter(string text)
    {
        var header = text.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
            .FirstOrDefault() ?? string.Empty;
        var options = new[] { ',', '\t', ';', '|' };
        return options.OrderByDescending(candidate => header.Count(value => value == candidate))
            .First();
    }

    private static void AddField(List<string> row, StringBuilder field)
    {
        row.Add(field.ToString().Trim());
        field.Clear();
    }

    private static void AddRow(
        List<IReadOnlyList<string>> rows,
        List<string> row)
    {
        if (row.Any(value => value.Length > 0))
        {
            rows.Add(row);
        }
    }
}
