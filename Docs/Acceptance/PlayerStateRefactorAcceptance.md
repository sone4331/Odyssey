# 玩家正交状态机验收指南

## 验收目标

本指南用于确认玩家已从“移动与动作混合的即时切换状态机”迁移为两条正交状态轴，并且没有破坏原场景、Prefab、输入资产和动画事件引用。

验收时应同时观察玩家 Inspector 底部的“运行时状态（只读）”：移动状态只允许出现 `Grounded`、`Airborne`、`WallSlide`；动作状态只允许出现 `Free`、`Attack`、`Dash`、`Hit`。

## 一、代码与编译验收

1. 打开 Unity 工程并等待右下角编译进度结束。
2. 打开 `Window > General > Console`。
3. 点击 Console 左上角 `Clear`。
4. 确认没有红色编译错误、Missing Script 或重复类型错误。
5. 在 Project 窗口搜索 `PlayerIdleState`、`PlayerMoveState` 和 `BaseState`，结果应为零。
6. 在命令行运行：

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\Tools\RunCoreTests.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File .\Tools\TestDocumentation.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File .\Tools\TestHumanReadableLanguage.ps1
```

预期结果：核心规格至少 45 项全部通过，中文和架构注释审计全部通过。

## 二、场景与引用验收

1. 打开 `Assets/_Project/Content/Scenes/Level_01.unity`。
2. 在 Hierarchy 选择玩家 Ellen。
3. 确认 `PlayerController` 组件存在，且没有 Missing Script。
4. 确认 `Input Reader`、`Wall Layer`、`Enemy Layer` 和 `Respawn Point` 保持原引用。
5. 无需新增、拖拽或挂载任何组件；移动轴和动作轴由 `PlayerController` 在 `Awake` 中按角色生命周期创建。

## 三、移动轴验收

1. 进入 Play Mode，静止时移动状态应为 `Grounded`，动作状态应为 `Free`。
2. 使用方向键或 WASD 移动，移动状态仍应为 `Grounded`，Animator 的 Speed 应正常变化。
3. 按住跳跃键不足蓄力阈值后松开，应执行普通跳；长按超过阈值后松开，应跳得更高。
4. 起跳后移动状态应变为 `Airborne`，落地后回到 `Grounded`。
5. 空中再次按跳跃键，应只允许一次二段跳。
6. 下降时贴近 Wall Layer 墙面，应进入 `WallSlide`；离开墙面应回到 `Airborne`；按跳跃键应执行蹬墙跳。

失败判定：同一帧出现重复位移、落地后仍显示 Airborne、滑墙离墙后卡住，或一次落地可无限二段跳。

## 四、动作轴验收

1. 地面按攻击键，动作状态应从 `Free` 进入 `Attack`，移动状态保持 `Grounded`。
2. 在连击窗口继续按攻击键，应递增 ComboIndex，不应创建新的玩家状态组件或出现 Missing Method。
3. 地面冲刺时动作状态应进入 `Dash`；结束后回到 `Free`。
4. 空中冲刺时移动状态保持 `Airborne`，动作状态进入 `Dash`；同一次滞空只能冲刺一次。
5. 攻击或冲刺期间受到怪物伤害，动作状态必须进入 `Hit`，证明受击打断优先级生效。
6. 受击结束后动作状态回到 `Free`，移动状态根据实际接地情况保持 Grounded 或 Airborne。

## 五、生命与复活验收

1. 玩家初次应用导表配置后，Inspector 中最大生命与当前生命都应为 6。
2. 受到伤害后，血条减少一格，动作状态进入 `Hit`。
3. 生命归零后播放死亡表现，并在配置时间后传送到 Respawn Point。
4. 复活后当前生命恢复到最大值，两条状态轴分别回到 `Grounded` 与 `Free`。
5. 连续死亡和复活两次，不应出现重复输入、一次按键触发多次动作或 Animator speed 未恢复的问题。

## 六、性能与代码结构验收

1. 打开 Profiler 的 CPU 与 GC Alloc 模块。
2. 预热攻击、冲刺、跳跃和滑墙后连续操作 30 秒。
3. 玩家状态切换不得持续产生状态对象分配；攻击命中使用 `OverlapSphereNonAlloc`，踩踏检测使用 `SphereCastNonAlloc`。
4. 在代码中确认玩家目录只保留 `PlayerController`、`PlayerLocomotionRuntime` 和 `PlayerActionRuntime` 三个核心控制文件，不再存在旧状态类和 Legacy FSM。

本阶段不承诺所有第三方 3D Game Kit 脚本零警告；验收重点是 Odyssey 自有代码零编译错误、零持续异常和零旧状态机残留。
