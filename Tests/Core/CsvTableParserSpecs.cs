using System;
using Odyssey.Gameplay.Config;

internal static class CsvTableParserSpecs
{
    public static void Register()
    {
        Spec.Run("CSV 解析器读取表头值", CsvParserReadsHeaderValues);
        Spec.Run("CSV 解析器支持引号内逗号", CsvParserSupportsQuotedCommas);
        Spec.Run("CSV 解析器拒绝不一致的列数", CsvParserRejectsInconsistentColumnCount);
    }

    private static void CsvParserReadsHeaderValues()
    {
        var table = CsvTableParser.Parse("id,walkSpeed,runSpeed\nplayer,6,10");

        Spec.Equal("player", table.Rows[0]["id"], "ID 列解析错误");
        Spec.Equal("10", table.Rows[0]["runSpeed"], "runSpeed 列解析错误");
    }

    private static void CsvParserSupportsQuotedCommas()
    {
        var table = CsvTableParser.Parse("id,label\nplayer,\"Fast, agile\"");

        Spec.Equal("Fast, agile", table.Rows[0]["label"], "引号内逗号错误拆分了字段");
    }

    private static void CsvParserRejectsInconsistentColumnCount()
    {
        Spec.Throws<FormatException>(() => CsvTableParser.Parse("id,speed\nplayer"), "字段数量不一致的 CSV 行被错误接受");
    }
}
