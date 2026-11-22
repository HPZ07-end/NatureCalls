# Demo 实现复用指南

本指南用于重新设计地图、玩法与 UI 时复用已有角色控制和交互实现。它描述现状和技术接入条件，不定义新玩法；领域语义仍以 [GLOSSARY](../GLOSSARY.md) 为唯一权威。

## 1. 范围、基线和阅读顺序

- 梳理日期：2026-11-23（Asia/Shanghai）。
- HEAD 与本次 fetch 后的 origin/dev：`9dc22188c97a455f8094d249342c39f14747d73a`；origin/main：`e43122a22c61623e57930c34932912bbcdca4a9a`。
- 当前分支：`feat/art-model-import`。本指南按 **HEAD 加当前未提交工作区** 阅读，不能仅凭这个 SHA 重建全部现状。
- 工作区已有角色、美术、相机、换装、联机外观、测试和 Glossary 修改。本次不覆盖、不提交这些改动。尤其男女模型、SharedAirborne、性别预览/同步属于工作区扩展，不能当作 HEAD 已包含的接口。
- 用户本次提出把已有成果作为 demo，重做地图、玩法与 UI。本次完成实现梳理，未搬动资源、改场景、删除模块或落地新的游戏规则。
- Unity：6000.3.26f1。当前 manifest：URP 17.3.0、Input System 1.20.1、Cinemachine 3.1.7、NGO 2.13.3、MPS 2.4.1、Multiplayer Play Mode 2.0.2、uGUI 2.0.0、Test Framework 1.6.0。复用时保留 Packages 锁文件，不在搬运时顺便升级。

涉及语义路径：`NatureCalls/Gameplay/Player`（Posture、Urgency、Disgust）、`NatureCalls/Gameplay/Facility`、`NatureCalls/Gameplay/Mess`、`NatureCalls/Gameplay/GameFlow`、`NatureCalls/Gameplay/UI/Lobby`、`NatureCalls/Infrastructure/Input`、`NatureCalls/Infrastructure/Network`、`NatureCalls/Content/Map`。

建议先读第 2、3 节确定复用边界；接角色看第 4 节，接设施看第 5、6 节，做新地图与 UI 看第 7、8 节，实施顺序与验收看第 9 节。

## 2. 哪些适合保留，哪些必须重新接入

**优先保留的能力**：Movement 的移动/观察/跳跃、Posture 状态通知、CameraController 的本地镜头绑定，以及 Item 的携带/丢掷和 StallDoor 的状态转换。它们仍有组件和资源前提，尚不是独立安装包。

**连同依赖一起复用的能力**：马桶/小便池使用、准度、擦拭、拖把清洁、男女模型与换装、网络设施同步。它们跨越 PlayerInput、设施、PlayerNeeds、Core、Playground、Network 多个对象；只复制一个脚本会缺初始化或结算。

**作为旧流程参考，按新设计替换的部分**：旧地图布局、LobbyCanvas/MatchHud 的视觉设计、LobbyController 的面板编排、Match/LobbyRoom 中的开始/结束与返回准备房流程、NeedsRules 的旧数值和 PlayerInput 的旧交互优先级。替换之前仍需保留运行所依赖的接线。

**不能据此认定已完成的玩法**：冲水、水管堵塞、水闸玩法、设施维修/拆除、事故、Tidiness 和胜利判定。美术模型或预览动画存在不等于有运行时交互。具体设计重新讨论，不从旧文档或模型名称补全规则。

保留名称：Player、Movement、Posture、PlayerInput、CameraController、Facility、Item、Playground、MapCatalog、Match、Session、LobbyRoom、Avatar、FacilitySync、Facilities。本文没有新增或移除运行时类型，也没有把“demo”变成新的命名体系。

## 3. 现有实现怎样连起来

```text
设备输入 → Input.PlayerInput
  ├─ Movement / Posture → CharacterController、角色朝向与姿态
  │                         └─ CameraController / PlayerAppearance 读取状态
  ├─ 离线：直接调用 Facility → PlayerNeeds / Core 规则 → Playground 污物
  └─ 联机：玩家 FacilitySync → 服务器校验 → Facilities 共享列表
                                               └─ 各端设施应用状态

UI → LobbyController → Session / LobbyRoom / Match
MapCatalog → 选择地图 → Match 加载 Scene
  ├─ 离线：Playground.Practice → 玩家生成、控制开启、相机绑定
  └─ 联机：LobbyRoom 等所有端加载完成 → Facilities → 网络玩家 → 本地镜头绑定
```

