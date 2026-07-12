using System;
using System.IO;

namespace Odyssey.Gameplay.Save
{
    public interface ISaveCodec<TSave>
    {
        string Encode(TSave data);
        TSave Decode(string payload);
    }

    public sealed class AtomicFileSaveService<TSave> : ISaveService<TSave> where TSave : class
    {
        private readonly string _path;
        private readonly ISaveCodec<TSave> _codec;

        public AtomicFileSaveService(string path, ISaveCodec<TSave> codec)
        {
            _path = path ?? throw new ArgumentNullException(nameof(path));
            _codec = codec ?? throw new ArgumentNullException(nameof(codec));
        }

        public void Save(TSave data)
        {
            if (data == null)
            {
                throw new ArgumentNullException(nameof(data));
            }

            var directory = Path.GetDirectoryName(_path);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var temporaryPath = _path + ".tmp";
            File.WriteAllText(temporaryPath, _codec.Encode(data));
            try
            {
                if (File.Exists(_path))
                {
                    File.Replace(temporaryPath, _path, null);
                }
                else
                {
                    File.Move(temporaryPath, _path);
                }
            }
            finally
            {
                if (File.Exists(temporaryPath))
                {
                    File.Delete(temporaryPath);
                }
            }
        }

        public bool TryLoad(out TSave data)
        {
            data = null;
            if (!File.Exists(_path))
            {
                return false;
            }

            try
            {
                data = _codec.Decode(File.ReadAllText(_path));
                return data != null;
            }
            catch (Exception)
            {
                data = null;
                return false;
            }
        }
    }
}
