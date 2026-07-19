# Odyssey

Odyssey 是一个面向 Unity 玩法客户端岗位的作品集项目，当前版本重点展示角色控制、动作战斗、差异化敌人、可解释 AI、数据驱动、存档、自动化测试与轻量 Host 权威联机。

## 当前状态

单机玩法闭环与独立联机技术切片已经完成：

- `Core → Gameplay → Unity → Editor/Tests` 程序集依赖边界已经建立。
- Bootstrap 与场景 Installer 显式装配配置和存档服务，不使用全局可变单例。
- 玩家使用移动轴与动作轴两条正交状态机；代码驱动位移负责加减速、转向和坡面投影，Animator 负责 Idle/Walk/Run、空中、落地和连击表现。
- Generic Ellen 使用轻量 Animation Rigging 完成双脚与骨盆贴坡，墙边安全半径只保护常态移动，不把表现约束混入玩法权威状态。
- 玩家与怪物共享 `DamageRequest → DamageResult → HealthChanged` 战斗管线，六点生命配置可自动扩容血量图标。
- 怪物 AI 按 `Perception → Blackboard → Reactive Behavior Tree → Action Port` 分层，并在 Inspector 显示当前中文运行路径。
- Chomper 负责近身压迫，Spitter 通过安全距离、攻击前摇和代码驱动投射物制造远程空间压力。
- 冲刺 Ability 在有效时段持有 `State.Invulnerable` 标签，接触和投射物伤害通过统一入口校验。
- 场景包含两组位于不同区域的独立遭遇；每组三只怪物从开局沿六点宽范围网络巡逻，感知玩家后自主追击攻击，第一战区清理完成后重新踩原关卡踏板才会开启隔离门。
- CSV 自动导入为经过校验的只读配置资产；存档使用带版本号的原子 JSON。
- 输入适配器只暴露连续快照和离散动作命令；暂停、存档与玩家快照职责已经分离。
- 独立 `NetworkArena` 使用 NGO + Unity Transport（UDP）实现双人 Host/Client IPv4 直连；Client 只提交移动与攻击命令，Host 权威决定位置、命中、生命、死亡和复活。
- 网络攻击使用递增请求序号，并明确拒绝重复、过期、冷却中、无目标和超距离请求；运行面板同步显示 RTT、请求序号、拒绝原因与权威生命。
- 当前门禁为 55 项纯 C# 核心规格、27 项 Unity EditMode 测试和 19 项 PlayMode 运行态测试。

首个作品集版本明确不实现 Lua、完整 GAS、确定性帧同步、KCP、Relay、NAT 穿透、通用对象池或全量零 GC 框架。联机范围冻结在可本机双开和局域网直连的双人技术切片，不继续扩展为在线服务框架。

## 架构文档

- [目标架构说明](Docs/Architecture/TargetArchitecture.md)：解释应用、会话、场景和角色生命周期，以及命名、事件和状态机边界。
- [架构基线验收指南](Docs/Acceptance/ArchitectureBaselineAcceptance.md)：逐步验证编译、场景自动装配、配置、战斗和 Git 状态。
- [玩家正交状态机验收指南](Docs/Acceptance/PlayerStateRefactorAcceptance.md)：验证移动轴、动作轴、受击打断、复活与 GC 行为。
- [联网前单机闭环验收指南](Docs/Acceptance/PreNetworkSinglePlayerAcceptance.md)：从编译、测试、场景、动画、AI、存档到轻量性能采样逐项验收当前版本。
- [单机玩法切片验收指南](Docs/Acceptance/GameplaySliceAcceptance.md)：验证 Spitter、冲刺无敌、战斗遭遇与事件驱动表现。
- [本地联机竞技场验收指南](Docs/Acceptance/NetworkArenaAcceptance.md)：从自动搭建、Windows 构建到本机双开逐项验证 Host 权威状态同步。

## 本地质量门禁

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\Tools\RunCoreTests.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File .\Tools\TestDocumentation.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File .\Tools\TestHumanReadableLanguage.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File .\Tools\Tests\RunDocumentationAuditSpecs.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File .\Tools\Tests\RunHumanReadableLanguageSpecs.ps1
```

项目自有生产类型使用中文 XML 摘要说明职责、设计模式和设计原因。已确认的遗留控制器记录在 `Tools/DocumentationAuditExclusions.txt`，对应里程碑重构完成时必须同步移出白名单。

在 Unity 内也可以直接使用：

- `Odyssey/配置/导入并校验全部 CSV`
- `Odyssey/场景/搭建单机玩法切片`
- `Odyssey/测试/运行 EditMode 测试`
- `Odyssey/测试/运行 PlayMode 测试`
- `Odyssey/联机/重新搭建本地联机场景`
- `Odyssey/联机/构建 Windows 双开演示`

## 开发环境

- Unity 版本：`2023.2.20f1c1`
- 输入系统：Input System `1.7.0`
- 动画约束：Animation Rigging `1.3.0`
- 摄像机：Cinemachine `2.10.0`
- 联机：Netcode for GameObjects `1.15.1`、Unity Transport `1.5.0`
- 主要开发与演示平台：Windows

## 项目目录

```text
Assets/_Project/
├─ Code/       程序集边界内的 Core、Gameplay、Unity、Network 与 Editor 代码
├─ Content/    场景、Prefab、动画控制器与输入动作资产
├─ Data/       设计 CSV、运行时 Resources 数据库与 Unity 配置资产
└─ Tests/      Unity EditMode 与 PlayMode 测试
```

## 第三方内容

当前原型场景引用 Unity 3D Game Kit Lite。该资源包和 TextMesh Pro 示例资产不会随公开源码重新分发。打开 `Assets/_Project/Content/Scenes/Level_01.unity` 前，需要从合法来源导入对应的原始资源包。

仓库使用 `type(scope): 中文说明` 格式的 Conventional Commits。只有相关纯 C#、Unity、中文文案和文档审计全部通过后，完整模块才会推送到远端。