当前 `NatureCalls.Runtime` 是一个总程序集，依赖 Input System、Cinemachine、NGO、Unity Transport、Services 和 URP；`NatureCalls.Core` 设置 `noEngineReferences: true`。只挑运行时脚本复制到全新工程，也必须处理这些程序集依赖，不能把当前目录当作已经完成拆分的 SDK。

最需要注意的耦合：

1. `PlayerInput` 同时负责设备绑定、目标查找、操作优先级、如厕/清洁状态机和静态结算 `ApplyResult`，不是单纯输入适配器。
2. `Playground` 同时保存地图引用、生成离线角色、驱动离线结束和维护污物对象池；这是多个模块查找的具体类型，并非只代表名为 Playground 的 Scene。
3. `Avatar`、`Facilities`、PlayerNeeds 和 HUD 会查找 `Map.Playground`。新 Scene 改名可以，直接删掉此组件会断链。
4. 设施常把规则与表现放在同一个 MonoBehaviour；例如 StallDoor 同时判断门锁、转门扇和切碰撞。替换美术时必须检查这些引用。
5. 多处使用全局 `FindObjectsByType` / `FindFirstObjectByType`，假定主要只有一张运行中的地图。Additive 多地图不是已经验证的复用方式。

## 4. 角色控制、姿态、镜头和外观接口

源文件：[Movement](../Assets/Player/Movement.cs)、[Posture](../Assets/Player/Posture.cs)、[CameraController](../Assets/Camera/CameraController.cs)、[PlayerAppearance](../Assets/Player/PlayerAppearance.cs)、[PlayerOutfitController](../Assets/Player/PlayerOutfitController.cs)。资产结构另见 [PLAYER](PLAYER.md)。

### 4.1 Movement

依赖同对象上的 `CharacterController`、`Posture`，以及 `Body` 模型根引用。Awake 暂时关闭 CharacterController，控制权建立后才开启。

```csharp
void SetControl(bool value)
void Look(Vector2 delta)
void Move(Vector2 direction, float deltaTime)
bool Jump()
void Enter(Transform anchor)
```

- `Move` 接收角色局部平面方向和秒数，内部归一化、乘 Speed、处理重力及 CharacterController 碰撞；调用者不能再乘一次 Speed 或 deltaTime。
- `Look` 接收鼠标增量，内部乘 Sensitivity 并限制俯仰；Body 朝向随角色根节点，俯仰通过 `Pitch` / `ViewRotation` 提供给镜头。
- `SetControl(false)` 同时关闭碰撞控制器；它不是只禁用 UI 输入的开关。`Immobile` 停止水平移动、转向和跳跃，但 Move 仍处理重力。
- `Jump` 返回是否成功；要求控制开启、着地、非如厕且未 Immobile。
- `Enter(anchor)` 保存进入前的位置/朝向并设置 Seat、切换 Squatting，**不把玩家传送到 anchor**。恢复 Standing 时，在 Seat 非空的情况下恢复进入前位置/朝向并清 Seat。
- 可读状态：`Controlled`、`Airborne`、`Pitch`、`ViewRotation`、`Seat`；可写状态：`Aiming`、`Immobile`；参数：Speed、Sensitivity、PitchLimit、JumpSpeed、StandingHeight、SquattingHeight。
- `SetControl`、Move 等应在组件 Awake 后调用；当前初始化路径是先 Instantiate 再设置控制。

### 4.2 Posture

```csharp
PostureState State { get; }
bool IsSquatting { get; }
event Action<PostureState> Changed;
void Set(PostureState state)
void Toggle()
```

姿态包括 Standing、Squatting 和 Map1 风道使用的 Crawling。Movement 按风道位置与净空切换 Crawling，镜头降低第一人称视点，Avatar 沿用 SharedPosture 同步。重复 Set 同一值不发事件。直接 Toggle 只切换如厕姿态，**不会完成设施占用、准度计时、需求结算**。

### 4.3 CameraController

```csharp
void Bind(Movement target)
void Unbind()
void UpdateTargets()
Movement Target { get; }
```

必须配置 Output、Brain、FirstPerson、ThirdPerson、Eye、Shoulder；第三人称虚拟相机需有 CinemachineThirdPersonFollow。FirstPerson 跟随 Eye，ThirdPerson 跟随 Shoulder。优先复用完整镜头组，避免只复制 Output。

