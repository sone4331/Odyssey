using UnityEngine;
using UnityEngine.UI;
using Odyssey.Gameplay.Combat;
using Odyssey.Unity.UI;

namespace Odyssey.Characters.Player
{
    /// <summary>
    /// 实现血量视图端口并管理 PlayerController 事件订阅，只负责 UGUI 图标和受伤闪屏表现。
    /// 采用 Passive View/MVP 与观察者模式，避免 UI 每帧轮询或直接修改 Gameplay 生命状态。
    /// </summary>
    public class PlayerHealthUI : MonoBehaviour, IHealthDisplayView
    {
        [Header("引用")]
        public PlayerController Player; 
        public Image[] HealthIcons; 

        [Header("Sprites (满血/空血图片)")]
        public Sprite FullHealthSprite;  // 存放亮的彩色水晶
        public Sprite EmptyHealthSprite; // 存放暗的灰色水晶

        [Header("受伤闪屏效果")]
        public Image DamageFlashImage; 
        public Color FlashColor = new Color(1f, 0f, 0f, 0.4f); // 默认半透明红
        public float FlashSpeed = 5f; 

        private PlayerController _boundPlayer;
        private HealthDisplayPresenter _presenter;
        private HealthIconPool _iconPool;
        private bool _reportedMissingTemplate;

        private void OnEnable()
        {
            BindPlayer();
        }

        private void Start()
        {
            BindPlayer();
        }

        private void OnDisable()
        {
            UnbindPlayer();
        }

        private void Update()
        {
            if (DamageFlashImage != null)
            {
                DamageFlashImage.color = Color.Lerp(DamageFlashImage.color, Color.clear, FlashSpeed * Time.deltaTime);
            }
        }

        public void SetHealth(int current, int maximum)
        {
            if (_iconPool == null)
            {
                _iconPool = new HealthIconPool(HealthIcons);
            }

            if (!_iconPool.SetHealth(current, maximum, FullHealthSprite, EmptyHealthSprite) &&
                !_reportedMissingTemplate)
            {
                _reportedMissingTemplate = true;
                Debug.LogError("血量 UI 缺少可用于扩容的生命图标模板。", this);
            }
        }

        public void ShowDamageFlash()
        {
            if (DamageFlashImage != null)
            {
                DamageFlashImage.color = FlashColor;
            }
        }

        private void BindPlayer()
        {
            if (Player == null || _boundPlayer == Player)
            {
                return;
            }

            UnbindPlayer();
            _boundPlayer = Player;
            _presenter = new HealthDisplayPresenter(this, Player.MaxHealth);
            _boundPlayer.HealthChanged += OnHealthChanged;
            _boundPlayer.RuntimeConfigured += OnRuntimeConfigured;
            _presenter.Initialize(_boundPlayer.CurrentHealth);
        }

        private void UnbindPlayer()
        {
            if (_boundPlayer != null)
            {
                _boundPlayer.HealthChanged -= OnHealthChanged;
                _boundPlayer.RuntimeConfigured -= OnRuntimeConfigured;
            }

            _boundPlayer = null;
            _presenter = null;
        }

        private void OnHealthChanged(HealthChanged change)
        {
            _presenter?.Handle(change);
        }

        private void OnRuntimeConfigured()
        {
            _presenter?.Reconfigure(_boundPlayer.CurrentHealth, _boundPlayer.MaxHealth);
        }
    }
}
