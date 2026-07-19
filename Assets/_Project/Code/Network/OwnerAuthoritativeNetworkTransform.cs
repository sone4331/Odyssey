using Unity.Netcode.Components;

namespace Odyssey.Networking
{
    /// <summary>
    /// 让玩家 Owner 提交现有 CharacterController 位移，Host 仍通过独立校验器纠正异常跳变。
    /// 只用于玩家 Prefab；怪物和投射物继续使用默认 Server 权威 NetworkTransform。
    /// </summary>
    public sealed class OwnerAuthoritativeNetworkTransform : NetworkTransform
    {
        protected override bool OnIsServerAuthoritative() => false;
    }
}
