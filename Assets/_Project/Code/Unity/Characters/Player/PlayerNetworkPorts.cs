using Odyssey.Gameplay.Combat;
using UnityEngine;

namespace Odyssey.Characters.Player
{
    /// <summary>
    /// 定义玩家攻击命中窗口的结算端口；单机实现直接查询怪物，联机实现把同一窗口转换为 Host 命令。
    /// 采用 Strategy 与依赖倒置，使动作状态机只描述攻击节奏，不依赖 NGO、RPC 或服务器身份。
    /// </summary>
    public interface IPlayerAttackResolver
    {
        void Resolve(PlayerController player, int comboIndex);
    }

    /// <summary>
    /// 定义由外部权威维护玩家生命时所需的最小接口，供 Host 网络适配器接管伤害与无敌判断。
    /// PlayerController 只依赖此端口，不反向引用 Odyssey.Network，从而保持 Unity 玩法层的程序集边界。
    /// </summary>
    public interface IExternalPlayerDamageAuthority
    {
        int CurrentHealth { get; }
        bool IsDamageImmune { get; }
        DamageResult TryTakeDamage(int damage, Vector3 attackerPosition, string sourceId);
    }
}
