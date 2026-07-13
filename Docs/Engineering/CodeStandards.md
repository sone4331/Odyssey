# Odyssey 代码与提交规范

## 架构边界

- `Odyssey.Core` 只包含不依赖 Unity 的通用规则，例如状态机、Ability 和 Gameplay Tag。
- `Odyssey.Gameplay` 组织战斗、AI、配置和存档领域规则，不引用 UnityEngine。
- `Odyssey.Unity` 实现序列化、UI、输入、导航和其他 Unity 适配器。
- Bootstrap 是 Composition Root；角色和领域对象不得自行查找全局可变单例。
- UI 通过领域事件和 Presenter 更新，不轮询或直接修改 Gameplay 状态。
- 只有出现真实跨层边界、测试替身或第二个实现时才新增接口。

所有项目自有 C# 必须位于 `Assets/_Project/Code` 的对应程序集目录。场景与表现资产进入 `Content`，设计源数据和运行时配置进入 `Data`；禁止重新创建含义不明的 `Scripts`、`MyScripts` 或 `Generated` 根目录。

## 中文架构注释

核心生产类型的 XML `summary` 必须回答：

1. 该类型负责什么，不负责什么。
2. 使用了什么模式或边界形式。
3. 为什么该分离对测试、复用或状态一致性有价值。

复杂方法只说明事务顺序、状态不变量、生命周期和失败策略。禁止解释赋值、循环、空判断等基础语法，也禁止用教学口吻重复代码。

`Tools/TestDocumentation.ps1` 负责最低存在性门禁，不替代人工质量审查。`Tools/DocumentationAuditExclusions.txt` 只允许记录已确认、即将在对应里程碑重构的遗留文件；新增代码不得加入白名单规避审计。

Windows PowerShell 5.1 需要包含中文的 `.ps1` 使用 UTF-8 BOM；C#、Markdown、JSON、CSV 和 Unity 文本资产继续使用 UTF-8 与 LF。

## 人类可读文本

README、工程文档、XML 注释、Unity 菜单、Inspector 标题与提示、日志、异常消息、测试报告和命令行输出统一使用中文。代码标识符、第三方 API、动画状态、配置 ID、文件路径和协议字段继续使用英文，避免为了翻译破坏序列化与跨系统契约。

`Tools/TestHumanReadableLanguage.ps1` 审计明确的人类可读上下文。新增英文提示必须改为中文；测试中用于确认“英文会被门禁拒绝”的夹具属于唯一例外。

## 提交与发布

提交信息采用 `type(scope): 中文说明`：

```text
refactor(combat): 拆分玩家战斗职责
feat(ai): 增加可解释的 Utility 决策
test(save): 补充存档迁移回归测试
docs(architecture): 完善中文架构说明
```

每次提交只包含一个可解释的变更单元。模块完成后必须通过相关测试、`git diff --check` 和 Unity 编译，再推送到 `origin`；禁止 force push 和带失败测试发布。
