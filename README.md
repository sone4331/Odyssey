# Odyssey

Odyssey 是一个面向 Unity 玩法客户端岗位的作品集项目，当前版本重点展示角色控制、动作战斗、差异化敌人、可解释 AI、数据驱动、存档与自动化测试。联网将作为独立的下一阶段扩展，不在当前单机闭环中伪装为已完成功能。

## 当前状态

联网前单机闭环已经完成：

- `Core → Gameplay → Unity → Editor/Tests` 程序集依赖边界已经建立。
- Bootstrap 与场景 Installer 显式装配配置和存档服务，不使用全局可变单例。
- 玩家使用移动轴与动作轴两条正交状态机；代码驱动位移负责加减速、转向和坡面投影，Animator 负责 Idle/Walk/Run、空中、落地和连击表现。
- Generic Ellen 使用轻量 Animation Rigging 完成双脚与骨盆贴坡，墙边安全半径只保护常态移动，不把表现约束混入玩法权威状态。
- 玩家与怪物共享 `DamageRequest → DamageResult → HealthChanged` 战斗管线，六点生命配置可自动扩容血量图标。
- 怪物 AI 按 `Perception → Blackboard → Utility Decision → Action` 分层，并提供只读 Inspector 与 Scene Gizmos。
- Chomper 负责近身压迫，Spitter 通过安全距离、攻击前摇和代码驱动投射物制造远程空间压力。
- 冲刺 Ability 在有效时段持有 `State.Invulnerable` 标签，接触和投射物伤害通过统一入口校验。
- 场景包含两组位于不同区域的独立遭遇；每组三只怪物在等待阶段沿可视化点位巡逻，玩家进入后转为战斗，击败本组后只开启对应蓝色出口。
- CSV 自动导入为经过校验的只读配置资产；存档使用带版本号的原子 JSON。
- 输入适配器只暴露连续快照和离散动作命令；暂停、存档与玩家快照职责已经分离。
- 当前门禁为 60 项纯 C# 核心规格、22 项 Unity EditMode 测试和 16 项 PlayMode 运行态测试。

首个作品集版本明确不实现 Lua、完整 GAS、确定性帧同步、KCP 接入、通用对象池或全量零 GC 框架。单机玩法切片验收后冻结功能，下一阶段只在独立 `NetworkArena` 场景中实现受控的 Host 权威状态同步。

## 架构文档

- [目标架构说明](Docs/Architecture/TargetArchitecture.md)：解释应用、会话、场景和角色生命周期，以及命名、事件和状态机边界。
- [架构基线验收指南](Docs/Acceptance/ArchitectureBaselineAcceptance.md)：逐步验证编译、场景自动装配、配置、战斗和 Git 状态。
- [玩家正交状态机验收指南](Docs/Acceptance/PlayerStateRefactorAcceptance.md)：验证移动轴、动作轴、受击打断、复活与 GC 行为。
- [联网前单机闭环验收指南](Docs/Acceptance/PreNetworkSinglePlayerAcceptance.md)：从编译、测试、场景、动画、AI、存档到轻量性能采样逐项验收当前版本。
- [单机玩法切片验收指南](Docs/Acceptance/GameplaySliceAcceptance.md)：验证 Spitter、冲刺无敌、战斗遭遇与事件驱动表现。

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

## 开发环境

- Unity 版本：`2023.2.20f1c1`
- 输入系统：Input System `1.7.0`
- 动画约束：Animation Rigging `1.3.0`
- 摄像机：Cinemachine `2.10.0`
- 主要开发与演示平台：Windows

## 项目目录

```text
Assets/_Project/
├─ Code/       程序集边界内的 Core、Gameplay、Unity 与 Editor 代码
├─ Content/    场景、Prefab、动画控制器与输入动作资产
├─ Data/       设计 CSV、运行时 Resources 数据库与 Unity 配置资产
└─ Tests/      Unity EditMode 与 PlayMode 测试
```

## 第三方内容

当前原型场景引用 Unity 3D Game Kit Lite。该资源包和 TextMesh Pro 示例资产不会随公开源码重新分发。打开 `Assets/_Project/Content/Scenes/Level_01.unity` 前，需要从合法来源导入对应的原始资源包。

仓库使用 `type(scope): 中文说明` 格式的 Conventional Commits。只有相关纯 C#、Unity、中文文案和文档审计全部通过后，完整模块才会推送到远端。
