# ⚔️ Odyssey - 3D Action Game (Core System Overhaul)

## 📖 项目简介
本项目基于 Unity 官方的 3D Game Kit Lite (Odyssey) 框架进行二次开发与深度重构。
摒弃了官方原版冗余的组件连线逻辑，完全采用**纯代码接管**的方式，重写了底层的**敌方 AI 状态机**与**全局数据存储系统**，实现了更纯粹、更硬核的动作游戏手感。

---

## 🏗️ 核心技术架构

系统整体分为三个相互解耦的模块：**系统调度层**、**AI 逻辑层** 与 **玩家交互层**。

### 1. 💾 系统调度层 (System Management)
**核心脚本：`SaveManager.cs`**
作为全局系统的最高调度者，负责 UI 交互、时间控制与数据持久化。
* **输入监听：** 监听 `Esc` / `P` 键，随时接管游戏控制权。
* **时空冻结：** 呼出菜单时通过 `Time.timeScale = 0f` 冻结物理引擎与所有 Entity 逻辑，并解除鼠标指针锁定 (`Cursor.lockState`)。
* **JSON 序列化持久化：** * 集成底层类 `PlayerSaveData` 作为数据容器 (Data Container)。
    * 摒弃了存在安全漏洞的 `BinaryFormatter`，采用业界标准的 `JsonUtility` 进行明文序列化。
    * 通过 `Application.persistentDataPath` 实现全平台兼容的安全本地存储，精准记录玩家生命值与空间坐标。

### 2. 🧠 AI 逻辑层 (Enemy Artificial Intelligence)
**核心脚本：`Enemy.cs`**
完全重构的敌人大脑，抛弃了复杂的 Animator 连线配置，采用“代码空间传送”实现绝对控制。
* **状态锁 (Action Lock / Hyper Armor)：** 引入 `_actionLockTimer` 霸体机制。在攻击 (1.2s) 与受击 (0.5s) 期间，强制剥夺 AI 的思考与寻路能力，从根本上杜绝了边界距离判定导致的“状态机抽搐”与“滑冰推人”Bug。
* **绝对物理刹车：** 进入近战范围或霸体状态时，不仅设置 `isStopped = true`，且强制清空 `velocity = Vector3.zero`，消除底层 NavMesh 的物理惯性。
* **大范围精准判定：** 废弃官方原版的射线检测，改用胸前中心坐标点生成超大半径 `Physics.OverlapSphere`，并通过 `GetComponentInParent` 实现穿透层级的霸道伤害抓取。
* **物理击飞演算：** 死亡时注销导航组件，通过 `Vector3.Lerp` 与重力模拟算法，实现死亡沿受击反方向抛物线击飞与化为飞灰的视觉表现。

### 3. 🤺 玩家交互层 (Player Control)
**核心引用：`PlayerController.cs`**
保留并调用官方优秀的底层主角物理与动画系统，对外暴露接口：
* 供 AI 系统调用的 `TakeDamage(int damage, Vector3 position)` 伤害计算与击退演算接口。
* 供 SaveManager 调用的空间位置属性 `transform.position`。

---

## 🛠️ 环境配置与部署
* **引擎版本：** Unity 2023.2.20f1c1 (或以上)
* **渲染管线：** Built-in / URP
* **核心依赖：** * `UnityEngine.AI` (NavMesh 寻路系统)
    * TextMeshPro (UI 系统)
* **注意事项：** * 打包前须确保 `PauseCanvas` 的 `Canvas Scaler` 已设置为 `Scale With Screen Size` (1920x1080) 以保证多端 UI 适配。
    * 场景中所有的怪物 `Animator` 组件必须取消勾选 `Apply Root Motion` 以交还物理控制权。

---
*Document Generated for Odyssey Custom Build.*