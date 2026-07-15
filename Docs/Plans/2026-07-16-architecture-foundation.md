# 架构基础实现计划

> **执行要求：** 使用 `superpowers:executing-plans` 按任务顺序执行；所有行为变更遵循测试驱动开发。

**目标：** 固化 Odyssey 的生命周期与命名约定，并实现可供玩家、AI、UI 和网络共享的会话级强类型事件总线。

**架构：** EventBus 位于无 Unity 依赖的 `Odyssey.Core`，只负责同一会话内的类型化通知。订阅令牌控制生命周期，发布采用稳定快照，订阅变化从下一次发布生效；多个订阅者异常在全部调用完成后统一抛出。

**技术栈：** C#、Unity 2023.2、Odyssey.Core、项目自有规格测试器、PowerShell 质量门禁。

---

### 任务一：记录架构决策

**文件：**

- 新增：`Docs/Architecture/TargetArchitecture.md`

- [x] 记录 Application、Session、Scene、Actor 四种生命周期。
- [x] 记录 Bootstrap、Installer、Controller、Service、System、Presenter、Provider、Adapter 的命名契约。
- [x] 记录受控单实例、依赖方向、事件边界和状态机迁移原则。
- [x] 运行中文与架构文档审计，预期全部通过。

### 任务二：先写事件总线失败规格

**文件：**

- 新增：`Tests/Core/EventBusSpecs.cs`
- 修改：`Tests/Core/Program.cs`

- [x] 增加“按载荷类型通知订阅者”规格。
- [x] 增加“释放订阅后不再接收事件”规格。
- [x] 增加“发布期间修改订阅从下一次生效”规格。
- [x] 增加“异常订阅者不阻断其他订阅者”规格。
- [x] 增加“释放总线后拒绝订阅和发布”规格。
- [x] 运行 `Tools/RunCoreTests.ps1`，确认编译因 `GameEventBus` 尚不存在而失败。

### 任务三：实现最小事件总线

**文件：**

- 新增：`Assets/_Project/Code/Core/Events/IGameEventBus.cs`
- 新增：`Assets/_Project/Code/Core/Events/GameEventBus.cs`

- [x] 定义以下最小接口：

```csharp
public interface IGameEventBus : IDisposable
{
    IDisposable Subscribe<TEvent>(Action<TEvent> handler);
    void Publish<TEvent>(TEvent eventData);
}
```

- [x] 使用 `Dictionary<Type, List<ISubscription>>` 保存类型化订阅，不使用反射扫描。
- [x] `Publish` 对当前订阅集合创建快照，使本轮调用顺序稳定。
- [x] 每个订阅者独立执行；全部执行后以 `AggregateException` 报告异常。
- [x] `Dispose` 清空订阅并使后续订阅、发布抛出 `ObjectDisposedException`。
- [x] 为接口、实现、订阅令牌和复杂发布方法补充中文架构注释。
- [x] 运行核心规格，预期全部通过。

### 任务四：质量门禁与提交

- [x] 运行核心规格、中文审计、架构注释审计及其自测试。
- [x] 运行 `git diff --check`，预期无空白错误。
- [ ] 提交 `feat(events): 增加会话级强类型事件总线`。
- [ ] 推送 `codex/player-architecture`，作为后续玩家迁移的稳定基线。
