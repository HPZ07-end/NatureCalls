# M1 Multiplayer Playground

本次语义授权：2026-10-19 用户「Multiplayer Playground Initialization」，见根 [GLOSSARY](../GLOSSARY.md)。本文件描述技术实现与验证方式，不重新定义游戏规则。工程仍用 Unity **6000.3.26f1**，游戏版本不提升（`0.1.0-alpha.1`）。

## 环境与依赖

| Package | 固定版本 |
| --- | --- |
| Universal RP | 17.3.0（随当前 Editor 提供） |
| Input System | 1.20.1 |
| Netcode for GameObjects | 2.13.3 |
| Multiplayer Services | 2.4.1 |
| Cinemachine | 3.1.7 |
| Multiplayer Play Mode | 2.0.2 |

完整传递依赖以 `Packages/packages-lock.json` 为准。选择依据为 Unity Registry 的稳定版本、最低 Editor 版本和本机实际包解析；NGO 3.x 要求更新的 Editor，因此没有升级。全部使用官方包，不实现替代传输或摄像机框架。

## 打开和重建

先在 Unity Hub 中打开仓库根目录对应的 NatureCalls 工程。完整流程从 **Editor 的 Project 面板**双击 `Assets/Scenes/DressingRoom.unity` 开始（构建后的游戏也从这里启动，见下方「Lobby 流程」）；只想测试厕所地图时可直接双击 `Assets/Scenes/Playground.unity` 后 Play。不是在 Finder 中双击场景文件来启动工程。在 Playground 中点击 Game 视图取得键盘焦点后，WASD 移动、Space 跳跃、鼠标观察、C 切换姿态、靠近卷纸后 E 拾取、R 向前丢掷携带中的卷纸、靠近隔间门或测试马桶后 E 交互。跳跃只允许在站立且着地时触发。隔间门打开时 E 关闭；关闭时隔间外 E 打开、隔间内 E 上锁；锁住后隔间外操作被拒绝、隔间内 E 解锁并打开。卷纸交互优先于门和马桶交互，每名玩家同时最多携带一个卷纸；丢掷后卷纸恢复物理运动并可再次拾取，当前不提供使用或消耗规则。当前 Player 使用导入的 `Peasant Nolant Brown` 嵌套 Prefab及 URP/Lit 兼容材质，离线练习无需连接 Unity Cloud。已有 Smoke/Sandbox 场景保留。

`python3 scripts/unity.py playground`（Windows：`py -3 scripts/unity.py playground`）通过 Editor API **重建原型 Playground、Player/Toilet Prefab 和原型材质**，只在明确需要恢复生成场景时运行；不要用它覆盖人工修改的原型。普通队友使用已提交场景即可，不需要补接组件。脚本保存原 GUID、不改旧场景；Playground 不在构建列表时追加，不改变 DressingRoom 作为启动场景。

`python3 scripts/unity.py lobby-flow`（或菜单 `NatureCalls/Build Lobby Flow`）生成 Lobby 流程资源：外观/地图目录与 `LobbyCanvas` Prefab（已存在则保留）、`Network`/`LobbyRoom` Prefab、DressingRoom 中的界面与镜子/门接线，以及构建列表 DressingRoom → Playground → Smoke。

## 组件边界

- `Player/Posture`：可供交互系统调用的姿态切换与通知；不依赖摄像机。
- `Player/Movement`：CharacterController、角色朝向、俯仰、姿态下的身体与碰撞体调整。
- `Input/PlayerInput`：Input System 将本地键鼠转化为移动、跳跃、观察、姿态与交互调用。
- `Camera/CameraController`：唯一实际 Camera + CinemachineBrain，两个 CinemachineCamera 的优先级和 Blend；仅绑定本地角色。
- `Facility/Toilet`：距离检测、姿态 Anchor 与内部可见目标；没有正式设施占用或冲水状态。
- `Facility/StallDoor`：驱动 WCStall Prefab 的 WC 门扇与独立碰撞体，依据玩家位于隔间内外处理打开、关闭、上锁和解锁。
- `Facility/PlanarMirror`：为每个 WC 镜面实例创建独立运行时 RenderTexture；第一人称主摄像机隐藏本地身体时，镜面摄像机仍可渲染本地 Brown 角色。
- `Facility/PaperRoll`：检测附近玩家并记录拾取者；拾取后随玩家保留并关闭场景渲染与碰撞，R 丢掷后恢复渲染、碰撞及 Rigidbody 运动。
- `Network/Session`：官方 MPS Session + Relay 的创建/加入/离开、匿名认证与错误状态；无需云端的 NGO 直连；Lobby 模式下只分配槽位并生成 LobbyRoom。跨场景保留，场景自带的重复对象自毁。
- `Network/LobbyRoom`：房间成员、准备、外观与阶段（等待/选图/对局），地图加载完成后统一生成玩家。
- `Network/Avatar`：Owner 本地控制、NetworkTransform、姿态 NetworkVariable、出生颜色、外观编号与镜头绑定生命周期。
- `Map/Playground`：原型资源引用、4 个出生位置与离线练习（联机对局时不生成离线玩家）。
- `Map/MapCatalog`、`Map/Match`：可选地图与本局模式、场景切换。
- `UI/Lobby`：`LobbyView`（界面引用）、`LobbyController`（换装面板、Lobby、选图行为）、`BackgroundBlur`（背景虚化）。
- `Editor/Playground`：用 Unity API 创建场景、材质、URP 设置和 Prefab。

