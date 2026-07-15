# Odyssey 架构基线保姆级验收指南

## 验收范围

本文用于验收 `codex/player-architecture` 分支当前已经交付的内容：

- 应用级 Bootstrap、ApplicationContext 与场景 Installer 装配。
- 玩家和怪物共用的配置绑定方式。
- 怪物使用 Gameplay `Health / DamageRequest / DamageResult`，不再直接维护公开生命整数。
- 玩家最大生命为 6，怪物战斗参数由 CSV 和运行时配置资产驱动。

玩家 Locomotion 状态机、分层 AI 和 NetworkArena 尚未包含在本阶段验收范围内。

## 一、打开正确工程

当前重构位于独立工作树：

```text
D:\Unity\Projects\Odyssey\.worktrees\player-architecture
```

不要同时让两个 Unity Editor 打开同一个工作树。若主工程仍在 Unity 中运行，可以先关闭主工程，再通过 Unity Hub 的“添加”选择上述目录。编辑器版本必须为 `2023.2.20f1c1`。

## 二、命令行自动验收

在 PowerShell 中执行：

```powershell
cd D:\Unity\Projects\Odyssey\.worktrees\player-architecture
powershell -NoProfile -ExecutionPolicy Bypass -File .\Tools\RunCoreTests.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File .\Tools\TestDocumentation.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File .\Tools\TestHumanReadableLanguage.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File .\Tools\Tests\RunDocumentationAuditSpecs.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File .\Tools\Tests\RunHumanReadableLanguageSpecs.ps1
git diff --check
```

合格结果：

- 核心规格显示“通过：38 个核心规格”或更多。
- 中文审计和架构注释审计全部显示“通过”。
- `git diff --check` 不输出任何错误。

Unity EditMode 测试：

1. 打开 `Window > General > Test Runner`。
2. 选择 `EditMode`。
3. 点击 `Run All`。
4. 合格结果为至少 15 个测试全部绿色，失败数为 0。

这一步同时证明：

- `Odyssey.Core / Gameplay / Unity / Editor` 程序集可以正常编译。
- `GameConfigBinder` 泛型约束没有命名空间冲突。
- `ApplicationContext` 与 Installer 的程序集引用正确。
- CSV DTO、运行时配置和怪物适配器的字段类型一致。

## 三、场景与组件配置验收

本阶段不需要手动挂载、拖拽或新建 Component。

打开：

```text
Assets/_Project/Content/Scenes/Level_01.unity
```

进入 Play Mode 后，在 Hierarchy 中应自动出现：

```text
[Odyssey Bootstrap]
[玩法场景安装器]
```

其中：

- `[Odyssey Bootstrap]` 使用 `DontDestroyOnLoad`，负责应用级配置和存档服务。
- `[玩法场景安装器]` 属于当前场景，负责向玩家、怪物和 SaveManager 注入依赖。
- 不应出现第二个 Bootstrap，也不应持续生成多个场景安装器。

如果没有出现：

1. 检查 Console 是否提示缺少 `Resources/Config/GameConfigDatabase`。
2. 检查运行时资产是否位于 `Assets/_Project/Data/Runtime/Resources/Config`。
3. 确认当前打开的是工作树工程，而不是主工程旧分支。

## 四、配置资产验收

打开：

```text
Assets/_Project/Data/Runtime/Resources/Config/GameConfigDatabase.asset
```

确认以下数据：

```text
player.maxHealth = 6
chomper.maxHealth = 3
chomper.attackDamage = 1
chomper.attackCooldown = 2
spitter.maxHealth = 3
```

再打开：

```text
Assets/_Project/Data/Design/Enemy.csv
```

表头必须包含：

```text
id,chaseRange,attackRange,maxHealth,attackDamage,attackCooldown
```

修改 CSV 并保存后，Unity 应自动重新导入配置。Console 应显示中文导入结果，不能出现重复 ID、非法范围或缺列异常。

## 五、玩法回归验收

进入 `Level_01` Play Mode：

1. 左上角玩家生命图标应显示 6 格。
2. 玩家接触怪物时，每次合法受伤减少 1 格。
3. 玩家攻击 Chomper，默认 3 次有效伤害后怪物死亡。
4. 同一次攻击命中怪物多个 Collider 时，若出现重复扣血，记录为后续 NonAlloc 命中去重模块的问题；当前阶段不要通过增大无敌时间掩盖。
5. 怪物死亡后 NavMeshAgent 与 Collider 应停止参与战斗，尸体按原表现消失。
6. 暂停、保存、读取、死亡和复活仍能按原方式工作。

验收过程中 Console 必须满足：

- 无红色编译错误。
- 无 `Missing Script`。
- 无 `NullReferenceException`。
- 无配置 ID 缺失。
- 无持续重复刷新的异常或警告。

## 六、Git 验收

执行：

```powershell
git status -sb
git log -5 --oneline --decorate
git branch -vv
```

合格条件：

- 当前分支为 `codex/player-architecture`。
- 工作区没有未说明的修改。
- 远端跟踪分支不存在 ahead/behind 分叉。
- 提交信息符合 `type(scope): 中文说明`。

本阶段关键提交：

```text
b3ae521 refactor(bootstrap): 建立应用与场景生命周期装配
6a87ba6 refactor(architecture): 删除未被玩法使用的会话事件空壳
b0e26a7 refactor(combat): 统一怪物配置与生命伤害管线
```

## 七、验收失败时提供的信息

如验收失败，请保留并提供：

1. Unity Console 第一条红色错误的完整堆栈。
2. Test Runner 失败测试的完整名称和消息。
3. Hierarchy 中 Bootstrap 与玩法场景安装器的截图。
4. `git status -sb` 输出。
5. 发生问题前最后执行的操作。

不要一次修改多个 Inspector 参数尝试碰运气。先根据第一条错误定位是配置、装配、领域规则还是 Unity 表现层，再进行单点修复。