镜头读取 Posture、Aiming 和 Seat，切换第一人称、对位/坐厕俯视与原地如厕第三人称；Blend、距离、俯角和碰撞层都是可调字段。Bind 只针对本地玩家；网络远端不抢本机镜头。

当前工作区用 `LocalBodyLayer = 30` 与 Output.cullingMask 隐藏第一人称身体，镜面相机仍可渲染角色。两款模型的 Renderer 会在 Bind 时一起登记；以后在绑定后新增模型，需要重新核对登记和恢复。Unbind 解除订阅并恢复图层/渲染状态。

### 4.4 外观和动画（包含未提交扩展）

- `PlayerAppearance.SetGender(PlayerGender)`：切换 Male/Female 模型；`ApplyOutfit(int)`：按 OutfitCatalog 编号染色；`ApplyIdentityColor(Color)`：出生槽位颜色；`SetRemoteAirborne(bool)`：远端动画离地状态。
- OutfitCatalog 是 ScriptableObject；有 `Count`、索引器、`Contains(int)`、`Clamp(int)`、`SetOutfits(Outfit[])`。当前 Outfit 是 Name/Color，不是装备系统。
- PlayerOutfitController：`BeginSelection()` → `Preview(int)` / `PreviewNext(int)` / `PreviewByGender(PlayerGender)` → `ConfirmSelection()` 或 `CancelSelection()`。确认保存两项，取消恢复确认值。
- `LocalProfile.SetAppearance(PlayerGender, int)` 保存本机 PlayerPrefs；跨网络同步另走 `LobbyRoom.SetAppearance` / AvatarAppearance，不能只改 PlayerPrefs。
- 动画参数为 `Speed`、`Grounded`、`Squatting`，当前模型配置关闭 root motion，角色根位移由 Movement/NetworkTransform 决定。Body 与两款 Animator、上衣 Renderer 的引用必须保留。
- `Movement` 对 Humanoid 不再用纵向缩放模拟蹲姿；primitive 的旧表现分支仍存在。此处是源码已有 legacy 兼容，后续在不再使用旧模型构建工具并通过回归后再评估移除，本次不删。

### 4.5 最小本地控制接入

使用配置完整且 Awake 已执行的玩家和镜头实例：

```csharp
var movement = player.GetComponent<NatureCalls.Player.Movement>();
movement.SetControl(true);
player.GetComponent<NatureCalls.Input.PlayerInput>().enabled = true;
cameraController.Bind(movement);
```

这只建立控制链，不创建完整对局。继续启用旧 PlayerInput 就会沿用旧 C/E/R 规则；若只复用移动，后续用新的输入控制层调用 Move/Look/Jump，并保证旧 PlayerInput 不再同时处理输入。本文不实施新的控制层。

## 5. 输入、交互和玩法状态机

源文件：[PlayerInput](../Assets/Input/PlayerInput.cs)、[Accuracy](../Assets/Core/Accuracy.cs)、[NeedsRules](../Assets/Core/NeedsRules.cs)、[PlayerNeeds](../Assets/Player/PlayerNeeds.cs)。

### 5.1 输入入口和状态读取

当前按键通过 Awake 中 `new InputAction` 创建，没有依靠一个可在 Inspector 中统一重绑的 InputActionAsset。WASD/鼠标/Space/C/E/R/Esc 绑定写在代码中。OnEnable/OnDisable 开关 Actions，OnDestroy Dispose。

`Update` 要求 Movement.Controlled；通常还要求应用有焦点、鼠标已捕获。点击非 UI 区捕获鼠标，Esc 或失焦释放。`CaptureMouse = false` 可取消捕获要求，但不会取消应用焦点检查。

上层操作接口：`bool TryToilet()`、`bool TryInteract()`。R 的 `TryThrowCarriedItem()` 是 private，不能当作新 UI 可直接调用的公开方法；定向丢掷需根据离线/联机路径调用 Item.Throw 或 FacilitySync.RequestThrow。

表现读取接口：`Phase`、`IsToileting`、`Unwiped`、`HeldItem`、`PaperRemainingUses`、`TargetCenter`、`TargetRadius`、`TargetAxes`、`CurrentLanding`、`Elapsed`、`Duration`、`CurrentAccuracy`、`CleaningProgress`、`Captured`。其中准确率/进度为比例，不是百分数；Elapsed/Duration 为秒。

### 5.2 C 的现有流程

