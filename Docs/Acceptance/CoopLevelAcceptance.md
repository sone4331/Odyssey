# 原关卡双人合作联机验收指南

## 一、验收目标

本次联机没有创建独立竞技场，而是在原有 `Level_01` 中提供正常游戏式入口：

- 主菜单点击“单人游戏”：使用 NGO 原生 `SinglePlayerTransport`，不监听 UDP 端口，保留存档和全局暂停。
- 主菜单进入“联机游戏”后点击“创建房间”：本机同时作为服务器与一号玩家，监听 UDP `7777`。
- 联机页填写 Host 的局域网 IPv4 后点击“加入房间”；同一台电脑双开时填写 `127.0.0.1`。

联机最多两人。玩家移动与动画由 Owner 保持原操作手感，怪物 AI、攻击命中、生命、投射物、复活、战区进度和隔离门由 Host 决定。

## 二、构建前自动验收

### 1. 编译与 Console

1. 使用 Unity `2023.2.20f1c1` 打开工程。
2. 确认当前场景为 `Assets/_Project/Content/Scenes/Level_01.unity`。
3. 等待右下角脚本编译结束。
4. 打开 `Window → General → Console`。
5. 确认没有红色编译错误、命名空间冲突、Missing Script 或持续异常。

### 2. 重新搭建原关卡联机

退出 Play Mode，点击：

```text
Odyssey → 联机 → 搭建原关卡合作联机
```

预期 Console 输出“原关卡合作联机搭建完成”。该工具可以重复执行，不会重复增加组件、怪物、玩家或网络根节点，也不会改变已有出生位置。

重新打开 `Level_01`，Hierarchy 应包含：

- `合作联机_会话`
- `合作联机_出生点`
- `合作联机_战区状态`

场景中不应固定放置 Ellen 玩家；玩家必须在选择模式后由 NGO 生成。Build Settings 只能包含 `Level_01`。

启动 Play Mode 后还应确认：

- 出现全屏中文主菜单和可操作鼠标光标。
- Hierarchy 中只有一个 `GameMenuController`，不存在旧 `GameplaySessionHud` 或 IMGUI 启动页。
- EventSystem 使用 `InputSystemUIInputModule`。
- FreeLook Camera 的 Follow/LookAt 在选择模式前为空，生成本机玩家后自动绑定到该玩家。

### 3. 自动化测试

