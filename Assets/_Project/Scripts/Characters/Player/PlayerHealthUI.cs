using UnityEngine;
using UnityEngine.UI;

namespace Odyssey.Characters.Player
{
    public class PlayerHealthUI : MonoBehaviour
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

        private int _lastHealth; 

        private void Start()
        {
            if (Player != null) _lastHealth = Player.CurrentHealth;
        }

        private void Update()
        {
            if (Player == null) return;

            // --- 1. 切换图片 (满血亮起 / 空血变暗) ---
            for (int i = 0; i < HealthIcons.Length; i++)
            {
                if (i < Player.CurrentHealth)
                {
                    HealthIcons[i].sprite = FullHealthSprite; // 亮起
                }
                else
                {
                    HealthIcons[i].sprite = EmptyHealthSprite; // 变暗
                }
            }

            // --- 2. 检测受伤并触发红屏 ---
            if (Player.CurrentHealth < _lastHealth)
            {
                if (DamageFlashImage != null) DamageFlashImage.color = FlashColor;
            }
            _lastHealth = Player.CurrentHealth;

            // --- 3. 红屏平滑褪色 ---
            if (DamageFlashImage != null)
            {
                DamageFlashImage.color = Color.Lerp(DamageFlashImage.color, Color.clear, FlashSpeed * Time.deltaTime);
            }
        }
    }
}