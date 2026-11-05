# 技术架构

领域含义只在 [Glossary](../GLOSSARY.md) 定义。当前为 Stage 1 + 人类授权的 M1 Playground；不要求概念树和目录同构。

| 目录 | 技术职责 / 当前内容 |
| --- | --- |
| `Assets/Core` | `NatureCalls.Core.asmdef`，无 Unity 引用；纯 C# 规则：`OutfitSelection`（换装预览/确认）、`LobbyRules`（2–4 人、非房主全员准备）、`MapSelection`（含随机地图）、`RoundClock`（全局对局计时）、`NeedsRules`（急迫值、恶心值与全体判负）。未完成 Stage 2 |
| `Assets/Player` | Movement：CharacterController、朝向、跳跃与马桶 Anchor 对齐；Posture：姿态状态与通知；PlayerAppearance：身份颜色与外观（按 `OutfitCatalog` 编号）；PlayerOutfitController：DressingRoom 换装流程；OutfitCatalog：可选外观资源；LocalProfile：本地已确认外观（PlayerPrefs）；PlayerNeeds：急迫值与恶心值（由离线 Playground 或房主驱动） |
| `Assets/Input` | PlayerInput：本地键鼠操作请求，只为 Owner 启用；C 负责全部如厕，E 按固定顺序分派其他交互 |
| `Assets/Camera` | CameraController：本地 Cinemachine 目标、优先级、Blend 与渲染 |
| `Assets/Facility` | Toilet：测试 Anchor 和可见目标；StallDoor：隔间门开/关/锁；PaperRoll：拾取、携带与丢掷；PlanarMirror：镜面渲染。门与卷纸由 Avatar 接入房主权威同步，尚无冲水、堵塞等正式玩法 |
| `Assets/Network` | Session：持久 MPS/Relay 与直连连接、人数、出生槽位、Lobby 模式；LobbyRoom：房间成员/准备/外观/阶段、地图加载后的出生、联机对局全局计时与全体判负；Avatar：NGO 所有权与姿态/颜色/外观同步；Probe：显式启动的 Development 验证程序 |
| `Assets/Map` | Playground：地图资源引用、出生点、离线练习；MapCatalog：可选地图；Match：本局模式（离线/联机）、场景切换与离线对局计时；DressingMirror / LobbyDoor：DressingRoom 中镜子与门的 E 键入口 |
| `Assets/UI` | MatchExit：地图左上角「退出」按钮（`MatchHud.prefab`），联机时先离开房间再回到准备房；MatchClock：同一 HUD 顶部的本局剩余时间；NeedsView：左上角本地玩家的急迫值/恶心值条；`UI/Lobby`：LobbyView（纯引用）、LobbyController（行为）、BackgroundBlur（URP 景深虚化） |
| `Assets/Scenes` | 构建顺序：DressingRoom（启动场景）、Playground（地图）、Smoke；Sandbox 原样保留 |
| `Assets/Prefabs` | `Characters/Player`（网络玩家基础 Prefab）与 Variant `DressingRoomPlayer`（带换装组件）；`Network/Network`（Lobby 的持久 NetworkManager + Session）、`Network/LobbyRoom`；`Levels/WCStall` 与早期 `Levels/Stall6x8`；`Toilet` 由 Playground 重建工具生成 |
| `Assets/Materials`、`Assets/Settings` | 原型材质、URP 管线与 Renderer |
| `Assets/Editor` | 配置、生成、验证、构建；`LobbyFlowBuilder` 生成 Lobby 流程资源与 DressingRoom 接线；`ToiletAnchorSetup` 与 `SpawnPointSetup` 在 Playground 场景中实测放置马桶 Anchor 与出生点；Editor-only 程序集 |
| `Assets/Tests` | EditMode 资产/配置、Lobby 资源接线与 Core 规则；PlayMode 实际运动、门、卷纸、镜头、换装、Lobby 离线/建房/地图加载与连接生命周期 |
| `scripts` | Mac/Windows Python 标准库命令、语义检查及双 Player 验证 |

运行时组件共用一个 `NatureCalls.Runtime` 总程序集（引用 `NatureCalls.Core`），使 Editor/测试能引用它；没有按 Player、Facility 等拆分程序集。运行时不依赖 Editor/测试，Core 不依赖 Unity。

