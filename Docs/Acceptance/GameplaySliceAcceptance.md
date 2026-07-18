# 单机玩法切片验收指南

## 一、验收目标

本阶段把技术原型收束为可录制的最小玩法闭环：两个 Chomper 负责近身压迫，一个 Spitter 负责远程空间控制；玩家使用走位、连击和带无敌标签的冲刺完成战斗，全部敌人被击败后出口开启。

本阶段不增加 Boss、装备、技能树、任务、通用波次编辑器或全局 Manager。通过本指南后冻结单机玩法，进入独立 `NetworkArena` 联机开发。

## 二、自动搭建

1. 使用 Unity `2023.2.20f1c1` 打开工程并退出 Play Mode。
2. 等待脚本编译完成，确认 Console 没有红色错误。
3. 点击 `Odyssey → 场景 → 搭建单机玩法切片`。
4. 等待 Console 输出“单机玩法切片搭建完成”。

工具会自动完成以下工作，不需要手工拖拽：

- 从 3D Game Kit Lite 的 Spitter 美术中移除教学脚本。
- 生成仅包含 Idle、Fleeing、Attack、TopHit 的项目自有 Animator。
- 生成代码驱动投射物 Prefab。
- 在 `Level_01` 保留两个 Chomper 并放置一个 Spitter。
- 创建战斗触发区、出口、HUD、命中特效、音效与 Cinemachine 冲击源。

重复执行菜单只更新同一批资产与同名场景根节点，不会继续增加敌人或 UI。

## 三、自动化门禁

在项目根目录执行：

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\Tools\RunCoreTests.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File .\Tools\TestDocumentation.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File .\Tools\TestHumanReadableLanguage.ps1
```

预期结果为 59 项核心规格通过，中文审计与架构注释审计通过。

在 Unity Test Runner 中执行：

- EditMode：预期 22 项通过。
- PlayMode：预期 16 项通过。

新增测试重点覆盖：

- Spitter 配置从 CSV 到只读资产的完整转换。
- 远程 AI 在过近、适中和过远距离选择撤退、攻击和追击。
- 投射物忽略发射者、只伤害一次并能超时销毁。
- 冲刺期间伤害被拒绝，冲刺结束后无敌标签立即移除。
- 三名敌人全部死亡后遭遇只完成一次。
- 项目自有 Spitter、投射物和 Animator 资产引用完整。

## 四、场景结构验收

打开 `Assets/_Project/Content/Scenes/Level_01.unity`，确认 Hierarchy 中存在“玩法切片_战斗遭遇”，其下包含：

- `战斗出口`。
- `战斗目标HUD`。
- `战斗反馈`。

`Spitter_远程威胁` 与遭遇根节点并列放在场景根级，避免非零父节点影响 `NavMeshAgent` 绑定。选中遭遇根节点，确认 `CombatEncounterController` 的参与者数量为 3，且正好引用两个 Chomper 和这个 Spitter。该组件不是单例，也不直接引用 HUD、出口或音效。

选中 Spitter，确认：

- 配置 ID 为 `spitter`。
- 攻击方式只读显示为“投射物”。
- 红圈表示最大攻击距离，青圈表示 3.5 米最小安全距离。
- 投射物 Prefab、发射点和攻击前摇提示均有引用。
- Animator 关闭 Root Motion，NavMeshAgent 使用关卡已烘焙的小型怪物 Agent 类型；进入战斗后 `Is On NavMesh` 为真。

## 五、手动玩法验收

1. 进入 Play Mode 并走入战斗触发区，HUD 显示“战斗开始”和剩余敌人数。
2. Chomper 应主动近身，Spitter 应在远处显示绿色前摇后发射投射物。
3. 靠近 Spitter 到约 3.5 米以内，它应主动后撤；距离 3.5–8 米时攻击，距离更远时追击。
4. 原地承受投射物，生命应只减少一格；同一投射物不得重复伤害。
5. 在投射物即将命中时冲刺，生命不减少，并播放闪避反馈。
6. 冲刺结束后再次被命中，生命正常减少。
7. 攻击敌人时出现命中特效、音效和轻量镜头冲击，游戏时间不应被冻结。
8. 击败全部三名敌人，HUD 显示“战斗完成”，出口平滑开启且完成事件不重复触发。
9. Console 不得出现 Missing Method、Missing Script、空引用或持续异常。

## 六、玩法冻结标准

- 玩家无需文字教程也能辨认近战和远程威胁。
- 冲刺既是位移能力，也是应对远程攻击的战斗决策。
- 一段 60–90 秒录像可以完整展示进入战斗、组合威胁、闪避投射物、连击击杀和出口开启。
- 达到上述标准后，不再横向增加单机系统，后续功能只服务于 `NetworkArena` 状态同步演示。
