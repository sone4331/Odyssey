using System;
using UnityEngine;

namespace Odyssey.Unity.Save
{
    /// <summary>
    /// 负责单个玩法场景的暂停状态、时间缩放、面板和光标表现。
    /// 采用小型 Facade 封装 Unity 全局副作用，使存档流程不再同时承担暂停职责，并在销毁时保证时间缩放恢复。
    /// </summary>
    public sealed class PauseRuntime : IDisposable
    {
        private readonly GameObject _panel;

        public PauseRuntime(GameObject panel)
        {
            _panel = panel;
            SetPaused(false);
        }

        public bool IsPaused { get; private set; }

        /// <summary>
        /// 原子地提交暂停状态及其全部 Unity 表现，避免面板、光标和 Time.timeScale 出现互相不一致。
        /// </summary>
        public void SetPaused(bool paused)
        {
            IsPaused = paused;
            Time.timeScale = paused ? 0f : 1f;
            if (_panel != null)
            {
                _panel.SetActive(paused);
            }

            Cursor.visible = paused;
            Cursor.lockState = paused ? CursorLockMode.None : CursorLockMode.Locked;
        }

        public void Dispose()
        {
            SetPaused(false);
        }
    }
}