```text
None → 隔间内首次 C → Aligning（允许移动/转向，镜头俯视）
Aligning → 再次 C、获准使用 → Holding → 准度计时结束 → None
None → 小便池前 C、获准使用 → Urinating → 计时结束 → None
None → 其它位置 C、获准使用 → Holding（原地）→ 计时结束 → None
```

这些是 demo 现状，重做玩法时不自动成为新版本要求。`TryToilet` 会阻止重复进行中的请求；联机请求有本地等待标记和超时，并等待服务器授权。

- `bool BeginGranted(Toilet targetToilet, Urinal targetUrinal)` 是批准后的启动入口，处理占用、姿态、锁移动和准度状态，不是未经校验的 UI 开始按钮。
- `void TickAim(float seconds, Vector2 adjustment)` 累积修正/漂移并 Sample，到时自动完成。当前 Update 已驱动它，新系统不能同时重复 Tick。
- `Accuracy(float duration)`、`Sample(float seconds, bool inside)` 按时间积分，末帧截断到 Duration；Value 为 Inside/Elapsed。算法可复用，但目标区域与结算阈值属于玩法配置。
- FinishToileting 是 private；完成时**先 ReportResult，再恢复 Standing**，避免释放占用后服务器无法核对结果。
- `static ApplyResult(Movement, Toilet, Urinal, float, Vector3, int)` 直接耦合需求、污物和隔间来源；它不是通用交互事件。没有 PlayerNeeds 或 Ticking 为 false 时提前返回。

### 5.3 E 的实际优先级

以源码分支为准：未擦拭且持卷纸时擦拭 → 持拖把时尝试最近可清污物 → 最近可拾 Item → 工具箱 → 镜子 → 最近可交互隔间门 → 大厅门。

这不是准星射线交互，也没有统一 IInteractable 注册表。Item 在自身类别内选最近；门在自身类别内选最近；镜子/工具箱遍历顺序不等于全类别最近。如果新玩法改为准星选择或交互菜单，应明确替换目标选择和分派，不能只更换提示文字。

清洁公开入口：`bool BeginCleaning(Mess)`、`void ContinueCleaning(bool held, float seconds)`、`void CancelCleaning()`。联机持续约每 0.1 秒发送清洁保持消息，服务器用时间与条件结算；松键或离开中断。不要用 UI 动画计时直接授予清洁结果。

### 5.4 需求与旧规则

`PlayerNeeds` 公开：`BeginRound(float random01)`、`StopRound()`、`CompleteToileting(bool insideStall)`、`CompleteUrination()`、`AddDisgust(float)`、`MarkUnwiped(StallDoor)`、`Wipe()`、`LeaveUnwiped()`、`ApplyShared(float urgency, float disgust, bool unwiped = false)`；读取 Urgency、Disgust、Ticking、Failed、Unwiped，可写 Toileting。

离线或服务器开启 Ticking 后在 Update 增长需求，并判断离开未擦拭隔间、积水距离。客户端通常只显示 ApplyShared 的状态。BeginRound 不是完整交互状态重置器；以后复用同一角色跨局时还需清理如厕、占用、未擦拭、移动和携带状态。

NeedsRules 是纯 C#，但包含旧玩法时间、阈值、漂移、距离、纸张次数等。RoundClock、LobbyRules、MapSelection、OutfitSelection 也可独立测试，却各自包含旧流程规则。可以保留算法结构；新数值和流程需重新设计并经语义批准。

## 6. 设施 API 与离线/联机调用边界

### 6.1 设施本地能力

