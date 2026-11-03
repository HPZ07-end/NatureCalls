# 技术架构

领域含义只在 [Glossary](../GLOSSARY.md) 定义。当前为 Stage 1 + 人类授权的 M1 Playground；不要求概念树和目录同构。

| 目录 | 技术职责 / 当前内容 |
| --- | --- |
| `Assets/Core` | `NatureCalls.Core.asmdef`，无 Unity 引用；纯 C# 规则：`OutfitSelection`（换装预览/确认）、`LobbyRules`（2–4 人、非房主全员准备）、`MapSelection`（含随机地图）。未完成 Stage 2 |
| `Assets/Player` | Movement：CharacterController、朝向、跳跃与马桶 Anchor 对齐；Posture：姿态状态与通知；PlayerAppearance：身份颜色与外观（按 `OutfitCatalog` 编号）；PlayerOutfitController：DressingRoom 换装流程；OutfitCatalog：可选外观资源；LocalProfile：本地已确认外观（PlayerPrefs） |
| `Assets/Input` | PlayerInput：本地键鼠操作请求，只为 Owner 启用；按固定顺序分派 E 键交互 |
| `Assets/Camera` | CameraController：本地 Cinemachine 目标、优先级、Blend 与渲染 |
| `Assets/Facility` | Toilet：测试 Anchor 和可见目标；StallDoor：隔间门开/关/锁；PaperRoll：拾取、携带与丢掷；PlanarMirror：镜面渲染。均无正式设施规则或网络同步 |
| `Assets/Network` | Session：持久 MPS/Relay 与直连连接、人数、出生槽位、Lobby 模式；LobbyRoom：房间成员/准备/外观/阶段与地图加载后的出生；Avatar：NGO 所有权与姿态/颜色/外观同步；Probe：显式启动的 Development 验证程序 |
| `Assets/Map` | Playground：地图资源引用、出生点、离线练习；MapCatalog：可选地图；Match：本局模式（离线/联机）与场景切换；DressingMirror / LobbyDoor：DressingRoom 中镜子与门的 E 键入口 |
| `Assets/UI` | MatchExit：地图左上角「退出」按钮（`MatchHud.prefab`），联机时先离开房间再回到准备房；`UI/Lobby`：LobbyView（纯引用）、LobbyController（行为）、BackgroundBlur（URP 景深虚化） |
| `Assets/Scenes` | 构建顺序：DressingRoom（启动场景）、Playground（地图）、Smoke；Sandbox 原样保留 |
| `Assets/Prefabs` | `Characters/Player`（网络玩家基础 Prefab）与 Variant `DressingRoomPlayer`（带换装组件）；`Network/Network`（Lobby 的持久 NetworkManager + Session）、`Network/LobbyRoom`；`Levels/WCStall` 与早期 `Levels/Stall6x8`；`Toilet` 由 Playground 重建工具生成 |
| `Assets/Materials`、`Assets/Settings` | 原型材质、URP 管线与 Renderer |
| `Assets/Editor` | 配置、生成、验证、构建；`LobbyFlowBuilder` 生成 Lobby 流程资源与 DressingRoom 接线；`ToiletAnchorSetup` 与 `SpawnPointSetup` 在 Playground 场景中实测放置马桶 Anchor 与出生点；Editor-only 程序集 |
| `Assets/Tests` | EditMode 资产/配置、Lobby 资源接线与 Core 规则；PlayMode 实际运动、门、卷纸、镜头、换装、Lobby 离线/建房/地图加载与连接生命周期 |
| `scripts` | Mac/Windows Python 标准库命令、语义检查及双 Player 验证 |

运行时组件共用一个 `NatureCalls.Runtime` 总程序集（引用 `NatureCalls.Core`），使 Editor/测试能引用它；没有按 Player、Facility 等拆分程序集。运行时不依赖 Editor/测试，Core 不依赖 Unity。

依赖流：Input → Movement/Posture/Facility/Map 交互对象；Camera 读取 Movement/Posture；Avatar 将 NGO 共享状态映射到角色，并只绑定本地 Camera；Session 管理连接和出生，UI 调用 Session、LobbyRoom 与 Match。Posture 不引用 Camera 或网络。未来正式如厕系统可调用 Posture/Movement 的现有能力。