在项目根目录执行：

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\Tools\RunCoreTests.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File .\Tools\TestDocumentation.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File .\Tools\TestHumanReadableLanguage.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File .\Tools\Tests\RunDocumentationAuditSpecs.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File .\Tools\Tests\RunHumanReadableLanguageSpecs.ps1
```

预期结果：

- 55 项纯 C# 核心规格全部通过。
- 架构注释审计通过。
- 人类可读文本中文审计通过。

在 Unity Test Runner 中执行：

- EditMode：31 项全部通过。
- PlayMode：24 项全部通过。

网络相关测试会验证合作玩家和投射物 Prefab、六只怪物、两个战区、一个门禁、IPv4 校验、攻击拒绝原因、连击段次，以及真实 Host 启动后的 Owner、行为树和 NavMesh 生命周期。

## 三、生成 Windows 双开包

退出 Play Mode，点击：

```text
Odyssey → 构建 → 构建 Windows 可玩版本
```

等待 Console 输出“原关卡合作联机构建完成”。默认文件位于：

```text
Builds/Windows/Odyssey.exe
```

首次运行时如果 Windows 防火墙询问是否允许访问网络，请至少勾选“专用网络”。局域网联机的两台电脑必须使用相同构建版本。

## 四、本机双开保姆级验收

### 1. 建立连接

1. 双击两次 `Odyssey.exe`，打开两个独立窗口。
2. 左侧窗口点击“联机游戏”，再点击“创建房间”。
3. 等待左侧生成蓝色玩家。
4. 右侧窗口点击“联机游戏”，在 `IP 地址` 输入框填写 `127.0.0.1`。
5. 点击“加入房间”。
6. 等待右侧生成橙色本机玩家；两个窗口都应看到两名玩家。
7. 两个窗口按 F3，确认房主显示 `2/2`，加入方显示已连接和 RTT；再次按 F3 隐藏面板。

如果 Client 无法连接：先确认 Host 已启动，再检查 UDP `7777` 是否被其他程序占用，并允许程序通过 Windows 防火墙。

### 2. 玩家同步

分别聚焦两个窗口并依次验证：

1. WASD 走跑、转向、跳跃和落地动画。
2. 冲刺后直接恢复最大奔跑速度。
3. 墙滑、墙跳和踩踏原玩法。
4. 四段连击和攻击恢复。
5. 两名玩家相互穿过时不发生身体阻挡。
6. 玩家攻击队友时不扣除队友生命。

远端角色可以有轻微插值延迟，但不能瞬移、持续抖动、读取本机输入或出现 Animator 卡死。

### 3. Host 权威战斗

1. 两名玩家一起接近第一战区。
2. 确认 Chomper 会选择较近的存活玩家追击；近战攻击开始后约 0.45 秒内由 Host 结算伤害，两端生命同步且无一秒以上的明显延迟。
3. 确认 Spitter 会追击、保持安全距离、后撤并发射同步投射物。
4. 任意一端受到同一次攻击后，两个窗口显示的该玩家生命必须一致。
5. 冲刺激活期间让近战或投射物命中，Host 应拒绝伤害；冲刺结束后恢复可受伤。
6. 连续攻击怪物，两个窗口中的受击、死亡和剩余数量必须一致。
7. 按 F3 打开右上角调试面板，确认显示模式、连接人数、RTT 和权威生命。

客户端不能提交目标或伤害值。超距离攻击、重复序号、非法连击段次、冷却中请求都必须由 Host 拒绝。

### 4. 战区和门禁

1. 第一战区尚有怪物时踩踏板，隔离门不得打开。
2. 一名玩家站在踏板上，另一名玩家击杀最后一只怪物，门仍不得自动打开。
3. 两名玩家都离开踏板。
4. 任意一名玩家重新踩入踏板，门应在两个窗口同步开启。
5. 进入第二战区并继续合作清怪，第一战区状态不得重置。

### 5. 死亡、断线和重新加入

1. 让 Client 玩家生命归零。
2. 只允许该玩家播放死亡并在延迟后独立复活；Host 玩家、怪物和战区不能重置。
3. 关闭 Client 窗口，确认 Host 可以继续移动和战斗。
4. 再次启动一个相同构建，使用 `127.0.0.1` 加入。
5. 新 Client 应获得当前剩余怪物、战区完成状态和门状态，而不是回到初始关卡。
6. 关闭 Host，Client 应结束旧会话并回到主菜单。
7. 可选：尝试打开第三个 Client，必须收到“合作房间已满，最多支持两名玩家”的中文拒绝原因。

## 五、单机回归验收

重新启动一个窗口并选择“单人游戏”：

1. 系统不应占用 UDP `7777`。
2. 原玩家移动、动画、AI、战区和门禁全部可用。
3. 按 ESC 打开菜单后整个单机游戏暂停，`Time.timeScale` 变为零；再次按 ESC 继续。
4. 保存后改变位置与生命，再读取存档，玩家快照应正确恢复。
5. 联机模式按 ESC 只关闭本机玩家和镜头输入，另一窗口、AI 与 Host 模拟继续运行；菜单中不显示保存和读取。
6. 设置页只包含主音量、镜头灵敏度和全屏，修改后立即生效并在重启后保留。

## 六、最终通过标准

- Console 无编译错误、Missing Script、破损 Prefab 和持续异常。
- 两个窗口都能看见相同的玩家、怪物、投射物、生命、战区和门状态。
- Client 不能直接决定命中、伤害、生命、AI 或开门。
- Client 断开后 Host 可继续，重新加入能获得当前关卡快照。
- 单机保留原存档和暂停，联机菜单不修改全局时间。
- 仓库和 Build Settings 不再包含独立联机场景或对应专属资源。