依赖流：Input → Movement/Posture/Facility/Map 交互对象；Camera 读取 Movement/Posture；Avatar 将 NGO 共享状态映射到角色，并只绑定本地 Camera；Session 管理连接和出生，UI 调用 Session、LobbyRoom 与 Match。Posture 不引用 Camera 或网络。未来正式如厕系统可调用 Posture/Movement 的现有能力。

如厕由 `PlayerInput.TryToilet`（C）处理，一次性：如厕中再按无效，5 秒后 `PlayerInput` 自动站起、释放马桶并报告结束（离线直接改 `PlayerNeeds`，联机发给房主）；位于某个隔间内（`StallDoor.Containing`）时请求坐到该隔间的马桶（联机时房主检查占用和所在隔间后回复 Owner 坐下）；否则原地如厕。E 键交互没有统一接口，由 `PlayerInput.TryInteract` 依次尝试：最近可拾取 PaperRoll → DressingMirror → 最近 StallDoor → LobbyDoor，首个成功者生效；坐着时也可用，因此能在隔间内关门上锁。新增可交互设施时需要在此加入并确定优先级。

## 游戏流程

```text
启动 → DressingRoom（镜子换装；本地玩家用 DressingRoomPlayer）
        └─ 左侧门 E → Lobby 界面（虚化背景）
             ├─ 单人离线测试 ─────────────────────────┐
             ├─ 创建房间 ─┐                            ├→ 地图选择（房主；含随机地图）→ 加载地图场景 → 对局
             └─ 加入房间 ─┴→ 房间（2–4 人，准备/开始）─┘
对局左上角「退出」→ 返回准备房（联机时先离开 Session）
全局计时结束 → 返回准备房（联机时所有人留在原房间，可再开一局）
```

- 离线：`Match.Begin(Offline)` 直接加载地图，地图的 Playground 生成本地玩家并穿上 `LocalProfile` 外观，不创建网络对象。
- 联机：LobbyController 在需要时实例化 `Network.prefab`（NGO 自动 DontDestroyOnLoad），`Session.LobbyMode` 使连接审批只分配槽位、不生成玩家；Host 启动后生成 `LobbyRoom`（同样跨场景保留）。未关联 Unity Cloud 时，创建房间走局域网直连，房间号显示房主局域网地址；加入时输入 IP 走直连，否则走 Relay Join Code。
- 房主选择地图后，LobbyRoom 用 RPC 让所有端各自加载地图场景；每端加载完成后回报，Host 在**全员加载完成后**统一 `SpawnAsPlayerObject`，并在生成前写入 `Avatar.OutfitIndex`，避免玩家对象被生成进旧场景。
- 全局计时：联机时 Host 在全员生成后写入 `LobbyRoom.RoundEndTime`（服务器时间），各端据此显示剩余时间；到点后 Host 把阶段改回 Waiting、清除准备、Despawn 所有角色，再用 RPC 让所有端回到 DressingRoom，大厅界面重新打开在原房间上。离线时 `Match` 在本地计时并在到点后返回。时长取自 `MapCatalog` 每张地图的 `RoundSeconds`（默认 180 秒）。房主同时驱动每个玩家的 `PlayerNeeds`，任一玩家急迫值或恶心值达到阈值即按同一流程结束本局并带上失败原因；大厅在房间状态上方显示上一局结果。
- 地图场景自带的 Network 对象（用于直接打开地图开发测试）在已有持久 Session 时于 Awake 中自毁，保证只有一个 NetworkManager。

## 网络模型

网络原型采用 Owner-authoritative NetworkTransform，位置与身体 Y 轴朝向同步；Posture 为 Owner-write NetworkVariable；出生颜色与外观编号为 Server-write。LobbyRoom 的成员、准备与阶段由 Host 权威，客户端经 RPC 请求。镜头、鼠标及 Blend 不同步。StallDoor、PaperRoll 的请求通过玩家 Avatar 到 Host，复用设施原有距离、门锁与容量验证；Host 玩家的 Avatar 用 NetworkList 发布门状态、卷纸携带者及丢掷位姿。设施在玩家生成时按地图层级路径固定索引；客户端卷纸不模拟刚体，由 Host 发布位置。Tidiness 与胜负规则尚未实现。

M1 仍关闭 NGO Scene Management：地图切换由各端本地加载并以 RPC 协调，地图中没有场景内 NetworkObject；以后需要场景内网络对象时再评估启用。没有 Host 迁移；对局中与房主断开的客户端回到准备房并看到原因。

### 网络拓扑与专用服务器方案（2026-11-05 记录，未实施）

