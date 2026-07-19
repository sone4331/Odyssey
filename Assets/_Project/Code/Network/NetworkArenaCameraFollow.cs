using UnityEngine;

namespace Odyssey.Networking
{
    /// <summary>
    /// 仅跟随本机拥有的网络角色，确保双开时每个进程看到自己的视角。
    /// 采用轻量 LateUpdate 跟随而非新增 Cinemachine 配置，因为技术竞技场只需要稳定观察同步结果。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class NetworkArenaCameraFollow : MonoBehaviour
    {
        private static readonly Vector3 Offset = new Vector3(0f, 9f, -10f);
        private Transform _target;

        public void SetTarget(Transform target)
        {
            _target = target;
            SnapToTarget();
        }

        private void LateUpdate()
        {
            if (_target == null)
            {
                return;
            }

            var desired = _target.position + Offset;
            transform.position = Vector3.Lerp(transform.position, desired, 1f - Mathf.Exp(-10f * Time.deltaTime));
            transform.rotation = Quaternion.LookRotation((_target.position + Vector3.up - transform.position).normalized);
        }

        private void SnapToTarget()
        {
            if (_target == null)
            {
                return;
            }

            transform.position = _target.position + Offset;
            transform.rotation = Quaternion.LookRotation((_target.position + Vector3.up - transform.position).normalized);
        }
    }
}