保留原逻辑目录，不建立同义的 Networking/Environment 体系。新增一个 `NatureCalls.Runtime` 总程序集是为了让 Editor 和测试能引用实际组件；没有逐模块拆程序集或提前提取 Core。

## Lobby 流程

1. 打开 `DressingRoom` 并 Play。走到镜子前按 E 打开换装面板，点选外观预览，「确认」保存（写入本机 PlayerPrefs），「取消」恢复。
2. 走到镜子左侧的门按 E：背景虚化，出现联机大厅。
   - **单人离线测试**：直接进入地图选择，选地图或「随机地图」后加载该地图，本地玩家穿着所选外观。
   - **创建房间**：已关联 Unity Cloud 时创建 Relay 房间并显示 Join Code；未关联时开启局域网房间，房间号即房主局域网地址（端口 7777）。
   - **加入房间**：输入 Join Code，或房主的局域网 IP（同一台机器用 `127.0.0.1`）。
3. 房间内非房主点「准备」，房主在 2–4 人且其他人都已准备时点「开始游戏」，随后房主选择地图，其他人等待；所有人加载地图后生成角色，外观同步给其他玩家。
4. 对局中按 Esc，点「返回准备房」离开房间并回到 DressingRoom。房主离开时客户端回到准备房并显示原因。

本机双开：用 Multiplayer Play Mode 启用 Player 2，或一个 Editor + 一个 Development 构建；一端创建房间，另一端加入 `127.0.0.1`。

### 替换界面与美术

界面是 `Assets/UI/Lobby/LobbyCanvas.prefab`（uGUI），行为与外观分离：

- 改样式：在 Prefab 中替换 Image 的 Sprite、Text 的字体/字号/颜色、调整位置与布局即可；`LobbyView` 上的引用保持指向对应对象。外观与地图选项按钮在运行时从 `OutfitOption` / `MapOption` 模板复制，只需美化模板。
- 换背景虚化：编辑 `Assets/UI/Lobby/LobbyBlur.asset`（URP Depth of Field）或 Prefab 中 `Dim` 的颜色。
- 换外观或地图列表：编辑 `Assets/Player/OutfitCatalog.asset`、`Assets/Map/MapCatalog.asset`；新地图场景需包含 `Playground` 组件和出生点，并加入构建列表。
- 不需要改 C#。只有新增按钮或信息时才在 `LobbyView` 加引用、在 `LobbyController` 加行为。重新运行 `lobby-flow` 不会覆盖已存在的 `LobbyCanvas.prefab`、外观/地图目录和 Blur 配置，只刷新引用；确实要按代码重新生成界面时，先删除该 Prefab。

## 镜头

第一人称使用眼部目标、Hard Lock To Target 和 Rotate With Follow Target。第三人称使用居中的跟随目标与 Cinemachine Third Person Follow，内置障碍物处理只检测世界层。原型玩家在 Ignore Raycast 层，墙壁/设备仍有物理碰撞。

两台 Cinemachine Camera 只是虚拟镜头，每个进程只有一个实际 Camera/AudioListener。切换使用 EaseInOut Blend；离开的目标保持原姿势，避免 E 对齐或站起时瞬间反转。第一人称本地 Body Renderer 隐藏，第三人称移出身体后恢复；不修改共享资产、物理墙体或其他玩家渲染状态。

2026-10-30 按用户反馈改为正后方居中近景：Third Person Distance **1.6**、Third Person Height **0.8**、Third Person Pitch **50°**、Shoulder Offset **0**。距离是 Cinemachine 沿观察方向的距离，高度是跟随目标高度；保留俯角以看见马桶内部。Shoulder Offset 是横向偏移参数，不表示把摄像机绑在人物肩膀上；当前默认没有左右偏移。

Inspector 参数位于场景 `Local camera`：Eye Height、Third Person Distance/Height/Pitch、Shoulder Offset、Transition Duration、Camera Collision Layers/Radius。当前没有墙体全局隐藏。C 可在任意位置测试姿态；要验证马桶内部构图，请走到马桶附近按 E。

