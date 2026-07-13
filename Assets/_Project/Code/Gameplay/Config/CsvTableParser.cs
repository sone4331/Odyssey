using System;
using System.Collections.Generic;
using System.Text;

namespace Odyssey.Gameplay.Config
{
    /// <summary>
    /// 表示一行以列名索引的 CSV 数据，为导表阶段提供显式的缺列错误。
    /// 作为只读数据对象隔离文本解析结果与具体 Gameplay 配置类型。
    /// </summary>
    public sealed class CsvRow
    {
        private readonly IReadOnlyDictionary<string, string> _values;

        internal CsvRow(IReadOnlyDictionary<string, string> values)
        {
            _values = values;
        }

        public string this[string column] => _values[column];
    }

    /// <summary>
    /// 保存 CSV 表头和数据行，是解析器与类型化导入器之间的中间表示。
    /// 中间表示让通用语法解析与业务字段校验保持独立，避免解析器了解玩家或敌人规则。
    /// </summary>
    public sealed class CsvTable
    {
        internal CsvTable(IReadOnlyList<CsvRow> rows)
        {
            Rows = rows;
        }

        public IReadOnlyList<CsvRow> Rows { get; }
    }

    /// <summary>
    /// 将 RFC4180 风格文本解析为通用表结构，并处理引号、逗号和换行。
    /// 使用无状态 Parser 模式确保导表、构建验证和测试共享完全一致的语法行为。
    /// </summary>
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
