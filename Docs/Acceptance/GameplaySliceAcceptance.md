# 单机玩法切片验收指南

## 一、验收目标

当前切片在地图不同位置配置两组敌人，每组包含两个 Chomper 和一个 Spitter。怪物从场景启动起自主巡逻，与战区状态无关；感知玩家后追击或调整距离，玩家离开丢失范围后恢复巡逻。

第一战区隔离门必须满足“全部怪物死亡，并且玩家在清怪后重新踩入原关卡踏板”。本阶段不增加 Boss、装备、技能树、任务、通用节点编辑器或全局 Manager。

## 二、自动搭建

1. 使用 Unity `2023.2.20f1c1` 打开工程并退出 Play Mode。
2. 等待脚本编译完成，确认 Console 没有红色错误。
3. 点击 `Odyssey → 场景 → 搭建单机玩法切片`。
4. 等待 Console 输出“单机玩法切片搭建完成”。

工具会自动完成：

- 从 3D Game Kit Lite 的 Spitter 美术中移除教学脚本，生成项目自有 Animator 与投射物 Prefab。
- 在两块区域各配置 `2 Chomper + 1 Spitter`。
- 为每组建立六个经过 NavMesh 采样的共享宽范围巡逻点，三只怪物使用不同起始索引。
- 删除旧蓝色测试板和战区触发器，不再用战区状态控制 AI。
- 精确接管 `ExampleLevel/Mechanism1/PressurePad` 与 `ExampleLevel/FinalBuilding/DoorHuge`，配置项目自有清怪门禁。
- 配置 HUD、命中特效、音效与 Cinemachine 冲击源。

重复执行菜单只更新同一批资产与场景根节点，不会继续增加敌人或 UI；巡逻网络跨度不足 12 米、点位过近或门禁资源缺失时，工具会以中文错误中止而不保存半成品。

## 三、自动化门禁

在项目根目录执行：

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\Tools\RunCoreTests.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File .\Tools\TestDocumentation.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File .\Tools\TestHumanReadableLanguage.ps1
```

预期为 55 项核心规格、中文审计和架构注释审计全部通过。

在 Unity Test Runner 中执行：

- EditMode：32 项全部通过，其中包含原关卡联机静态装配与 Host 攻击规则。
- PlayMode：20 项全部通过，其中包含单机无端口传输和真实 Host 启动冒烟测试。

重点覆盖：

- 行为树节点生命周期与响应式抢占。
- 巡逻、追击、攻击、目标丢失回巡逻和 Spitter 近距离后撤。
- 代码前摇只结算一次近战或投射物伤害。
- 两组战区从开局独立统计，六点巡逻网络跨度不少于 12 米。
- 清怪前踩板不开门、站在板上清怪不开门、清怪后重新踩入才开门。
- 场景无 Missing Script，Spitter、投射物、Animator 和门禁引用完整。

## 四、场景结构验收

打开 `Assets/_Project/Content/Scenes/Level_01.unity`，确认存在“玩法切片_战斗遭遇”，其下包含“第一战斗组”和“第二战斗组”。

每个战斗组应包含：

- 一个 `CombatEncounterController`，参与者正好为两个 Chomper 和一个 Spitter。
- 一个“共享宽范围巡逻网络”，内部有六个巡逻点。
- 战斗目标 HUD 与战斗反馈。

场景中不应再出现项目生成的蓝色战斗出口或玩家进入触发区。主流程 PressurePad 上应有且仅有一个 `EncounterClearancePressurePlate`，FinalBuilding DoorHuge 上应有且仅有一个 `EncounterClearanceGate`；原第三方发送命令与门体平移组件必须从这两个对象中移除，避免双重控制。

选中任意怪物，Inspector 只读区域应显示：

- 当前行为与中文行为树路径。
- 是否已感知玩家、目标距离和生命比例。
- 攻击方式、最小安全距离、巡逻路线和当前巡逻点。

Scene 视图中黄色圆为发现范围、橙色圆为丢失范围、红色圆为攻击范围；Spitter 额外显示青色最小安全距离。

## 五、手动玩法验收

1. 进入 Play Mode，不靠近第二战区，确认第二组也已经沿较大范围路线巡逻。
   - 六只怪物都必须产生实际位置变化，不能只播放跑步动画。
   - 怪物 Animator 的 Root Motion 必须关闭，NavMeshAgent 的位置与旋转同步必须开启。
2. 进入 Chomper 黄色感知范围，确认它停止巡逻、持续追击并在近距离实际扣血。
3. 离开橙色丢失范围，确认怪物放弃目标并返回巡逻。
4. 观察 Spitter：距离过远时追击、处于安全射程时显示绿色前摇并攻击、距离过近时后撤。
5. 原地承受投射物，生命只减少一格；冲刺命中时伤害被无敌标签拒绝。
6. 清怪前踩第一战区踏板，确认隔离门不开启。
7. 站在踏板上击杀最后一只怪物，确认门仍不开启。
8. 离开踏板后重新踩入，确认原 DoorHuge 平滑开启。
9. 检查 Console，不得出现 `Animator.GotoState`、Missing Method、Missing Script、空引用或持续异常。

## 六、通过标准

- 玩家无需解释即可辨认近战追击与远程空间威胁。
- 行为树结构、当前运行路径和打断原因可以在面试中直接展示。
- 一段 60–90 秒录像可以展示巡逻、感知、追击、远程闪避、清怪和踏板开门闭环。
- 当前切片已经冻结，并直接在同一个 `Level_01` 上增加双人 Host 权威合作玩法，不再维护独立联机场景。