旧场景需要应用上述参数时，可以运行菜单 `NatureCalls/Center Playground Camera`；关闭 Editor 后也可用 `python3 scripts/unity.py camera`（Windows 用 `py -3`）。这只保存 Playground 的四个镜头参数，不重新生成地形、Prefab 或材质。已更新的仓库场景无需再次运行。

## 本地双客户端（无需 Unity Cloud）

以下是直接打开 Playground 的开发诊断方式，不经过 Lobby。

1. 打开 Playground，选择 Window → Multiplayer → Multiplayer Play Mode，启用 Player 2，再进入 Play。也可使用一个 Editor + 一个独立 Development Player，或两个独立 Player。
2. 两端 Esc 打开菜单，展开 `Local / LAN test tools (no Relay)`。
3. 主端点 `Local Host`；另一端地址 `127.0.0.1`，点 `Local Join`。不同机器改为主端局域网 IP，保持端口 7777；如系统询问防火墙/本地网络权限，由操作者决定授权。
4. 点击各自游戏画面操作，只控制该端角色。切换窗口先 Esc；失焦会释放鼠标。C 测姿态，E 在马桶附近对齐。角色为不同颜色。
5. `Leave` 断开；回到 `Offline practice` 可继续单机。Host 退出不迁移主机，Client 获得断开反馈。

直接连接使用官方 NGO/UnityTransport，是无云端诊断路径，不提供 Join Code、互联网穿透或 Session 服务。仅本机双进程已自动验证；MPPM GUI、真实 LAN 和 Windows 组合仍需人工验收。MPPM 的 Library/虚拟玩家数据不进入 Git。

## Relay / Join Code 的最少人工配置

当前用户没有 Unity Cloud 项目，仓库未填写或伪造 Project ID。已有 Hub 登录/Editor 许可不等于已创建 Cloud 项目。

1. 团队在 Unity Cloud 中创建/选择共同项目及组织；登录、组织权限、服务条款和可能的计费启用由人类负责人处理。
2. Editor → Edit → Project Settings → Services 关联该 Cloud Project；按 Dashboard 提示启用 Multiplayer 所需的 Relay/Lobby，确保 Authentication 可匿名登录。双方使用同一个项目和环境；场景 `Session.EnvironmentName` 默认 `production`。
3. 关联后通过 Editor 保存必要项目设置，检查 diff 只包含共享标识与预期设置；不提交 Token、账号缓存或密钥。所有构建需重新生成以包含项目标识。
4. Host 选择 `Create Host Session`，把显示的 **Session join code** 发给队友；Client 输入后 `Join Session`。不是手工创建 Relay allocation 或复制底层 Relay Code。
5. 分别测试 Mac Host → Windows Client 和 Windows Host → Mac Client，记录同一 Git SHA、操作、错误及退出结果。

代码通过 MPS `CreateSessionAsync(...WithRelayNetwork())` / `JoinSessionByCodeAsync` 使用官方连接生命周期；不同时手动 StartHost 干预 SDK。每进程使用独立匿名认证 profile，便于同机测试；不是正式账号系统。未关联 Cloud 会显示原因并保留离线角色；空 Code 会提示输入。真实服务权限、配额、过期 Code、服务端异常仍需关联后验收。

## 验证状态与限制

实际日志及测试结果见 [M1 验收记录](records/m1-2026-10-20.md)，2026-10-30 居中镜头回归见 [开发记录](records/status-log.md)。本机通过与待验收项分别列出；直连不能证明 Relay 可用。

原型以可调镜头验证为目标，没有设施占用管理，多个玩家可能同时使用同一 Anchor；不要把它当成正式如厕系统。Owner-authoritative 移动只用于 M1 响应速度，未来设施、Tidiness 与胜负状态走 Host 权威。没有 Host 迁移、完整大厅、玩家间推挤或正式动画。C 在任意地点切换不保证都有马桶构图；E Anchor 是本次可复现的可见性测试位置。

## 官方依据

- [Sessions + NGO](https://docs.unity.com/en-us/mps-sdk/tutorials/build-your-first-session)
- [Casual co-op quickstart](https://docs.unity.com/en-us/multiplayer/quickstarts/casual-co-op-quickstart)
- [Relay + NGO](https://docs.unity.com/en-us/mps-sdk/tutorials/relay-and-ngo)
- [Create session](https://docs.unity.com/en-us/mps-sdk/create-session)
- [Cinemachine Third Person Follow](https://docs.unity.cn/Packages/com.unity.cinemachine%403.1/manual/CinemachineThirdPersonFollow.html)

实现 API 还直接核对了本次安装包的源代码，避免使用旧版 Cinemachine 2 或 NGO 旧继承式权限示例。
