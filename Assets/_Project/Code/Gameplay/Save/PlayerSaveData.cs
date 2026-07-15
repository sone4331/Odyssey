using System;

namespace Odyssey.Gameplay.Save
{
    /// <summary>
    /// 定义当前玩家存档的版本化数据契约，只保存可序列化值，不引用场景对象或 Unity 组件。
    /// 将 DTO 放在 Gameplay 层可使文件存档、云存档和测试替身共享同一格式，并避免序列化协议依赖 SaveManager。
    /// </summary>
    [Serializable]
    public sealed class PlayerSaveData : IVersionedSave
    {
        public const int CurrentVersion = 1;

        public int version = CurrentVersion;
        public int health;
        public float posX;
        public float posY;
        public float posZ;

        public int Version
        {
            get => version;
            set => version = value;
        }
    }
}
