using System;
using Odyssey.Gameplay.Save;
using UnityEngine;

namespace Odyssey.Unity.Save
{
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
                throw new ArgumentException("Save payload cannot be empty.", nameof(payload));
            }

            var data = JsonUtility.FromJson<TSave>(payload);
            if (data == null)
            {
                throw new InvalidOperationException("Save payload did not produce a data object.");
            }

            return data;
        }
    }
}