- [StallDoor](../Assets/Facility/StallDoor.cs)：`CanInteract(Movement)`、`IsInside(Movement)`、`Contains(Movement)`、`Interact(Movement)`、`ApplySharedState(StallDoorState)`；静态 `Containing(Movement)`。State 为 Open/Closed/Locked。依赖 DoorLeaf、DoorColliderPivot；Awake 缺引用会禁用组件。`Toilet` 在 Awake 从子层级寻找。范围由子 Renderer bounds 推导，内外按局部 Z 判断，改变朝向/装饰/层级后需复测。Interact 返回 true 不一定表示状态变化，例如门锁住时外部玩家的尝试仍可返回 true。
- [Toilet](../Assets/Facility/Toilet.cs)：`CanSeat(Movement)`、`Seat(Movement)`、`Release(Movement)`、`ApplySharedOccupant(Movement)`；读取 Occupant、IsOccupied、TargetCenter、TargetRadius；静态 `SeatedOn(Movement)`。需要 Anchor，Interior 提供目标中心。Seat 只检查局部条件并调用 Movement.Enter，不替代 PlayerInput 的完整使用流程，也不独立校验隔间归属。
- [Urinal](../Assets/Facility/Urinal.cs)：`Contains(Movement)`、`Occupy(Movement)`、`Release(Movement)`、`ApplySharedOccupant(Movement)`；Center/Front 是局部坐标，TargetCenter/FrontPosition 为世界坐标。距离、占用是交互前提。
- [Item](../Assets/Facility/Item.cs)：`CanPickup(Movement)`、`Pickup(Movement)`、`Throw(Movement, Vector3 direction)`；静态 `CountCarriedBy`、`FindCarriedBy`。携带容量共用，携带时隐藏 Renderer/Collider，仍留在地图 Scene，不挂到会 Despawn 的玩家下面。丢掷用 Rigidbody。若新 UI 要在手上显示道具，需要另做表现，当前没有手持挂点模型系统。
- Item 的 `ApplySharedPickup`、`ApplySharedThrow`、`ApplySharedRest`、`FollowShared` 是应用状态/运动的方法；`Store(Vector3)`、`Take(Movement)` 供工具箱状态使用。它们绕过部分许可检查，不应当作普通客户端交互入口。
- [PaperRoll](../Assets/Facility/PaperRoll.cs)：继承 Item；`Use()` 消耗，`ApplyUses(int)` 应用剩余次数，0 时停用对象。读取 RemainingUses。单独 Use 不等于已经调用 PlayerNeeds.Wipe。
- [Mop](../Assets/Facility/Mop.cs)：继承 Item；[Toolbox](../Assets/Facility/Toolbox.cs) 引用唯一 Mop，通过 `CanInteract(Movement)`、`Interact(Movement)` 取出/归还；Start 将未携带的 Mop 收纳。不能仅凭有拖把模型就认为接入完成。
- [Mess](../Assets/Facility/Mess.cs)：`Configure(int index, MessKind kind, Vector3 position, int toiletIndex)`、`CanClean(Movement)`；需要 Renderer，当前由 Playground primitive 对象池提供。Configure 包含占位形状、颜色和位置偏移，新污物美术需要替换这一表现。Index/Kind/ToiletIndex 供结算与同步使用。
- [DressingMirror](../Assets/Map/DressingMirror.cs) / [LobbyDoor](../Assets/Map/LobbyDoor.cs)：`CanInteract(Movement)`、`Interact(Movement)` 和 PlayerEntered/PlayerExited/Interacted UnityEvents。交互范围和 UI 事件需要接线；新入口布局可重接事件。

### 6.2 联机请求入口

源文件：[FacilitySync](../Assets/Network/FacilitySync.cs)、[Facilities](../Assets/Network/Facilities.cs)。

```csharp
bool RequestDoor(StallDoor door)
bool RequestPickup(Item item)
bool RequestThrow(Item item, Vector3 direction)
bool RequestSeat(Toilet toilet)
bool RequestUrinal(Urinal urinal)
bool RequestOutside()
void ReportResult(float accuracy, Vector3 position)
bool RequestWipe()
bool RequestToolbox(Toolbox toolbox)
void RequestClean(int index, bool held)
```

这些请求由**本地拥有的、已 Spawn 的玩家 FacilitySync** 发起。bool 通常表示本地检查通过并发出了请求，不能当作服务器操作已成功。RequestThrow 还有本地立即模拟，服务器再校验；不应在新 UI 中收到 true 就自行永久更新共享状态。

离线入口调用设施本地方法；联机入口走 FacilitySync，由服务器调用本地方法校验并发布 Facilities 列表。`ApplyShared*` 是接收后的应用端。客户端不能绕过 Request 直接开门、消费卷纸或改占用，否则会出现仅自己看到的状态。

Facilities 维护 SharedDoors、SharedItems、SharedToilets、SharedUrinals、SharedMesses。马桶/小便池占用用 0 表示空，其他值是 clientId + 1；ItemSnapshot.Carrier 使用原始 clientId，Simulator 用 clientId + 1。不要混用。

设施索引在生成 Facilities 时建立，主要按层级名字和 sibling index 排序；门、Item、马桶还按地图 Scene 过滤，小便池/工具箱当前使用全局查找。所有端必须有一致地图版本和层级。它不是持久设施 ID；运行中动态加入设施、多地图 Additive 或各端独立改层级，不能直接依赖当前索引机制。

离线 `PlayerInput.ToiletIndex` 按 InstanceID 排序，联机按 Facilities 数组索引；不要把离线索引存成跨运行/跨网络的持久 ID。

