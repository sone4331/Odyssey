# 本地联机竞技场验收指南

## 一、实现范围

`NetworkArena` 是与单机 `Level_01` 隔离的双人技术切片，使用：

- 联机框架：Netcode for GameObjects `1.15.1`。
- 传输层：Unity Transport `1.5.0`，底层使用 UDP。
- 主机模式：Listen Server，创建房间的玩家同时承担 Host 和本地 Client。
- 连接方式：IPv4 直连，本机使用 `127.0.0.1`，局域网使用 Host 电脑的 IPv4。
- 同步权威：客户端只提交移动和攻击意图，Host 决定位置、命中、伤害、死亡与复活。

本阶段不包含公网服务器、匹配、Relay、NAT 穿透、KCP、帧同步、聊天、账号和断线重连。它们不属于“无服务器双人联机演示”的核心闭环。

## 二、零手工搭建检查

1. 使用 Unity `2023.2.20f1c1` 打开工程。
2. 等待右下角编译结束，确认 Console 没有红色错误。
3. 点击 `Odyssey → 联机 → 重新搭建本地联机场景`。
4. 打开 `Assets/_Project/Content/Scenes/NetworkArena.unity`。

场景中应存在：

- “联机运行时”：`NetworkManager + UnityTransport + NetworkSessionController + NetworkArenaHud`。
- “主摄像机”：包含 `NetworkArenaCameraFollow`。
- 地面、四面边界、中央掩体、蓝色 Host 出生点和橙色 Client 出生点。

`Assets/_Project/Content/Prefabs/Network/NetworkPlayer.prefab` 根节点应包含：

- 网络身份组件：`NetworkObject`。
- 位置同步组件：`NetworkTransform`。
- 联机角色适配器：`NetworkPlayerAvatar`。
- 权威碰撞移动组件：`CharacterController`。
- Animator 使用 Ellen 表现，Root Motion 关闭。

不需要手工挂载 Component、拖拽 Prefab 或填写 NetworkManager。

## 三、自动化验收

在 Unity 菜单依次执行：

1. `Odyssey → 测试 → 运行 EditMode 测试`
2. `Odyssey → 测试 → 运行 PlayMode 测试`

预期结果：

- EditMode：27 项通过，0 失败。
- PlayMode：19 项通过，0 失败。

网络专项重点覆盖：

- 重复/过期序号、死亡、冷却、无目标和超距离攻击会得到明确拒绝原因。
- 玩家 Prefab 只有联机切片需要的网络边界，不复用单机控制器写入位置和生命。
- `NetworkArena` 能真实启动 Host、完成连接审批并自动生成一个服务器权威玩家。
- Host 与 Client 出生点分离，场景关闭后不会遗留 `NetworkManager.Singleton` 污染单机测试。

## 四、生成 Windows 双开包

1. 退出 Play Mode。
2. 点击 `Odyssey → 联机 → 构建 Windows 双开演示`。
3. 等待 Console 输出“联机演示构建完成”。
4. 打开：

```text
Builds/NetworkArena/OdysseyNetworkArena.exe
```

该构建只包含 `NetworkArena`，不会先进入单机 `Level_01`。`Builds` 已被 Git 忽略，不会污染源码提交。

## 五、本机双开保姆级验收

### 1. 启动连接

1. 双击 `OdysseyNetworkArena.exe`，打开第一个窗口。
2. 再双击一次同一个 exe，打开第二个窗口。
3. 第一个窗口点击“创建 Host”。
4. 第二个窗口确认地址为 `127.0.0.1`，点击“加入 Client”。
5. 两边状态分别应显示“Host 运行中”和“Client 已连接”，Host 显示连接人数 `2/2`。

如果第二个窗口无法连接：

- 先关闭两个窗口，确认没有旧进程占用 UDP `7777` 端口。
- Windows 防火墙弹窗出现时，至少允许“专用网络”。
- 本机双开不要填写路由器公网地址，只填写 `127.0.0.1`。

### 2. 验证位置状态同步

1. 两个窗口左右并排摆放。
2. 在 Host 窗口按 WASD，确认 Host 角色移动，Client 窗口也能看到同一角色移动。
3. 在 Client 窗口按 WASD，确认 Client 角色移动，Host 窗口也能看到同一结果。
4. 让两个角色分别撞向边界和中央掩体，确认不能穿过碰撞体。
5. 观察远端角色移动应有插值，不应每帧大幅跳变。

通过标准：客户端从未直接同步自己的 Transform；它发送归一化输入，Host 使用 CharacterController 模拟后由 NetworkTransform 复制位置。

### 3. 验证 Host 权威攻击

1. 两个角色保持较远距离，在任意窗口按 J 或鼠标左键。
2. 面板应显示递增的“本地攻击序号”，最近请求显示“Host 拒绝：目标超出攻击距离”。
3. 面朝另一名玩家并靠近，再按一次攻击。
4. 面板应显示“Host 已接受”，对方“权威生命”减少 1。
5. 快速连续点击攻击，冷却中的请求应显示“Host 拒绝：攻击仍在冷却”。
6. 每次有效攻击只能减少 1 点生命，两个窗口观察到的生命结果必须一致。

通过标准：Client 不能提交伤害值或目标；Host 根据请求序号、存活、冷却、朝向和距离选择目标并修改生命。

### 4. 验证死亡、复活和断开

1. 连续有效攻击直到对方生命归零。
2. 两个窗口都应播放死亡表现。
3. 约 2 秒后角色在自己的出生点复活，权威生命恢复为 `5/5`。
4. 任意一端点击“断开连接”，确认会话停止且没有持续报错。
5. 关闭两个程序，重新双开一次，确认端口可再次使用。

## 六、局域网两台电脑验收（可选）

1. 两台 Windows 电脑连接同一路由器。
2. Host 电脑运行 `ipconfig`，找到当前网卡的 IPv4，例如 `192.168.1.23`。
3. Host 点击“创建 Host”。
4. Client 电脑输入 `192.168.1.23` 并点击“加入 Client”。
5. 若失败，检查两台电脑是否处于同一网段，并允许防火墙专用网络 UDP `7777`。

本方案没有 NAT 穿透，因此两台电脑跨公网时不能直接连接，这是当前范围内的明确设计边界，不是缺陷。

## 七、面试讲解顺序

1. “这是状态同步，不是帧同步；CharacterController 和浮点物理不保证确定性。”
2. “Client 只发送 Command，Host 模拟并复制 State。”
3. “移动 RPC 使用 20Hz 非可靠传输；丢失一帧输入不会阻塞后续输入。”
4. “攻击使用可靠命令和递增序号，Host 拒绝重复、过期、冷却和超距离请求。”
5. “NetworkArena 与单机玩法隔离，证明网络边界而不把整个原型改造成难以收尾的在线游戏。”
