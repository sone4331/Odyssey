using System;
using System.Collections.Generic;
using UnityEngine;

namespace Odyssey.Characters.Enemies
{
    /// <summary>
    /// 保存单个怪物的有序巡逻点，并维护“到达—等待—前往下一点”的轻量游标。
    /// 采用场景组件与状态游标模式：路线负责点位和进度，EnemyActionRuntime 只负责 NavMesh 移动与动画。
    /// 这样策划可以直接在 Scene 视图调整路线，同时让多只怪物共享点位但使用不同起点，避免队伍重叠。
    /// </summary>
    public sealed class EnemyPatrolRoute : MonoBehaviour
    {
        [SerializeField] private Transform[] patrolPoints = Array.Empty<Transform>();
        [SerializeField, Min(0)] private int initialPointIndex;
        [SerializeField, Min(0.05f)] private float arrivalDistance = 0.45f;
        [SerializeField, Min(0f)] private float waitDuration = 0.8f;

        private int _currentIndex;
        private float _waitRemaining;

        public IReadOnlyList<Transform> PatrolPoints => patrolPoints;
        public bool HasValidRoute => patrolPoints != null && patrolPoints.Length >= 2;
        public float ArrivalDistance => arrivalDistance;
        public string CurrentPointName => HasValidRoute && patrolPoints[_currentIndex] != null
            ? patrolPoints[_currentIndex].name
            : "无";
        public int CurrentPointIndex => _currentIndex;
        public float MaximumPointDistance
        {
            get
            {
                var maximum = 0f;
                if (patrolPoints == null)
                {
                    return maximum;
                }

                for (var first = 0; first < patrolPoints.Length; first++)
                {
                    for (var second = first + 1; second < patrolPoints.Length; second++)
                    {
                        if (patrolPoints[first] != null && patrolPoints[second] != null)
                        {
                            maximum = Mathf.Max(maximum,
                                Vector3.Distance(patrolPoints[first].position, patrolPoints[second].position));
                        }
                    }
                }

                return maximum;
            }
        }

        private void Awake()
        {
            _currentIndex = HasValidRoute
                ? Mathf.Clamp(initialPointIndex, 0, patrolPoints.Length - 1)
                : 0;
        }

        /// <summary>
        /// 计算本帧巡逻目标；返回 false 表示路线配置无效，waiting 为 true 表示怪物应在点位短暂停留。
        /// 方法不直接操作 NavMeshAgent，保证路线状态可以独立于具体移动适配器演进。
        /// </summary>
        public bool Evaluate(
            Vector3 ownerPosition,
            float deltaTime,
            out Vector3 destination,
            out bool waiting)
        {
            destination = ownerPosition;
            waiting = false;
            if (!HasValidRoute)
            {
                return false;
            }

            _currentIndex = Mathf.Clamp(_currentIndex, 0, patrolPoints.Length - 1);
            var currentPoint = patrolPoints[_currentIndex];
            if (currentPoint == null)
            {
                Advance();
                return false;
            }

            destination = currentPoint.position;
            if (_waitRemaining > 0f)
            {
                _waitRemaining = Mathf.Max(0f, _waitRemaining - deltaTime);
                waiting = true;
                return true;
            }

            var planarOffset = Vector3.ProjectOnPlane(destination - ownerPosition, Vector3.up);
            if (planarOffset.sqrMagnitude > arrivalDistance * arrivalDistance)
            {
                return true;
            }

            _waitRemaining = waitDuration;
            Advance();
            waiting = true;
            return true;
        }

        private void Advance()
        {
            _currentIndex = (_currentIndex + 1) % patrolPoints.Length;
        }

        /// <summary>
        /// 由导航适配器在当前点不可达时跳到下一点，避免 Agent 对着断开的 NavMesh 岛持续原地播放跑步。
        /// 路线只移动游标，不承担路径计算职责。
        /// </summary>
        public void SkipCurrentPoint()
        {
            _waitRemaining = 0f;
            if (HasValidRoute)
            {
                Advance();
            }
        }

        private void OnValidate()
        {
            arrivalDistance = Mathf.Max(0.05f, arrivalDistance);
            waitDuration = Mathf.Max(0f, waitDuration);
            initialPointIndex = Mathf.Max(0, initialPointIndex);
        }

        private void OnDrawGizmosSelected()
        {
            if (!HasValidRoute)
            {
                return;
            }

            Gizmos.color = new Color(0.1f, 0.9f, 1f, 0.9f);
            for (var index = 0; index < patrolPoints.Length; index++)
            {
                var current = patrolPoints[index];
                var next = patrolPoints[(index + 1) % patrolPoints.Length];
                if (current == null || next == null)
                {
                    continue;
                }

                Gizmos.DrawWireSphere(current.position, arrivalDistance);
                Gizmos.DrawLine(current.position, next.position);
            }
        }
    }
}