共享物品飞行由丢掷者模拟，约每 0.1 秒向服务器上报，其他端外推/平滑；静止后回到 Landed，断线时留在最后位置。服务器校验占用、请求来源、距离或计时等，但准度仍由客户端上报，飞行位姿也来自客户端；这是现有原型边界，不是完整反作弊系统。

## 7. 新地图必须接上的场景契约

源文件：[Playground](../Assets/Map/Playground.cs)、[MapCatalog](../Assets/Map/MapCatalog.cs)、[Match](../Assets/Map/Match.cs)、[MapLayoutIntegration](../Assets/Editor/MapLayoutIntegration.cs)。

### 7.1 继续使用现有运行时的最小条件

1. 一个 Map.Playground：填 PlayerPrefab、Spawns、Camera；Spawns 至少满足当前玩家数，当前房间目标为四个。StartOffline 按进入方式配置。
2. 一套完整的 CameraController 镜头组；本地屏幕相机只有一个有效 Output，镜面 RenderTexture 相机另计。纯服务器关闭所有相机。
3. 配置完整的共享 Player Prefab、CharacterController、Body 和设施组件，不只是模型；地板/墙体 Collider、出生点胶囊空间、门开合与跳跃边界需验证。
4. 使用旧如厕流程时，Toilet 放在正确 StallDoor 子树，Anchor/Interior 有效；隔间局部 +Z、门轴与范围适合新布局。
5. 需要旧 HUD 时放入 MatchHud 并接齐引用；重做 HUD 时保留其数据源，布局可以全部重建。
6. 从大厅选择时，把 ScenePath、DisplayName、RoundSeconds 加入 MapCatalog，并将 Scene 加入 Build Profiles 的场景列表。只在 Project 里新建 Scene 不会自动完成这些配置。
7. 需要直接地图联机开发入口时配置 Session.World；大厅进入模式则由持久 Session 和 LobbyRoom 负责生成，避免同时生成离线和网络玩家。

### 7.2 三种启动路径不可混为一谈

- **直接打开 Scene**：默认 Match.Mode 为 None，StartOffline 可生成练习玩家。Map1 单独启用了 `startOfflineRoundOnDirectPlay`，从编辑器进入 Play Mode 时走现有离线计时、需求和设施结算，便于验证擦拭与污物。地图目录和大厅选图仍使用原有流程。
- **大厅离线**：`Match.Begin(MatchMode.Offline, catalog, mapIndex)` 加载地图 → Playground.Start/Practice 生成角色、应用 LocalProfile、开启输入/绑定镜头 → 开始离线计时和需求；到点/失败返回 DressingRoom。
- **大厅联机**：LobbyRoom 选图 RPC → 各端 Match.Begin → 回报加载完成 → 服务器先 SpawnFacilities，再 SpawnAsPlayerObject → Avatar 仅为 Owner 开输入和绑定镜头 → 服务器开计时与需求。

`Playground.Practice()` 先清理旧练习玩家再生成；`StopPractice()` 解绑相机、停用并销毁玩家、释放鼠标。离线/联机切换必须保留清理次序。

Playground 的污物入口是 `int AddMess(MessKind kind, Vector3 position, int toiletIndex = -1)`、`void Drop(Vector3 position)`、`bool IsDirty(int toiletIndex)`、`void RemoveMess(int index)`；读取 `IReadOnlyList<Mess> Messes`。`ShowMesses(IReadOnlyList<MessSnapshot>)` 用于把同步快照映射到本地对象。联机 AddMess 转交 Facilities；客户端新增/删除请求仍应走服务器路径，不能直接调用这些方法假装完成权威操作。

MapLayoutIntegration 是**针对已有 MapLayoutPrototype 结构的 Editor 接线工具**，硬编码了 ScenePath、门/马桶/卷纸/Mop visual 名称和出生位置；不是可对任意新地图直接运行的通用导入器。Playground/LobbyFlowBuilder 等重建工具也应先读作用范围，不能拿来覆盖重新设计的场景/UI。

### 7.3 场景、网络与返回流程

Match.Begin 使用 Single 加载；Match.ReturnToDressingRoom 的返回路径是固定字符串。新启动页或返回地点改变时，旧 Match 和断线处理都要一起重接，不能只替换一个按钮。

Session 管连接、Transport、席位、Cloud/Relay、直连及清理；LobbyRoom 管成员、准备、领队、选图、加载完成协调、计时和结束。专用服务器不是玩家，权威判定用 IsServer。

