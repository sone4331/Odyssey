using Unity.Netcode.Components;

namespace Odyssey.Networking
{
    /// <summary>
    /// 复制 Owner 运行的原玩家 Animator 状态，使远端无需重跑输入和动作状态机即可看到完整连击与移动表现。
    /// </summary>
    public sealed class OwnerAuthoritativeNetworkAnimator : NetworkAnimator
    {
        protected override bool OnIsServerAuthoritative() => false;
    }
}
