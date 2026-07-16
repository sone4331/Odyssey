# 联网前单机闭环验收指南

## 一、验收范围

本指南只验收进入网络开发前必须稳定的单机能力：工程编译、配置装配、玩家移动与动作、Animator 连贯性、血量 UI、暂停与存档、分层怪物 AI、自动化测试和轻量性能采样。

本阶段不验收 Lua、KCP、帧同步、状态同步、Addressables 全量迁移、通用对象池或“主循环绝对零分配”等尚未实现或不适合当前 Demo 规模的能力。

## 二、编译与静态门禁

1. 使用 Unity `2023.2.20f1c1` 打开项目。
2. 等待右下角资源导入和脚本编译结束。
3. 打开 `Window → General → Console`，确认没有红色编译错误。
4. 在项目根目录依次运行：

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\Tools\RunCoreTests.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File .\Tools\TestDocumentation.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File .\Tools\TestHumanReadableLanguage.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File .\Tools\Tests\RunDocumentationAuditSpecs.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File .\Tools\Tests\RunHumanReadableLanguageSpecs.ps1
```

预期结果：

- 显示“通过：51 个核心规格”。
- 中文审计和架构注释审计全部通过。
- `git diff --check` 没有空白错误。

## 三、Unity 自动化测试

### 编辑模式（EditMode）

1. 点击 `Odyssey → 测试 → 运行 EditMode 测试`。
2. 等待 Console 输出测试摘要。

预期结果：17 项通过、0 项失败。重点覆盖 Ability、Tag、伤害、配置导入、Animator 结构、动态血量图标、暂停状态和目录完整性。

### 运行模式（PlayMode）

1. 确认当前没有处于 Play Mode。
2. 打开 `Window → General → Test Runner`，切换到 `PlayMode` 页签。
3. 点击 `Run All`。测试会自动进入 Play Mode、装载 `Level_01` 并在结束后退出。

预期结果：3 项通过、0 项失败。三项分别验证：

- Bootstrap 应用六点生命配置并生成六个生命图标。
- 暂停门面同时更新面板和 `Time.timeScale`。
- 怪物在真实运行时依次进入追击、攻击和低生命撤退目标。

## 四、场景与配置验收

1. 打开 `Assets/_Project/Content/Scenes/Level_01.unity`。
2. 确认 Build Settings 中只有该场景且处于启用状态。
3. 点击 `Odyssey → 配置 → 导入并校验全部 CSV`。
4. 选中 `Assets/_Project/Data/Runtime/Resources/Config/GameConfigDatabase.asset`。
5. 确认玩家 `maxHealth` 为 6。
6. 选中层级中的 `Ellen`，确认：

   - `InputReader` 已引用 `PlayerInputReader.asset`。
   - `EnemyLayer` 包含怪物层。
   - `WallLayer` 包含第 16 层的关卡碰撞体。
   - `RespawnPoint` 引用有效。

7. 确认 Console 中没有 Missing Script、缺失引用或持续异常。

## 五、玩家与 Animator 手动验收

进入 Play Mode 后按以下顺序操作：

1. 使用 `W/A/S/D` 移动，观察待机与移动 BlendTree 平滑变化，不应重复重播移动状态。
2. 短按并释放空格执行普通跳跃；按住超过约 0.3 秒再释放执行蓄力跳。
3. 在空中再次按空格执行一次空中跳跃。
4. 靠近关卡墙面下落，确认进入墙滑；按空格确认墙跳离开墙面。
5. 按左 `Shift` 执行冲刺，确认冲刺结束后能恢复正确的地面或空中动画，Animator 全局速度保持 1。
6. 连续点击鼠标左键执行四段连击：

   - 每段动作使用短交叉淡化，输入响应不应等待旧 Any State 的 Exit Time。
   - 连击段之间不应突然回到待机。
   - 最后一段结束后应恢复到当前移动状态。

7. 接触怪物触发受击，确认受击可以打断攻击或冲刺，并在结束后恢复移动动画。
8. 生命归零后确认播放死亡表现，约 3 秒后在 `RespawnPoint` 复活并恢复六点生命。

## 六、血量 UI 验收

1. 进入 Play Mode，确认左上角 `HeartContainer` 下有六个激活图标。
2. 第六个运行时对象名称应为“生命图标 6”。
3. 受伤后只改变对应图标 Sprite，不继续创建新图标。
4. 退出并再次进入 Play Mode，图标总数仍为六个，不应递增。

## 七、暂停与存档验收

1. 按 `P` 或 `Escape`，确认暂停面板显示、游戏时间停止、鼠标解锁并显示。
2. 点击继续或再次按暂停键，确认面板隐藏、时间恢复、鼠标重新锁定。
3. 移动到明显位置并受到一次伤害，然后暂停并点击保存。
4. 移动到另一位置或继续受伤，再点击读取。
5. 确认玩家位置和生命恢复到保存时状态，且 Console 输出中文成功信息。
6. 在文件系统中确认 `Application.persistentDataPath/SaveData.json` 存在；同目录不应长期残留临时写入文件。

## 八、怪物 AI 验收

1. 在非 Play Mode 选中任意 `Chomper`。
2. Scene 视图应显示黄色追击范围和红色攻击范围。
3. 进入 Play Mode 后观察 Inspector 的只读区域：当前目标、Utility 分数、目标距离和生命比例会持续刷新。
4. 远离怪物时目标为 `Idle`；进入黄色范围后变为 `Chase`；进入红色范围且冷却完成后变为 `Attack`。
5. 怪物剩余 1/3 生命且玩家仍在追击范围内时变为 `Retreat`。
6. 受击与死亡属于事件打断，应优先于上述常规 Utility 目标。

## 九、轻量性能验收

本项目只做与作品集规模相称的性能冒烟检查，不把一次 Editor 采样包装成正式商业性能报告。

1. 进入 Play Mode，等待 5 秒完成资源和对象池预热。
2. 打开 `Window → Analysis → Profiler`，只观察 CPU、GPU 和 Memory。
3. 在普通场景静止和战斗各采样约 10 秒。
4. 目标：

   - 普通场景稳定达到 60 FPS。
   - CPU 主线程不存在持续的长帧尖峰。
   - 玩家移动、连击和四个怪物决策不会每帧创建状态对象或物理查询数组。
   - 若 Editor 显示 GC 分配，先通过调用栈区分编辑器、MCP、第三方资源与项目自有代码，不声称“绝对零 GC”。

本次 MCP Editor 采样的参考值为 CPU 帧约 `5.77 ms`、主线程约 `2.65 ms`、GPU 约 `0.84 ms`。该数值只证明当前场景有充足的 60 FPS 余量，最终简历数据应以独立 Windows Development Build 重新采集。

## 十、最终通过标准

- Unity Console 无编译错误、Missing Script 和项目自有持续异常。
- 51 项核心规格、17 项 EditMode、3 项 PlayMode 全部通过。
- 六点生命配置、六格血条、墙滑/墙跳配置、暂停存档和 AI 四目标均可实际复现。
- Animator 的移动、跳跃、连击、冲刺、受击、死亡与复活衔接连贯。
- 场景验证结果为 0 个 Missing Script、0 个破损 Prefab。
- 轻量性能采样满足 60 FPS 目标，不提交夸大的零 GC 或商业级性能结论。