Session 可用入口：`StartDirect(bool host, string address = "127.0.0.1")`、`ConnectRelayAsync(bool host, string code = "")`、`StartServer()`、`LeaveAsync()`、`TryAddress(...)`；Status/Busy/Active/JoinCode/IsRelay 为 UI 状态。StartDirect 的 true 只说明启动调用成功，不代表已经完成远程连接。先配置 Manager、Transport、PlayerPrefab 和网络预制体注册。

LobbyRoom 可用入口：`SetReady(bool)`、`SetAppearance(PlayerGender, int)`、`RequestStart()`、`ChooseMap(int)`、`CancelMapSelection()`；读取 Members、Phase、IsLeader、CanStart、RoundRemaining，以及 TryGetMember/ReadyMembers。联机不要用 UI 本地改 Phase 或直接 Match.Begin 跳过加载协调。

NGO Scene Management 当前关闭，Scene 由本地加载加 RPC 协调。当前网络对象主要运行时生成。不要在重做地图时随手改为另一套 NGO 场景管理流程。

## 8. 重做 UI 时接哪些数据与命令

源文件：[ToiletingHud](../Assets/UI/ToiletingHud.cs)、[NeedsView](../Assets/UI/NeedsView.cs)、[MatchClock](../Assets/UI/MatchClock.cs)、[MatchExit](../Assets/UI/MatchExit.cs)、[LobbyController](../Assets/UI/Lobby/LobbyController.cs)、[LobbyView](../Assets/UI/Lobby/LobbyView.cs)。

- 本地玩家：现有地图 HUD 用 `Playground.Camera.Target` 取得玩家，继而读 PlayerInput、PlayerNeeds；LobbyController 则用 Playground.OfflinePlayer。重做 UI 时必须明确使用哪个本地玩家来源，不能绑定随便 Find 到的远端玩家。
- 急迫/恶心条：PlayerNeeds.Urgency / Disgust，用 NeedsRules 的上限归一化；UI 不应每帧自行增长数值。
- 交互提示/进度/目标圈：读第 5.1 节接口；用地图 Output 做世界坐标投影，不能假设 Camera.main 就是当前玩家镜头。
- 计时：`MatchClock.Remaining` 已封装联机 LobbyRoom.RoundRemaining / 离线 Match.OfflineRemaining，负数表示不应显示；不要为 UI 再开一个不一致的倒计时。
- 换装：调用 PlayerOutfitController 的 Begin/Preview/Confirm/Cancel；在已连接房间中确认后还需 Room.SetAppearance。新 UI 可以直接使用这些下层接口。
- 大厅：若保留旧面板结构，可调用 LobbyController.OpenLobby/CloseLobby、StartOffline、CreateRoom、JoinRoom、PressPrimary、LeaveRoom、ChooseMap、BackFromMap。JoinRoom 等会读取旧 LobbyView 控件，不能把此类直接当成与视图无关的服务。
- 全新大厅视图：可直接接 Session/LobbyRoom/Match，但必须接回连接状态、异常反馈、房间权限、准备条件、外观提交和生命周期清理。
- `LobbyController.PreviewGender` 目前是 private；下层公开的是 PlayerOutfitController.PreviewByGender。
- 面板打开时旧代码通过 Movement.SetControl(false) 并 PlayerInput.Unlock 释放鼠标；关闭再恢复控制。新 UI 需要显式实现这一交接。
- 退出：现有 MatchExit.Exit 会等待 Session.LeaveAsync，再返回固定准备房；“退出房间”和“一局结束留在房间”不是同一条流程。

现有 UI 使用 UnityEngine.UI 的 Text/Image/Button 和序列化引用，并非已提供通用 UI 数据绑定层。可以重建视觉层，但不要让新的按钮脚本直接写 NetworkVariable 或绕过服务器校验。

## 9. 建议的复用实施顺序（尚未执行）

