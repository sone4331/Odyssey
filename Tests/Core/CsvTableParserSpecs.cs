using System;
using Odyssey.Gameplay.Config;

internal static class CsvTableParserSpecs
{
    public static void Register()
    {
        Spec.Run("csv_parser_reads_header_values", CsvParserReadsHeaderValues);
        Spec.Run("csv_parser_supports_quoted_commas", CsvParserSupportsQuotedCommas);
        Spec.Run("csv_parser_rejects_inconsistent_column_count", CsvParserRejectsInconsistentColumnCount);
    }

    private static void CsvParserReadsHeaderValues()
    {
        var table = CsvTableParser.Parse("id,walkSpeed,runSpeed\nplayer,6,10");

        Spec.Equal("player", table.Rows[0]["id"], "id column was parsed incorrectly");
        Spec.Equal("10", table.Rows[0]["runSpeed"], "runSpeed column was parsed incorrectly");
    }

    private static void CsvParserSupportsQuotedCommas()
    {
        var table = CsvTableParser.Parse("id,label\nplayer,\"Fast, agile\"");

        Spec.Equal("Fast, agile", table.Rows[0]["label"], "quoted comma split the field");
    }

    private static void CsvParserRejectsInconsistentColumnCount()
    {
        Spec.Throws<FormatException>(() => CsvTableParser.Parse("id,speed\nplayer"), "inconsistent CSV row was accepted");
    }
}
