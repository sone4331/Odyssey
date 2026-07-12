using System;
using System.Collections.Generic;
using System.Text;

namespace Odyssey.Gameplay.Config
{
    public sealed class CsvRow
    {
        private readonly IReadOnlyDictionary<string, string> _values;

        internal CsvRow(IReadOnlyDictionary<string, string> values)
        {
            _values = values;
        }

        public string this[string column] => _values[column];
    }

    public sealed class CsvTable
    {
        internal CsvTable(IReadOnlyList<CsvRow> rows)
        {
            Rows = rows;
        }

        public IReadOnlyList<CsvRow> Rows { get; }
    }

    public static class CsvTableParser
    {
        public static CsvTable Parse(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                throw new FormatException("CSV text cannot be empty.");
            }

            var lines = text.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries);
            var headers = ParseLine(lines[0]);
            var uniqueHeaders = new HashSet<string>(StringComparer.Ordinal);
            foreach (var header in headers)
            {
                if (string.IsNullOrWhiteSpace(header) || !uniqueHeaders.Add(header))
                {
                    throw new FormatException($"CSV header '{header}' is empty or duplicated.");
                }
            }

            var rows = new List<CsvRow>();
            for (var lineIndex = 1; lineIndex < lines.Length; lineIndex++)
            {
                var fields = ParseLine(lines[lineIndex]);
                if (fields.Count != headers.Count)
                {
                    throw new FormatException($"CSV row {lineIndex + 1} has {fields.Count} fields; expected {headers.Count}.");
                }

                var values = new Dictionary<string, string>(StringComparer.Ordinal);
                for (var fieldIndex = 0; fieldIndex < fields.Count; fieldIndex++)
                {
                    values[headers[fieldIndex]] = fields[fieldIndex];
                }

                rows.Add(new CsvRow(values));
            }

            return new CsvTable(rows);
        }

        private static List<string> ParseLine(string line)
        {
            var fields = new List<string>();
            var field = new StringBuilder();
            var inQuotes = false;

            for (var index = 0; index < line.Length; index++)
            {
                var character = line[index];
                if (character == '"')
                {
                    if (inQuotes && index + 1 < line.Length && line[index + 1] == '"')
                    {
                        field.Append('"');
                        index++;
                        continue;
                    }

                    inQuotes = !inQuotes;
                    continue;
                }

                if (character == ',' && !inQuotes)
                {
                    fields.Add(field.ToString().Trim());
                    field.Clear();
                    continue;
                }

                field.Append(character);
            }

            if (inQuotes)
            {
                throw new FormatException("CSV row contains an unterminated quoted field.");
            }

            fields.Add(field.ToString().Trim());
            return fields;
        }
    }
}
