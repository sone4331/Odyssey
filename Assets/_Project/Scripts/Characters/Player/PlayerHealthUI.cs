using UnityEngine;
using UnityEngine.UI;
using Odyssey.Gameplay.Combat;
using Odyssey.Unity.UI;

namespace Odyssey.Characters.Player
{
    public class PlayerHealthUI : MonoBehaviour, IHealthDisplayView
    {
        [Header("References")]
        public PlayerController Player; 
        public Image[] HealthIcons; 

        [Header("Sprites (满血/空血图片)")]
        public Sprite FullHealthSprite;  // 存放亮的彩色水晶
        public Sprite EmptyHealthSprite; // 存放暗的灰色水晶

        [Header("Damage Flash Effect")]
        public Image DamageFlashImage; 
        public Color FlashColor = new Color(1f, 0f, 0f, 0.4f); // 默认半透明红
        public float FlashSpeed = 5f; 

        private PlayerController _boundPlayer;
        private HealthDisplayPresenter _presenter;

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
            if (HealthIcons == null)
            {
                return;
            }

            for (var i = 0; i < HealthIcons.Length; i++)
            {
                if (HealthIcons[i] != null)
                {
                    HealthIcons[i].sprite = i < current && i < maximum
                        ? FullHealthSprite
                        : EmptyHealthSprite;
                }
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
            _presenter.Initialize(_boundPlayer.CurrentHealth);
        }

        private void UnbindPlayer()
        {
            if (_boundPlayer != null)
            {
                _boundPlayer.HealthChanged -= OnHealthChanged;
            }

            _boundPlayer = null;
            _presenter = null;
        }

        private void OnHealthChanged(HealthChanged change)
        {
            _presenter?.Handle(change);
        }
    }
}