当前采用 **房主权威（listen server）+ Relay**，课程截止（2026-12-21）前不改变。规则类状态（计时、急迫值/恶心值、判负、门与马桶占用）继续由房主判定；玩家正在操作的物理物体可以临时归操作者所有（见 PLAYGROUND「卷纸」）。

备选拓扑：专用服务器（云上无画面服务器，玩家对等，成本与运维较高）、分布式权威（NGO 2.x，每个对象由拥有者计算并由 Unity 服务协调，自带会话所有者迁移，但现有「请求发给房主」的写法要重写）、纯拥有者权威（只适合各管各的状态）、确定性同步（Unity 物理不满足确定性，不考虑）。

团队成员的自有服务器可作为专用服务器原型与构建机：

| 项 | 现状 / 计划 | 对游戏服务器的意义 |
| --- | --- | --- |
| CPU | 2 × Xeon E5-2673 v4，40 核 80 线程 | 每个 2–4 人房间约占一个核心以内，可同时承载几十个房间（估算，未实测） |
| 内存 | 64 GB ECC DDR4 | 每个房间约 0.3–1 GB（估算） |
| GPU | 计划 RTX 3090 / 3090 Ti | 无画面服务器版本不需要；可与 GPU 任务并存，用 systemd / Docker 限定游戏服务器的核心与内存 |
| 存储 | 2 × 500 GB SATA SSD RAID 1，NVMe 待定 | 服务器程序约几百 MB，加日志，足够 |
| 系统与管理 | Ubuntu Server 22.04 LTS，机房 IPMI + SSH | 可直接运行 Linux Dedicated Server 构建 |

实施前提与工作量：

1. **网络**：需要公网 IP 或机房端口映射，开放一段 UDP 端口（每个房间一个）。没有公网入口时，服务器同样可以作为 Relay 客户端，延迟多一跳。只开放游戏端口与 SSH，SSH 仅密钥登录，IPMI 不暴露在公网，游戏进程以普通用户运行。
2. **构建**：Hub 为 6000.3.26f1 安装 Linux Dedicated Server Build Support，增加服务器构建命令；客户端与服务器必须来自同一提交。
3. **无房主结构**：门、卷纸、马桶占用目前挂在房主玩家的 Avatar 上，需迁到服务器拥有的对象（如 `LobbyRoom` 或独立地图状态对象）；计时、需求与判负已由服务器端逻辑计算，可沿用。这一步对房主迁移和分布式权威同样有用。
4. **语义**：「房主选图 / 房主开始」在无房主时要改为新规则（例如第一个进入的玩家），属于 GLOSSARY 变化，需人类批准。
5. **房间分配**：先常驻固定几个房间（IP:端口），之后再考虑按需启动进程的分配程序。

## 命名

标准名称采用 Player、Posture、Facility、Input、Network、Map；Camera 是技术模块，不强行加入语义树。不另建 Networking/Environment 或 Toileting 状态的同义层。旧 Smoke/Sandbox 保留。

2026-11-02 移除：ChangingRoom 场景与 `ChangingRoomPlatform` / `ChangingRoomHud` / `ThirdPersonCamera`（由 DressingRoom 镜子换装替代）；独立 `LobbyUI` 场景与 `LobbyUIController` / `LobbyMockData` / `LobbySceneNavigator`（由 DressingRoom 内的 Lobby 界面与 LobbyRoom 替代）；IMGUI `Panel` 菜单（由 `MatchExit` 左上角退出按钮替代，连接入口统一在大厅）；已完成的一次性迁移/生成脚本（ChangingRoom、LobbyUI 与 DressingRoom UI/门/练习相关 Builder、`PlayerContractMigration`）。`ChangingRoomPlayer.prefab` 更名为 `DressingRoomPlayer.prefab`（GUID 保留）。组件细节、镜头参数与官方 API 依据见 [PLAYGROUND](PLAYGROUND.md)，Player 接口见 [PLAYER](PLAYER.md)。

阅读一次 C 键切换：从 `Assets/Input/PlayerInput.cs` 的 Update 进入 `Posture.Toggle`，Changed 通知让 Movement 更新身体/碰撞体，CameraController 更新两台 Cinemachine 镜头的优先级；NGO Avatar 在联机时发布拥有者的姿态状态。镜头距离、目标高度、俯角和横向偏移在场景 Local camera 的 Inspector 调整，默认正后方居中。IDE 打开与跳转步骤见 [开发指南](DEVELOPMENT.md)。
