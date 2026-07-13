using System;
using System.IO;

namespace Odyssey.Gameplay.Save
{
    /// <summary>
    /// 定义存档对象与文本载荷之间的编解码端口，使文件事务不依赖具体 JSON 库或 Unity API。
    /// </summary>
    public interface ISaveCodec<TSave>
    {
        string Encode(TSave data);
        TSave Decode(string payload);
    }

    /// <summary>
    /// 通过临时文件写入和原子替换持久化存档，避免进程中断留下半写文件。
    /// 采用 Strategy 注入编解码器，并把文件事务与序列化职责分离，便于测试失败恢复路径。
    /// </summary>
    public sealed class AtomicFileSaveService<TSave> : ISaveService<TSave> where TSave : class
    {
        private readonly string _path;
        private readonly ISaveCodec<TSave> _codec;

        public AtomicFileSaveService(string path, ISaveCodec<TSave> codec)
        {
            _path = path ?? throw new ArgumentNullException(nameof(path));
            _codec = codec ?? throw new ArgumentNullException(nameof(codec));
        }

        /// <summary>
        /// 先完整写入临时文件，再替换正式文件；任何写入异常都不会破坏上一份有效存档。
        /// </summary>
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