1. **保存可追溯 demo 基线**：先核对当前未提交角色/资源/Glossary 工作，按项目 PR 流程保存；不能只记录 HEAD 就删除或搬走工作区。是否建新仓库/目录、何时做 demo 归档是后续具体实施选择，本次不代为执行。
2. **新地图只接角色控制**：保留旧场景作对照，用共享玩家和完整镜头组确认移动、观察、跳跃、碰撞、本地身体隐藏与动画。需要纯移动试验时停止旧 PlayerInput 的玩法分派。
3. **逐个接设施**：先一扇门、一个隔间马桶、一卷纸，再接工具箱/拖把/污物/小便池；同时验证本地入口和数据读取，不一次性搬全部旧流程。
4. **重做 UI**：先接只读状态，再接操作命令和鼠标/控制交接；地图和 HUD 的目标始终是本地角色。
5. **确定新玩法后调整旧耦合**：优先评估从 PlayerInput 分离输入/分派与玩法状态、从 Playground 分离地图配置与对局/污物职责。这里是技术候选方向，没有新增类型或承诺实施大规模 Core/asmdef 拆分；语义变化仍先走 Proposed Change。
6. **接联机**：恢复 Session → LobbyRoom → Facilities → Avatar 的顺序，检查地图版本/设施索引、Owner 输入、服务器校验、结果广播与退出清理。
7. **最后决定资源归档布局**：通过 Unity API 移动 Scene/Prefab 等资产并保留 GUID/.meta。代码和美术可以被 demo 与新地图同时引用；不要为“归档”复制出第二套 Movement/Player 命名体系。

## 10. 回归依据与本次验证边界

现有测试资产：

- [PlaygroundTests](../Assets/Tests/PlayMode/PlaygroundTests.cs)：移动、碰撞、跳跃、姿态、镜头、网络拥有权及设施基础流程。
- [MapLayoutTests](../Assets/Tests/PlayMode/MapLayoutTests.cs)：地图出生点与门/马桶/纸张/工具箱接入。
- [DressingRoomTests](../Assets/Tests/PlayMode/DressingRoomTests.cs)：换装、面板、离线/Host 选图与返回。
- [PlayMode AccuracyTests](../Assets/Tests/PlayMode/AccuracyTests.cs)：类名 ToiletingTests，对准、结算与清洁集成；不要按类名猜测为不存在的 ToiletingTests.cs。
- [EditMode AccuracyTests](../Assets/Tests/EditMode/AccuracyTests.cs)、[OutfitSelectionTests](../Assets/Tests/EditMode/OutfitSelectionTests.cs)、[LobbyRulesTests](../Assets/Tests/EditMode/LobbyRulesTests.cs)、[ProjectTests](../Assets/Tests/EditMode/ProjectTests.cs)：纯规则与资产接线。
- `scripts/multiplayer.py`、`scripts/server.py`：Development Player 的双进程/服务器加两客户端探针。固定地图路径、索引、设施数量等断言需随新测试场景调整；保留旧 demo 回归，不直接让旧测试宣告新玩法正确。

新版本最少确认：角色可控且仅控制自己、着地和跳跃有效、镜头跟随本地角色、姿态后能恢复、设施组件与引用齐全、占用冲突可拒绝、拾取/投掷与断线不丢道具、清洁可中断、UI 不驱动权威规则、各端状态一致、结束/退出清理完整。旧的准度阈值和失败规则只在决定保留时成为新版本验收条件。

后续按 [TESTING](TESTING.md) 在保存关闭同工程 Unity GUI 后依次运行 validate、EditMode、PlayMode，再按改动范围构建和运行进程探针；不能 GUI 与 batchmode 并行，也不能用 Python 单测代替 Unity 回归。Relay/公网/跨设备需要单独证据。

本次进行了源代码、接口、资源路径和文档检查；未启动 Unity、未运行 EditMode/PlayMode、未构建、未执行联机探针。历史运行记录见 [开发日志](records/status-log.md)，不视为当前未提交工作区已通过测试。

本次只读仓库检查 `py -3 scripts/check.py` 通过（1279 files / 648 asset GUIDs）；三个涉及文档的本地链接检查和已跟踪文档的 `git diff --check` 通过。Python 启动器在受限沙箱内最初未发现解释器，随后在授权环境运行同一仓库检查成功；没有以此代替 Unity 验证。

新地图需专门复验的已知问题：开发状态记录了跳上设施后越过墙体/隔板的风险，以及新男性模型在旧俯视镜头下遮住马桶开口中心的问题。旧角色控制可复用，不代表碰撞布局、镜头构图和新美术比例已经通过新地图验收。旧卷纸跨机器丢掷手感也不能由本机探针替代。

## Semantic Diff

`Semantic-Diff: none`

本次只新增技术复用说明和开发状态入口，不增删改领域语义、代码、资源或既有命名。工作区原有 Glossary/角色修改属于此前工作，不纳入本次 none；以后如果将它们一并提交，PR 必须按实际 Glossary 与实现变化重新声明，不能沿用本次声明掩盖已有变化。
