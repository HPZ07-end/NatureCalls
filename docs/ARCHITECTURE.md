# 技术架构

领域含义只在 [Glossary](../GLOSSARY.md) 定义。当前为 Stage 1 + 人类授权的 M1 Playground；不要求概念树和目录同构。

| 目录 | 技术职责 / 当前内容 |
| --- | --- |
| `Assets/Core` | `NatureCalls.Core.asmdef`，无 Unity 引用；目前只有换装预览/确认状态 `OutfitSelection` 一个纯 C# 类型，未完成 Stage 2 |
| `Assets/Player` | Movement：CharacterController、朝向、跳跃与马桶 Anchor 对齐；Posture：姿态状态与通知；PlayerAppearance：身份颜色与外观；PlayerOutfitController：本地换装预览，委托 Core `OutfitSelection` |
| `Assets/Input` | PlayerInput：本地键鼠操作请求，只为 Owner 启用；按固定顺序分派 E 键交互 |
| `Assets/Camera` | CameraController：本地 Cinemachine 目标、优先级、Blend 与渲染 |
| `Assets/Facility` | Toilet：测试 Anchor 和可见目标；StallDoor：隔间门开/关/锁；PaperRoll：拾取、携带与丢掷；PlanarMirror：镜面渲染；ChangingRoomPlatform：ChangingRoom 场景换装台。均无正式设施规则或网络同步 |
| `Assets/Network` | Session：MPS/Relay、直连诊断、人数及出生点；Avatar：NGO 所有权与姿态/颜色；Probe：显式启动的 Development 验证程序 |
| `Assets/Map` | Playground：资源引用、出生点、离线练习生命周期；DressingMirror / LobbyDoor：DressingRoom 中镜子与大厅门的 E 键入口；LobbySceneNavigator：切换到 LobbyUI 场景 |
| `Assets/UI` | Panel：Host/Join/Leave、状态与输入捕获提示；ChangingRoomHud：换装面板；`UI/Lobby`：LobbyUIController 与本地四人 Mock `LobbyMockData`（未接入 Session） |
| `Assets/Scenes` | Playground 为首个构建场景，其后为 Smoke、DressingRoom、`UI/Lobby/LobbyUI`、ChangingRoom；Smoke、Sandbox 原样保留 |
| `Assets/Prefabs` | `Characters/Player`（网络玩家基础 Prefab）与其 Variant `ChangingRoomPlayer`；`Levels/WCStall`（三个隔间实例共用）与早期 `Levels/Stall6x8`；`Toilet` 由 Playground 重建工具生成。均通过 Editor API 生成或修改 |
| `Assets/Materials`、`Assets/Settings` | 原型材质、URP 管线与 Renderer |
| `Assets/Editor` | 配置、生成、验证、构建；场景/Prefab Builder 与 Migration；Editor-only 程序集 |
| `Assets/Tests` | EditMode 资产/配置及 `OutfitSelection` 验证；PlayMode 实际运动、门、卷纸、镜头、DressingRoom、Lobby UI 与连接生命周期 |
| `scripts` | Mac/Windows Python 标准库命令、语义检查及双 Player 验证 |

运行时组件共用一个 `NatureCalls.Runtime` 总程序集（引用 `NatureCalls.Core`），使 Editor/测试能引用它；没有按 Player、Facility 等拆分程序集。运行时不依赖 Editor/测试，Core 不依赖 Unity。

依赖流：Input → Movement/Posture/Facility/Map 交互对象；Camera 读取 Movement/Posture；Avatar 将 NGO 共享状态映射到角色，并只绑定本地 Camera；Session 管理连接和出生，UI 调用 Session。Posture 不引用 Camera 或网络。未来正式如厕系统可调用 Posture/Movement 的现有能力。

E 键交互没有统一接口，由 `PlayerInput.TryInteract` 依次尝试：最近可拾取 PaperRoll → DressingMirror → 最近 StallDoor → LobbyDoor → Toilet，首个成功者生效。新增可交互设施时需要在此加入并确定优先级。ChangingRoomPlatform 例外：它自行用旧版 `UnityEngine.Input` 读取 E 键，不经过该链。

网络原型采用 Owner-authoritative NetworkTransform，位置与身体 Y 轴朝向同步；Posture 为 Owner-write NetworkVariable，出生颜色为 Server-write。镜头、鼠标及 Blend 不同步。StallDoor、PaperRoll 状态只在本地进程变化，联机时各端不一致。未来共享设施、Tidiness、胜负规则采用 Host-authoritative 方向，本次没有实现。M1 关闭 NGO Scene Management，因为所有客户端使用同一个已提交的 Playground 场景；DressingRoom → LobbyUI 是本地场景切换，不经过网络，LobbyUI 也不会进入 Playground。以后增加联机关卡切换需另行设计与测试。

DressingRoom（镜子 + DressingMirror）与 ChangingRoom（换装台 + ChangingRoomPlatform + `ChangingRoomPlayer`）是两套并存的换装原型；Glossary 只定义 DressingRoom，两者取舍待人类决定。ChangingRoom 的 Prefab 约定见 [Player 接口约定](PLAYER.md)。

标准名称采用 Player、Posture、Facility、Input、Network、Map；Camera 是技术模块，不强行加入语义树。不另建 Networking/Environment 或 Toileting 状态的同义层。旧 Smoke/Sandbox 保留。组件细节、镜头参数与官方 API 依据见 [PLAYGROUND](PLAYGROUND.md)。

阅读一次 C 键切换：从 `Assets/Input/PlayerInput.cs` 的 Update 进入 `Posture.Toggle`，Changed 通知让 Movement 更新身体/碰撞体，CameraController 更新两台 Cinemachine 镜头的优先级；NGO Avatar 在联机时发布拥有者的姿态状态。镜头距离、目标高度、俯角和横向偏移在场景 Local camera 的 Inspector 调整，默认正后方居中。IDE 打开与跳转步骤见 [开发指南](DEVELOPMENT.md)。
