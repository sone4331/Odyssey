using System;
using System.IO;
using Odyssey.Gameplay.Save;

internal static class AtomicFileSaveServiceSpecs
{
    public static void Register()
    {
        Spec.Run("原子存档替换现有文件", AtomicSaveReplacesExistingFile);
        Spec.Run("无效载荷读取返回失败", LoadReturnsFalseForInvalidPayload);
    }

    private static void AtomicSaveReplacesExistingFile()
    {
        var directory = CreateDirectory();
        try
        {
            var path = Path.Combine(directory, "save.txt");
            File.WriteAllText(path, "old");
            var service = new AtomicFileSaveService<TestData>(path, new TestCodec());

            service.Save(new TestData { Value = 42 });

            Spec.Equal("42", File.ReadAllText(path), "存档文件未包含新载荷");
            Spec.True(!File.Exists(path + ".tmp"), "临时存档文件未被清理");
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    private static void LoadReturnsFalseForInvalidPayload()
    {
        var directory = CreateDirectory();
        try
        {
            var path = Path.Combine(directory, "save.txt");
            File.WriteAllText(path, "not-an-integer");
            var service = new AtomicFileSaveService<TestData>(path, new TestCodec());

            var loaded = service.TryLoad(out var data);

            Spec.True(!loaded, "无效载荷被错误报告为读取成功");
            Spec.True(data == null, "无效载荷返回了数据");
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    private static string CreateDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), "odyssey-spec-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    private sealed class TestData
    {
        public int Value { get; set; }
    }

    private sealed class TestCodec : ISaveCodec<TestData>
    {
        public string Encode(TestData data) => data.Value.ToString();

        public TestData Decode(string payload)
        {
            return new TestData { Value = int.Parse(payload) };
        }
    }
}
