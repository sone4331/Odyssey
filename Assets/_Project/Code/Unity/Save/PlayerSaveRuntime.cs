using System;
using Odyssey.Characters.Player;
using Odyssey.Gameplay.Save;
using UnityEngine;

namespace Odyssey.Unity.Save
{
    /// <summary>
    /// 在 PlayerController 与应用级 ISaveService 之间转换玩家快照，不处理暂停界面或输入。
    /// 采用 Adapter 模式隔离 Transform、CharacterController 与纯存档模型，使 SaveManager 只保留场景按钮兼容职责。
    /// </summary>
    internal sealed class PlayerSaveRuntime
    {
        private readonly PlayerController _player;
        private readonly ISaveService<PlayerSaveData> _service;

        public PlayerSaveRuntime(PlayerController player, ISaveService<PlayerSaveData> service)
        {
            _player = player;
            _service = service ?? throw new ArgumentNullException(nameof(service));
        }

        /// <summary>
        /// 从玩家当前权威状态创建版本化快照并交给原子文件服务持久化。
        /// </summary>
        public bool Save()
        {
            if (_player == null)
            {
                Debug.LogError("保存失败：未指定玩家。");
                return false;
            }

            var position = _player.transform.position;
            _service.Save(new PlayerSaveData
            {
                health = _player.CurrentHealth,
                posX = position.x,
                posY = position.y,
                posZ = position.z
            });
            return true;
        }

        /// <summary>
        /// 校验存档版本后恢复生命与位置；传送期间临时关闭 CharacterController，避免物理系统覆盖加载结果。
        /// </summary>
        public bool Load()
        {
            if (_player == null)
            {
                Debug.LogError("读取失败：未指定玩家。");
                return false;
            }

            if (!_service.TryLoad(out var data))
            {
                Debug.LogWarning("未找到有效存档文件。");
                return false;
            }

            if (data.Version != PlayerSaveData.CurrentVersion)
            {
                Debug.LogError($"不支持存档版本 {data.Version}。");
                return false;
            }

            _player.SetHealth(data.health, "load");
            var controller = _player.Controller;
            controller.enabled = false;
            _player.transform.position = new Vector3(data.posX, data.posY, data.posZ);
            controller.enabled = true;
            return true;
        }
    }
}