E 键交互没有统一接口，由 `PlayerInput.TryInteract` 依次尝试：如厕姿态中则站起 → 最近可拾取 PaperRoll → DressingMirror → 位于隔间内时先试 Toilet → 最近 StallDoor → LobbyDoor → Toilet，首个成功者生效。新增可交互设施时需要在此加入并确定优先级。

## 游戏流程

```text
启动 → DressingRoom（镜子换装；本地玩家用 DressingRoomPlayer）
        └─ 左侧门 E → Lobby 界面（虚化背景）
             ├─ 单人离线测试 ─────────────────────────┐
             ├─ 创建房间 ─┐                            ├→ 地图选择（房主；含随机地图）→ 加载地图场景 → 对局
             └─ 加入房间 ─┴→ 房间（2–4 人，准备/开始）─┘
对局左上角「退出」→ 返回准备房（联机时先离开 Session）
```

- 离线：`Match.Begin(Offline)` 直接加载地图，地图的 Playground 生成本地玩家并穿上 `LocalProfile` 外观，不创建网络对象。
- 联机：LobbyController 在需要时实例化 `Network.prefab`（NGO 自动 DontDestroyOnLoad），`Session.LobbyMode` 使连接审批只分配槽位、不生成玩家；Host 启动后生成 `LobbyRoom`（同样跨场景保留）。未关联 Unity Cloud 时，创建房间走局域网直连，房间号显示房主局域网地址；加入时输入 IP 走直连，否则走 Relay Join Code。
- 房主选择地图后，LobbyRoom 用 RPC 让所有端各自加载地图场景；每端加载完成后回报，Host 在**全员加载完成后**统一 `SpawnAsPlayerObject`，并在生成前写入 `Avatar.OutfitIndex`，避免玩家对象被生成进旧场景。
- 地图场景自带的 Network 对象（用于直接打开地图开发测试）在已有持久 Session 时于 Awake 中自毁，保证只有一个 NetworkManager。

## 网络模型

网络原型采用 Owner-authoritative NetworkTransform，位置与身体 Y 轴朝向同步；Posture 为 Owner-write NetworkVariable；出生颜色与外观编号为 Server-write。LobbyRoom 的成员、准备与阶段由 Host 权威，客户端经 RPC 请求。镜头、鼠标及 Blend 不同步。StallDoor、PaperRoll 状态只在本地进程变化，联机时各端不一致。未来共享设施、Tidiness、胜负规则采用 Host-authoritative 方向，本次没有实现。

M1 仍关闭 NGO Scene Management：地图切换由各端本地加载并以 RPC 协调，地图中没有场景内 NetworkObject；以后需要场景内网络对象时再评估启用。没有 Host 迁移；对局中与房主断开的客户端回到准备房并看到原因。

## 命名

标准名称采用 Player、Posture、Facility、Input、Network、Map；Camera 是技术模块，不强行加入语义树。不另建 Networking/Environment 或 Toileting 状态的同义层。旧 Smoke/Sandbox 保留。

2026-11-02 移除：ChangingRoom 场景与 `ChangingRoomPlatform` / `ChangingRoomHud` / `ThirdPersonCamera`（由 DressingRoom 镜子换装替代）；独立 `LobbyUI` 场景与 `LobbyUIController` / `LobbyMockData` / `LobbySceneNavigator`（由 DressingRoom 内的 Lobby 界面与 LobbyRoom 替代）；IMGUI `Panel` 菜单（由 `MatchExit` 左上角退出按钮替代，连接入口统一在大厅）；已完成的一次性迁移/生成脚本（ChangingRoom、LobbyUI 与 DressingRoom UI/门/练习相关 Builder、`PlayerContractMigration`）。`ChangingRoomPlayer.prefab` 更名为 `DressingRoomPlayer.prefab`（GUID 保留）。组件细节、镜头参数与官方 API 依据见 [PLAYGROUND](PLAYGROUND.md)，Player 接口见 [PLAYER](PLAYER.md)。

阅读一次 C 键切换：从 `Assets/Input/PlayerInput.cs` 的 Update 进入 `Posture.Toggle`，Changed 通知让 Movement 更新身体/碰撞体，CameraController 更新两台 Cinemachine 镜头的优先级；NGO Avatar 在联机时发布拥有者的姿态状态。镜头距离、目标高度、俯角和横向偏移在场景 Local camera 的 Inspector 调整，默认正后方居中。IDE 打开与跳转步骤见 [开发指南](DEVELOPMENT.md)。
