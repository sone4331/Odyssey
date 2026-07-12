using UnityEngine;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary; // 二进制所需
using Odyssey.Characters.Player; // 引入你的玩家命名空间

namespace Odyssey.Systems
{
    // 1. 定义一个专门用来存放数据的“数据盒子”
    [System.Serializable] // 必须加上这个标签，才能被序列化
    public class PlayerSaveData
    {
        public int health;
        public float posX, posY, posZ;
    }

    public class SaveManager : MonoBehaviour
    {
        [Header("UI 引用")]
        public GameObject PauseMenuPanel; // 拖入你刚才做的 PauseMenu

        [Header("玩家引用")]
        public PlayerController Player; // 拖入场景里的 Ellen

        private bool _isPaused = false;
        
        // 存档文件的路径
        private string _saveFilePath;

        private void Awake()
        {
            // Application.persistentDataPath 是 Unity 官方推荐的存档路径，不同平台会自动适配（比如C盘AppData）
            _saveFilePath = Application.persistentDataPath + "/SaveData.json";
        }

        private void Update()
        {
            // 改为：按下 P 键 或者 Esc 键 都可以呼出菜单！
            if (Input.GetKeyDown(KeyCode.P) || Input.GetKeyDown(KeyCode.Escape))
            {
                if (_isPaused) ResumeGame();
                else PauseGame();
            }
        }

        // --- 游戏暂停与恢复逻辑 ---
        private void PauseGame()
        {
            PauseMenuPanel.SetActive(true);
            Time.timeScale = 0f; 
            _isPaused = true;

            // --- 【新增：解锁并显示鼠标】 ---
            Cursor.visible = true; // 让鼠标可见
            Cursor.lockState = CursorLockMode.None; // 解除鼠标锁定状态
        }

        private void ResumeGame()
        {
            PauseMenuPanel.SetActive(false);
            Time.timeScale = 1f; 
            _isPaused = false;

            // --- 【新增：重新隐藏并锁定鼠标】 ---
            Cursor.visible = false; // 隐藏鼠标
            Cursor.lockState = CursorLockMode.Locked; // 再次把鼠标锁死在屏幕中心
        }

        // ==========================================
        // 🏆 推荐：JSON 存储方式 (主流、明文可查、扩展性强)
        // ==========================================
        public void SaveGame_JSON()
        {
            // 1. 把玩家当前状态装进盒子里
            PlayerSaveData data = new PlayerSaveData();
            data.health = Player.CurrentHealth;
            data.posX = Player.transform.position.x;
            data.posY = Player.transform.position.y;
            data.posZ = Player.transform.position.z;

            // 2. 把盒子转换成 JSON 字符串
            string json = JsonUtility.ToJson(data, true);

            // 3. 写入文件
            File.WriteAllText(_saveFilePath, json);
            Debug.Log("【JSON】游戏已保存到: " + _saveFilePath);
            ResumeGame();
        }

        public void LoadGame_JSON()
        {
            if (File.Exists(_saveFilePath))
            {
                // 1. 读取 JSON 字符串
                string json = File.ReadAllText(_saveFilePath);
                
                // 2. 将字符串还原成盒子对象
                PlayerSaveData data = JsonUtility.FromJson<PlayerSaveData>(json);

                // 3. 把数据应用给玩家
                Player.CurrentHealth = data.health;
                
                // 注意：由于玩家身上有 CharacterController，瞬间移动前必须先关闭它
                Player.GetComponent<CharacterController>().enabled = false;
                Player.transform.position = new Vector3(data.posX, data.posY, data.posZ);
                Player.GetComponent<CharacterController>().enabled = true;

                Debug.Log("【JSON】游戏读取成功！");
                ResumeGame();
            }
            else
            {
                Debug.LogWarning("找不到存档文件！");
            }
        }

        // ==========================================
        // 🥈 备选：PlayerPrefs 存储 (通常只用来存音量、画质设置)
        // ==========================================
        public void SaveGame_PlayerPrefs()
        {
            PlayerPrefs.SetInt("PlayerHealth", Player.CurrentHealth);
            PlayerPrefs.SetFloat("PlayerPosX", Player.transform.position.x);
            PlayerPrefs.SetFloat("PlayerPosY", Player.transform.position.y);
            PlayerPrefs.SetFloat("PlayerPosZ", Player.transform.position.z);
            PlayerPrefs.Save(); // 强制写入硬盘
            Debug.Log("【PlayerPrefs】游戏已保存！");
        }

        // ==========================================
        // 🥉 了解：二进制存储 (不推荐商用，仅供学习原理)
        // ==========================================
        public void SaveGame_Binary()
        {
            PlayerSaveData data = new PlayerSaveData();
            data.health = Player.CurrentHealth;
            data.posX = Player.transform.position.x;
            // ... 同上装箱操作

            BinaryFormatter formatter = new BinaryFormatter();
            string path = Application.persistentDataPath + "/SaveData.dat";
            FileStream stream = new FileStream(path, FileMode.Create);

            formatter.Serialize(stream, data);
            stream.Close();
            Debug.Log("【二进制】游戏已保存！");
        }

        // ==========================================
        // 🚪 退出游戏逻辑
        // ==========================================
        public void QuitGame()
        {
            Debug.Log("正在退出游戏...");
            // 如果在编辑器里，这段代码能停止运行
            #if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
            #else
            // 如果打包成了独立的 exe 游戏，这句代码会关闭窗口
            Application.Quit();
            #endif
        }
    }
}