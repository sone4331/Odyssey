# Odyssey

Odyssey 是一个面向 Unity 玩法客户端岗位的作品集项目，重点展示角色控制、动作战斗、可解释 AI、数据驱动、自动化测试、性能分析和 Host 权威联机能力。

## 当前状态

项目的工业化基线已经可以实际运行：Core、Gameplay、Unity 与 Editor 程序集边界已经建立；玩家与怪物战斗共享 Health/Damage 契约，玩家动作使用轻量 Ability；配置从 CSV 导入并生成经过校验的运行时资产；存档使用带版本号的原子 JSON；血量 UI 通过领域事件更新。

后续里程碑依次为玩家职责收敛、分层 Utility AI、性能证据和独立的 Host 权威 NetworkArena。首个作品集版本明确不实现 Lua、完整 GAS、确定性帧同步、KCP 接入和通用构建框架。

## 架构文档

- [目标架构说明](Docs/Architecture/TargetArchitecture.md)：解释应用、会话、场景和角色生命周期，以及命名、事件和状态机边界。
- [架构基线验收指南](Docs/Acceptance/ArchitectureBaselineAcceptance.md)：逐步验证编译、场景自动装配、配置、战斗和 Git 状态。

## 本地质量门禁

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\Tools\RunCoreTests.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File .\Tools\TestDocumentation.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File .\Tools\TestHumanReadableLanguage.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File .\Tools\Tests\RunDocumentationAuditSpecs.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File .\Tools\Tests\RunHumanReadableLanguageSpecs.ps1
```

项目自有生产类型使用中文 XML 摘要说明职责、设计模式和设计原因。已确认的遗留控制器记录在 `Tools/DocumentationAuditExclusions.txt`，对应里程碑重构完成时必须同步移出白名单。

## 开发环境

- Unity 版本：`2023.2.20f1c1`
- 输入系统：Input System `1.7.0`
- 摄像机：Cinemachine `2.10.0`
- 主要开发与演示平台：Windows

## 项目目录

```text
Assets/_Project/
├─ Code/       程序集边界内的 Core、Gameplay、Unity 与 Editor 代码
├─ Content/    场景、Prefab、动画控制器与输入动作资产
├─ Data/       设计 CSV、运行时 Resources 数据库与 Unity 配置资产
└─ Tests/      Unity EditMode 测试
```

旧玩家状态机暂存于 `Code/Unity/Legacy/FSM`，仅服务当前玩家迁移；玩家切换到纯 C# 延迟提交状态机后必须删除该目录。

## 第三方内容

当前原型场景引用 Unity 3D Game Kit Lite。该资源包和 TextMesh Pro 示例资产不会随公开源码重新分发。打开 `Assets/_Project/Content/Scenes/Level_01.unity` 前，需要从合法来源导入对应的原始资源包。

## 仓库公开策略

本地“架构学习文档”目录包含对外部商业项目的学习笔记，因此不会进入公开作品集，也不会被描述为 Odyssey 的本人实现。

仓库使用 `type(scope): 中文说明` 格式的 Conventional Commits。只有相关纯 C#、Unity、中文文案和文档审计全部通过后，完整模块才会推送到远端。
