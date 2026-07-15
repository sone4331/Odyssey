# Odyssey 目标架构说明

## 文档目的

本文档说明 Odyssey 重构后的对象生命周期、命名规则、依赖方向、事件通信和状态机边界。它不是设计模式清单，而是后续代码评审、测试和面试讲解共同遵守的架构契约。

## 生命周期

运行时对象分为四种作用域：

| 作用域 | 开始与结束 | 典型对象 |
|---|---|---|
| 应用 | 从启动进程到退出游戏 | Bootstrap、配置、存档文件服务、音频输出 |
| 会话 | 从开始一局游戏或建立一次连接，到返回菜单或断开连接 | EventBus、ActorRegistry、权威战斗上下文 |
| 场景 | 从场景装载到场景卸载 | Installer、暂停控制器、UI Presenter、调试视图 |
| 角色 | 从角色生成到销毁 | Health、AbilitySystem、Locomotion FSM、AI Blackboard |

应用级服务可以只有一个运行实例，但不因此暴露静态 `Instance`。它们由 `OdysseyBootstrap` 创建，再由场景安装器显式注入。会话对象必须在每局结束时释放，防止事件和可变状态泄漏到下一局。

## 命名规则

统一命名指相同职责使用相同后缀，而不是所有协调类都叫 Manager。

| 后缀 | 唯一含义 | 示例 |
|---|---|---|
| Bootstrap | 建立应用级对象关系 | `OdysseyBootstrap` |
| Installer | 组装场景依赖 | `GameplaySceneInstaller` |
| Controller | 接收输入或命令并协调流程 | `PauseController` |
| Service | 提供可复用基础能力 | `AudioService` |
| System | 执行纯玩法规则 | `AbilitySystem` |
| Presenter | 把状态和事件转换为界面表现 | `HealthDisplayPresenter` |
| Provider | 提供只读数据查询 | `GameConfigProvider` |
| Adapter | 翻译 Unity 或第三方接口 | `NavMeshMovementAdapter` |

只有当一个对象明确持有并管理一组同类对象的创建、索引和销毁时才使用 Manager。项目不新增通用 `SingletonMonoBehaviour<T>`、`GameManager`、`UIManager` 或 `CombatManager`。

## 依赖方向

```text
Odyssey.Core
    ↑
Odyssey.Gameplay
    ↑
Odyssey.Unity
    ↑
Odyssey.Editor / Tests
```

- Core 保存状态机、Tag、Ability 和事件总线等不依赖 Unity 的机制。
- Gameplay 保存战斗、角色、AI、配置与存档规则。
- Unity 只负责 Input System、CharacterController、Animator、NavMesh、UI 和网络适配。
- Bootstrap 与 Installer 是 Composition Root；业务对象不得自行查找全局服务。

## 事件通信

命令表达“请求执行”，事件表达“已经发生”。命令使用一对一方法调用并返回结果；事件使用 `Action<T>` 或会话级 `IGameEventBus` 通知观察者。

局部所有权关系优先使用直接事件，例如 `IHealth.Changed`。只有伤害结果、角色死亡、暂停变化和网络请求审计等跨功能事实进入 EventBus。EventBus 为会话级对象，使用强类型载荷、同步主线程发布和可释放订阅，不使用字符串、反射扫描或静态全局入口。

## 玩家状态边界

旧玩家状态机在状态 `Tick` 内立即调用 `ChangeState`，因此父状态切换后子状态仍可能继续执行本帧逻辑。当前 `PlayerIdleState` 和 `PlayerMoveState` 中的实例检查正是为规避该问题而存在。

目标设计拆成三个正交维度：

```text
角色生命周期：Alive / Dead / Respawning
Locomotion：Grounded / Airborne / WallSlide
Ability：Attack / Dash / HitReaction
```

Locomotion 使用延迟提交 FSM：状态只返回转移意图，状态机在 `Tick` 返回后统一执行 Exit、替换和 Enter。Idle 与 Move 由 Grounded 状态内的速度和动画混合树表达，不再作为独立状态。Attack、Dash 和 HitReaction 由 AbilitySystem 与 Gameplay Tag 表达，不再挤占移动状态。

## 渐进迁移约束

1. 每个模块先增加失败规格，再实现最小行为。
2. Legacy FSM 在新玩家控制链完成行为对照前保留。
3. 新旧系统不得同时写入同一份角色状态。
4. 每个模块通过纯 C#、Unity、中文和注释门禁后独立提交并推送。
5. 运行时调试面板必须能显示状态、Ability、领域事件和网络验证结果，让架构可以在演示视频中被观察。
