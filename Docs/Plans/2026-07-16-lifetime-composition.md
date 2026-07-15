# 生命周期与场景装配实现计划

> **执行要求：** 使用 `superpowers:executing-plans` 按任务顺序执行；所有行为变更遵循测试驱动开发。

**目标：** 让应用级配置和存档服务由 Bootstrap 创建，让每个玩法场景通过 Installer 获得独立 Session，并删除 SaveManager 自行构造基础设施的隐藏依赖。

**架构：** `ApplicationContext` 只暴露明确命名的应用级端口，不提供通用服务查询；`GameplaySession` 拥有会话级 EventBus；`GameplaySceneInstaller` 是场景 Composition Root，负责配置玩家和存档控制器。Bootstrap 只管理应用生命周期和创建 Installer，不再直接扫描玩家。

**技术栈：** C#、Unity 2023.2、Odyssey.Core、Odyssey.Gameplay、Odyssey.Unity、NUnit EditMode。

---

### 任务一：先写生命周期失败规格

**文件：**

- 新增：`Tests/Core/GameplaySessionSpecs.cs`
- 修改：`Tests/Core/Program.cs`
- 修改：`Assets/_Project/Tests/EditMode/UnityAdapterTests.cs`

- [x] 规格要求每个 `GameplaySession` 拥有独立 EventBus，并在 Session 释放后拒绝继续发布。
- [x] EditMode 测试要求 `ApplicationContext` 显式保存配置与存档端口。
- [x] EditMode 测试要求 Installer 重复安装同一上下文保持幂等，并在销毁时释放 Session。
- [x] 运行核心与 EditMode 测试，确认因目标类型尚不存在而失败。

### 任务二：建立应用与会话对象

**文件：**

- 新增：`Assets/_Project/Code/Gameplay/Application/GameplaySession.cs`
- 新增：`Assets/_Project/Code/Gameplay/Save/PlayerSaveData.cs`
- 新增：`Assets/_Project/Code/Unity/Bootstrap/ApplicationContext.cs`

- [x] `GameplaySession` 创建并拥有 `GameEventBus`，重复释放安全。
- [x] 将 `PlayerSaveData` 从 Unity 场景脚本迁移到 Gameplay 存档模型，保持序列化字段名不变。
- [x] `ApplicationContext` 通过构造函数接收 `IGameConfigProvider` 与 `ISaveService<PlayerSaveData>`，拒绝空依赖且不提供 `GetService<T>`。
- [x] 运行核心规格，确认新增规则通过。

### 任务三：建立场景 Installer 并收敛 Bootstrap

**文件：**

- 新增：`Assets/_Project/Code/Unity/Bootstrap/GameplaySceneInstaller.cs`
- 修改：`Assets/_Project/Code/Unity/Bootstrap/OdysseyBootstrap.cs`
- 修改：`Assets/_Project/Code/Unity/Save/SaveManager.cs`

- [x] Bootstrap 加载配置并创建原子 JSON 存档服务，再构建 ApplicationContext。
- [x] 每次场景加载时只查找或创建一个场景 Installer，并把对象移动到目标场景。
- [x] Installer 创建本场景 Session，只扫描本场景根对象，绑定玩家配置并注入 SaveManager。
- [x] SaveManager 改为依赖 `ISaveService<PlayerSaveData>`，删除 `Awake` 内部构造。
- [x] Installer 销毁时释放 Session；不同上下文重复安装明确抛错。

### 任务四：验证、提交与推送

- [x] 核心规格、Unity EditMode、中文审计、架构注释审计全部通过。
- [x] `git diff --check` 无错误，场景和 Prefab GUID 不变。
- [ ] 提交 `refactor(bootstrap): 建立应用与场景生命周期装配`。
- [ ] 推送 `codex/player-architecture`。
