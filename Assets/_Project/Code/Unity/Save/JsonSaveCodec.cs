using System;
using Odyssey.Gameplay.Save;
using UnityEngine;

namespace Odyssey.Unity.Save
{
    /// <summary>
    /// 使用 Unity JsonUtility 实现存档编解码端口，不参与文件事务或版本迁移。
    /// 这是 Adapter 模式的基础设施实现，使 Gameplay 存档服务保持对 Unity 序列化 API 的零依赖。
    /// </summary>
    public sealed class JsonSaveCodec<TSave> : ISaveCodec<TSave> where TSave : class
    {
        public string Encode(TSave data)
        {
            if (data == null)
            {
                throw new ArgumentNullException(nameof(data));
            }

            return JsonUtility.ToJson(data, true);
        }

        public TSave Decode(string payload)
        {
            if (string.IsNullOrWhiteSpace(payload))
            {
                throw new ArgumentException("存档载荷不能为空。", nameof(payload));
            }

            var data = JsonUtility.FromJson<TSave>(payload);
            if (data == null)
            {
                throw new InvalidOperationException("存档载荷未能生成有效数据对象。");
            }

            return data;
        }
    }
}
