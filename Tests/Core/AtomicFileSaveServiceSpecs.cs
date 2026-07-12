using System;
using System.IO;
using Odyssey.Gameplay.Save;

internal static class AtomicFileSaveServiceSpecs
{
    public static void Register()
    {
        Spec.Run("atomic_save_replaces_existing_file", AtomicSaveReplacesExistingFile);
        Spec.Run("load_returns_false_for_invalid_payload", LoadReturnsFalseForInvalidPayload);
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

            Spec.Equal("42", File.ReadAllText(path), "save file did not contain the new payload");
            Spec.True(!File.Exists(path + ".tmp"), "temporary save file was left behind");
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

            Spec.True(!loaded, "invalid payload was reported as loaded");
            Spec.True(data == null, "invalid payload returned data");
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
