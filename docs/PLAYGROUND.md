# M1 Multiplayer Playground

## 对抗模式 C：离线验收

捣蛋者由测试直接调用 `RoleRegistry.Set(player, PlayerRole.Saboteur)`。手持卷纸对准马桶长按 E 约 2 秒后塞纸，管路增加 40；手持扳手对准损坏设施长按 F 3 秒修复，手持疏通器对准堵塞马桶长按 F 4 秒清空管路。捣蛋者对准洗手台按 E 接水，再按 E 洒出，积水出现在前方 1.5 米；对准关着且有人使用的隔间门按 E 触发 `BodyEvents.Knock`。各动作的冷却在服务器 `Perform` 重新检查。运行 `python3 scripts/unity.py plumbing` 会通过 Editor API 在 Playground 和 Map2 接上 Breakable，并在工具箱生成 Wrench、Plunger；接线完成后应在两张地图各检查一次。

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

在 Unity Hub 打开当前工程，从 `Assets/Scenes/DressingRoom.unity` 开始完整流程，或直接 Play `Playground` 离线练习。点击 Game 视图取得焦点；WASD 移动、Space 跳跃、鼠标观察、Esc 释放鼠标。

隔间内第一次 C 进入俯视对位，WASD 与鼠标调整位置/朝向；第二次 C 在当前位置蹲下，5 秒内 WASD 修正漂移落点，结束自动站起回到蹲下前的位置。小便池前 C 站立小便 3 秒，鼠标修正落点。其他位置 C 原地如厕 5 秒。对准、结算与数值依据 [GLOSSARY](../GLOSSARY.md)，实现参数集中在 `Core/NeedsRules`。

E 只作用于准星指向的设施：拾取卷纸或地上拖把、工具箱取放、开关锁门、准备房的镜子与大厅门、Map2 的井口、售纸机、维修点与金币。视线未直接命中时取约 15° 窄锥内离视线最近的对象，仍并列再按优先级；坐下、蹲下、爬行或瞄准时（非第一人称）改为按距离与优先级选择，因此坐在马桶上也能关门、上锁和开门。F 只作用于手持物品或自身：未擦拭时持卷纸按 F 用卷纸擦（每卷 3 次，用完消失），没有卷纸按 F 用手擦（记为手脏，后果待定）；持拖把对准污物长按 F 三秒清洁，松开 F、失去焦点、走开或不再朝向污物会中断。未擦拭时 E 拾取卷纸或操作门都不扣恶心值，未擦拭就走出隔间扣一次。卷纸与拖把共用一件携带容量，R 丢掷。全场一个工具箱、一把拖把。门原有开/关/锁顺序保留。

`python3 scripts/unity.py toileting`（菜单 `NatureCalls/Configure Toileting`）通过 Editor API 给现有 `Urinal -2/-1/0` 安装组件、生成 `Assets/Prefabs/Toolbox.prefab` 并在公共区域放置一次。重跑保留已有工具箱和地图资源，不重建地图。

`python3 scripts/unity.py playground`（Windows：`py -3 scripts/unity.py playground`）通过 Editor API **重建原型 Playground、Player/Toilet Prefab 和原型材质**，只在明确需要恢复生成场景时运行；不要用它覆盖人工修改的原型。普通队友使用已提交场景即可，不需要补接组件。脚本保存原 GUID、不改旧场景；Playground 不在构建列表时追加，不改变 DressingRoom 作为启动场景。

`python3 scripts/unity.py lobby-flow`（或菜单 `NatureCalls/Build Lobby Flow`）生成 Lobby 流程资源：外观/地图目录与 `LobbyCanvas` Prefab（已存在则保留）、`Network`/`LobbyRoom` Prefab、DressingRoom 中的界面与镜子/门接线，以及构建列表 DressingRoom → Playground → Smoke。

## 组件边界

- `Player/Posture`：可供交互系统调用的姿态切换与通知；不依赖摄像机。
- `Player/Movement`：CharacterController、角色朝向、俯仰、姿态下的身体与碰撞体调整。
- `Input/PlayerInput`：Input System 将本地键鼠转化为移动、跳跃、观察、姿态与交互调用。
- `Camera/CameraController`：唯一实际 Camera + CinemachineBrain，两个 CinemachineCamera 的优先级和 Blend；仅绑定本地角色。
- `Player/PlayerNeeds`：急迫值与恶心值。规则在 Core `NeedsRules`：急迫值上限 100、从 0 涨满 60 秒、开局初值随机 0–40、如厕中不涨、大便结束清零、小便减半；隔间外如厕 +10 恶心值；任一项到阈值（100）全队失败。离线由 Playground 驱动，联机由房主驱动并经 `NeedsSync` 的 `SharedUrgency`/`SharedDisgust`/`SharedUnwiped`/`SharedHandsDirty` 同步；地图 HUD 的 `NeedsView` 显示本地玩家的两项数值。三种污物由 `Playground` 从同一对象池取出，联机时类型、位置和马桶索引经服务器拥有的 `Facilities` 的 `SharedMesses` 同步。
- 粪便种类（GLOSSARY `Mess/StoolKind`，2026-11-26 起，数值暂定）：大便结算时由服务器或离线 `Facility/StoolOutput` 按 Core `StoolRules` 抽取软便/硬便/拉稀（基础 0.6/0.25/0.15；捣蛋者硬 +0.35；急迫值 ≥ 90% 或失控时稀 +0.35；舒爽度时软 +0.2×舒爽度，舒爽度待 PlayerNeeds 发布后接入）。落地：软便是 `Dropping`（螺旋堆或压扁堆），拉稀是新的 `MessKind.LooseStool`（薄片带气泡，站上去与 WetZone 相同的惯性 8），二者仍走 `IMessSink.AddMess`（带来源）与 `SharedMesses`；硬便是 `Facility/HardStool` 物品（干裂长条或一串小球），走过会被踢飞，E 拾取扣 8 点镇定值，R 丢掷，落进马桶开口时加 45 堵塞并回到存放池，同步沿用 `SharedItems`（每个地图场景运行时生成 6 个存放态硬便，用完时退回软便堆）。进马桶：堵塞值软 +25、硬 +45、稀 +10；管路已堵时拉稀立即倒灌。气味权重软 1.5、稀 1.0、硬 0.4，决定污物上方绿色臭气精灵的数量和大小；脏度权重建议软 1.0、硬 0.6、稀 1.2（`StoolRules.DirtWeight`，由对局的脏度规则取用）。
- 正式污物模型：`scripts/blender/mess_models.py` 用 Blender 后台生成（每个 < 2000 三角面，原点在底部中心），导入 `Assets/Art/Mess/`；`python3 scripts/unity.py mess-art` 配置导入设置、URP 材质与 `Assets/Art/Mess/Resources/MessArt.asset`。运行时 `Mess.Configure` 调用 `MessLook.Apply`，按种类显示软便堆、拉稀、失禁（大摊稀便加压扁堆）、呕吐物、尿迹、倒灌和马桶内壁抹痕；模型挂在单独的 `… look` 对象上，Mess 本身的位置、缩放和清洁判定不变。
- 如厕视觉交互（GLOSSARY `Squatting/统一如厕过程`，2026-11-26，数值暂定）：坐在马桶上大便时把鼠标往下移即低头（`Input/ToiletGaze`），镜头从头顶看向臀部下方，地面出现落点（绿色在马桶开口内，红色在外）；每次坐下约 3 秒的「看」额度，用完视线被拉回、不能再低头。低头时 `Movement.Pitch` > 45°，由 `PlayerNeeds` 按 +1/秒 计入舒爽度，落点漂移 ×0.6。结束前约 1.2 秒大便从臀部挤出并按重力下落：落进马桶有水花，并有 50% 溅到屁股（全屏水滴与「噗通！屁股一凉」字幕，不扣镇定值，是否扣待定）；落在外面溅出棕色小点。小便时视角转为正对小便池，尿流是一条从胯部出发、落在鼠标瞄准点上的抛物线；打在目标区外的池壁上按入射角反弹出飞溅，入射接近正面（余弦 ≥ 0.8）持续 0.35 秒时溅到自己，扣 15 镇定值（`BodyEvents.Splash(OwnRebound)`，每次小便最多一次，联机经服务器）。舒爽增益开始时头顶出现金色闪光。均为本机表现，其他玩家看不到尿流和下落；目前没有声音资源。
- `Facility/Toilet`：姿态 Anchor、内部可见目标与占用（同一时间一人，站起释放；联机时由房主判定并经服务器拥有的 `Facilities` 的 `SharedFacilityState` 同步）；`TargetCenter`/`TargetRadius` 表示目标区；Anchor 只识别设施，坐下不再吸附。没有冲水状态。使用哪个马桶由 `StallDoor.Containing`（隔间门后方且在隔间范围内）决定。`WCStall.prefab` 的马桶由 `python3 scripts/unity.py toilet-anchors`（菜单 `NatureCalls/Configure WC Stall Toilet`）配置。
- `Map/Playground` 出生点：`Spawns[i]` 对应房间槽位 i（房主为 Spawn 1）。由 `python3 scripts/unity.py spawn-points`（菜单 `NatureCalls/Place Playground Spawn Points`）以 Spawn 1 为起点在场景中实测放置：脚下有地板、站立胶囊不碰撞、不在隔间内、与 Spawn 1 之间无遮挡，间距至少 1.2m。修改房间布局后重新运行。
- `Facility/StallDoor`：驱动 WCStall Prefab 的 WC 门扇与独立碰撞体，依据玩家位于隔间内外处理打开、关闭、上锁和解锁。
- `Facility/PlanarMirror`：为每个 WC 镜面实例创建独立运行时 RenderTexture；第一人称主摄像机隐藏本地身体时，镜面摄像机仍可渲染本地 Brown 角色。
- `Facility/Item`：卷纸与拖把共用拾取、容量与丢掷机制；`PaperRoll` 额外维护剩余次数。检测附近玩家并记录拾取者；拾取后随玩家保留并关闭场景渲染与碰撞，R 丢掷后恢复渲染、碰撞及 Rigidbody 运动。联机时由丢掷者本机模拟飞行，其他端经房主转发收到位姿后用 `FollowShared` 平滑跟随，落地后用 `ApplySharedRest` 放到最终位置（见 ARCHITECTURE「网络模型」）。
- `Network/Session`：官方 MPS Session + Relay 的创建/加入/离开、匿名认证与错误状态；无需云端的 NGO 直连；Lobby 模式下只分配槽位并生成 LobbyRoom。跨场景保留，场景自带的重复对象自毁。
- `Network/LobbyRoom`：房间成员、准备、外观与阶段（等待/选图/对局），地图加载完成后统一生成玩家。
- `Network/Avatar`：Owner 本地控制、姿态 NetworkVariable 与镜头绑定生命周期；`Network/AvatarAppearance`：出生颜色与外观编号；`Network/NeedsSync`：急迫值与恶心值同步；`Network/FacilitySync`：门、卷纸、马桶占用与黑色小球的共享状态和请求。
- `Map/Playground`：原型资源引用、4 个出生位置与离线练习（联机对局时不生成离线玩家）。
- `Map/MapCatalog`、`Map/Match`：可选地图与本局模式、场景切换。
- `UI/MatchExit`：地图左上角「退出」按钮（`Assets/UI/MatchHud.prefab`），联机时先离开房间再回到准备房。
- `UI/Lobby`：`LobbyView`（界面引用）、`LobbyController`（换装面板、Lobby、选图行为）、`BackgroundBlur`（背景虚化）。
- `Editor/Playground`：用 Unity API 创建场景、材质、URP 设置和 Prefab。

保留原逻辑目录，不建立同义的 Networking/Environment 体系。新增一个 `NatureCalls.Runtime` 总程序集是为了让 Editor 和测试能引用实际组件；没有逐模块拆程序集；Core 已包含本次准度与需求规则。

## 对抗模式施工版

从大厅开始一局后，服务器先发布 4 秒 Reveal，再进入 Playing；默认局长 480 秒，最后一波后保留 45 秒收尾。离线直接播放 Playground 时可在命令行加入 `-saboteur`，或设置 `MatchDirector.ForceSaboteur = true` 做捣蛋者路径测试。每局身份通过 `LobbyRoom.Roles` 同步，客户端角色由 `RoleRegistry` 读取；`MatchContext.Phase`、`Elapsed`、`Tidiness` 和 `Schedule` 是 UI/身体逻辑的只读入口。

污物统一经过 `Playground.AddMess`/`Facilities.SharedMesses`，`Mess.SaboteurSource` 表示来源，`DirtField.Total()` 经 `MatchRules.Tidiness` 计算共享干净值。结算条件是干净值归零（捣蛋者胜）或最后收尾结束时仍大于零（保洁队胜）；急迫值和镇定值不再直接结束对局。 `MatchContext.Winner`、`EndReason`、`Settlement` 提供结算快照；Ended 阶段默认停留 8 秒。随机事件通过 `MatchDirector.LastEventText`、`EventSerial`、`BlackoutRemaining` 暴露给 UI，服务器同步事件造成的 Pipe、售纸机和高峰变化。

验证顺序：先关闭同工程 Unity GUI，再运行 `python3 scripts/unity.py test-editmode`、`python3 scripts/unity.py test-playmode`、`python3 scripts/unity.py build-server`，最后运行 `python3 -m unittest discover -s scripts/tests` 与 `python3 scripts/check.py`。

## Lobby 流程

地图选择提供原 Playground 与「模块厕所（MapLayoutPrototype）」。后者是已存在模块布局的可玩接入，使用同一个 `Map.Playground` 组件和 Player Prefab；没有新增运行时地图框架或依赖。通过大厅进入时由 `Match` 加载，联机由 `LobbyRoom` 等待服务器及所有成员完成加载再生成玩家；只给拥有者绑定地图的本地镜头。

`py -3 scripts/unity.py map-integration`（菜单 `NatureCalls/Connect Layout Map To Lobby`）通过 Unity API 为现有布局补齐镜头、出生点、门/马桶/卷纸/工具箱接线、HUD、地图目录和构建列表；不重新生成布局、覆盖原 Playground 或改变模块 Prefab。普通使用不需要重跑。漏水、阀门、应急设施及损坏/污渍装饰保持视觉展示，本次不赋予新的规则。

`python3 scripts/unity.py map2-integration`（菜单 `NatureCalls/Connect Map2 To Lobby`）通过 Unity API 为 Map2 接线：出生点、隔间、工具箱、井口、`PaperVendingMachine`（售纸机与金币）、`WetZone`（每块湿地一个）和 `Map2Repair`（A 支路维修原型），并删除失效的脚本引用。修改这些组件的字段后重跑。
`python3 scripts/unity.py plumbing`（菜单无独立入口）通过 Unity API 为 Playground 和 Map2 添加 Sink、Pipe，并把用水装置接到对应管路；Map2Repair 同时获得该 Pipe 的疏通引用。

1. 打开 `DressingRoom` 并 Play。走到镜子前、准星对准镜子按 E 打开换装面板，点选外观预览，「确认」保存（写入本机 PlayerPrefs），「取消」恢复。
2. 走到镜子左侧的门、对准门按 E，进入双隔间联机房：左隔间是局域网 / IP 直连，右隔间是课程服务器房间号（也接受 Relay Join Code）。走近马桶、看向水面约 0.3 秒，镜头低头看水面，水面上出现联机菜单；这时角色停住、鼠标放开，**鼠标指针就是准星**。
   - 首页有「单人离线测试」「创建房间」「加入房间」「返回准备房」。点「创建房间」立即建房，水面切到房间页，显示房间号和玩家列表。点「加入房间」进入「隔间大厅」页：上方按房间号列出现存房间，点一下直接加入；也可在下方输入框填房间号（左隔间填 IP:端口），按回车加入，或结束输入后把鼠标指到冲水按钮上按 E 冲水加入；「返回」回到首页。输入框在输入状态时按 E 只会打字，不会冲水。水面文字像沉在水里，会随水流缓慢波动，所有内容都在马桶水面以内；每次点击水面都会荡开水波。裸 IP 默认 7777，同机可用 `127.0.0.1`，课程专用服务器入口为 `49.233.132.244:8000`（部署状态见 DEVELOPMENT_STATUS）。
   - **单人离线测试**：水面菜单里的同名按钮，点击后进入地图选择；选地图或「随机地图」，再指着冲水按钮按 E 确认，加载该地图，本地玩家穿着所选外观。
   - Esc 退出水面菜单，镜头回到第一人称。
3. 房间内非领队点「准备」，领队在 2–4 人且其他人都已准备时点「开始游戏」，随后领队选择地图，再用鼠标指着冲水按钮按 E 确认出发（旋涡吸入转场），其他人等待；房主模式下领队为房主，专用服务器按进入顺序确定领队并在其离开后接任。所有人加载地图后生成角色，外观同步给其他玩家。
4. 对局中按 Esc 释放鼠标，点左上角「退出」离开房间并回到 DressingRoom。房主离开时客户端回到准备房并显示原因。

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

Inspector 参数位于场景 `Local camera`：Eye Height、Third Person Distance/Height/Pitch、Shoulder Offset、Transition Duration、Camera Collision Layers/Radius。当前没有墙体全局隐藏。C 在隔间外可在任意位置测试姿态；要验证马桶内部构图，请走进隔间按 C。

坐在马桶上（隔间内先 C 对位再 C 蹲下）时，隔间太窄放不下正后方镜头，第三人称改为从上方俯视：Inspector 的 Seated Distance（默认 1.9）与 Seated Pitch（默认 80°）控制距离和俯角；隔间外按 C 进入的如厕姿态仍用正后方镜头。5 秒后自动站起，回到坐下前的位置。

旧场景需要应用上述参数时，可以运行菜单 `NatureCalls/Center Playground Camera`；关闭 Editor 后也可用 `python3 scripts/unity.py camera`（Windows 用 `py -3`）。这只保存 Playground 的四个镜头参数，不重新生成地形、Prefab 或材质。已更新的仓库场景无需再次运行。

## 多人联机实测（无需 Unity Cloud）

两台电脑各开自己的 Unity Editor 即可，不需要打包：

1. 两人切到同一分支的同一 Commit，打开 `DressingRoom` 并 Play。
2. 房主走进左侧门，点「创建房间」。未关联 Cloud 时这是局域网房间，房间号显示房主的 IPv4 地址，端口 UDP 7777。macOS 首次会询问是否允许 Unity 接受传入连接，Windows 会弹防火墙提示，需要由本人允许。
3. 加入方走进左侧门，点「加入房间」，在隔间大厅点选房主的房间或输入房主地址后回车，再点「准备」。
4. 房主在人数满足（2–4 人）且其他人都已准备时点「开始游戏」，再选择地图或「随机地图」。
5. 对局中按 Esc 释放鼠标，点左上角「退出」回到准备房；房主退出时，其他人也会回到准备房并看到原因。

同一台电脑双开：Window → Multiplayer → Multiplayer Play Mode 启用 Player 2，或一个 Editor + 一个 Development 构建，加入方填 `127.0.0.1`。

校园网等公共网络的注意事项：

- 直连要求两台电脑之间 UDP 7777 可达。很多校园 Wi-Fi 开启了终端隔离，或把设备放在不同子网/NAT 后面，这时加入方会超时。能 `ping` 通不代表 UDP 一定放行。
- 房主有多块网卡（有线与无线、VPN、虚拟网卡、Tailscale）时，大厅先显示最可能可达的地址（有网关的网卡、局域网网段优先），其余列在「其他」后面；跳过 `169.254.x.x` 自分配地址（没连上网络的网卡）。仍不确定时以系统网络设置中的实际地址为准，两台电脑地址的前三段应相同（如都是 `172.20.10.x`）。
- 加入方报 `Failed to connect to server` 且 Editor 日志出现 `Baselib failed to send`：数据包没能从本机发出，通常是填的地址不在本机能到达的网段（例如 `169.254.x.x`）。macOS 15 及以后还需在「隐私与安全性 → 本地网络」中允许 Unity。
- 直连不通时：两人连同一个手机热点或路由器；或用组网工具（如 Tailscale、ZeroTier）后输入其虚拟局域网地址；或关联 Unity Cloud 后使用 Relay Join Code（见下一节）。

直接打开 Playground 场景时没有连接菜单，只有离线练习和左上角「退出」；不经过大厅的双进程直连诊断使用 `scripts/multiplayer.py`（见 [TESTING](TESTING.md)）。

直接连接使用官方 NGO/UnityTransport，是无云端诊断路径，不提供 Join Code、互联网穿透或 Session 服务。仅本机双进程已自动验证；MPPM GUI、真实 LAN 和 Windows 组合仍需人工验收。MPPM 的 Library/虚拟玩家数据不进入 Git。

## Relay / Join Code 的最少人工配置

2026-11-03 已关联 Cloud 项目 NatureCalls（`ProjectSettings.asset` 的 `cloudProjectId`，组织 `herryying30`）。拉取后无需再关联即可使用 Relay；队友只有在需要查看 Dashboard 时才要加入该组织。以下步骤保留给更换项目时参考。关联后大厅不再提供局域网建房，只能用 IP 加入局域网房间；大厅建房的 PlayMode 测试也会走真实 Relay，需要联网。

1. 团队在 Unity Cloud 中创建/选择共同项目及组织；登录、组织权限、服务条款和可能的计费启用由人类负责人处理。
2. Editor → Edit → Project Settings → Services 关联该 Cloud Project；按 Dashboard 提示启用 Multiplayer 所需的 Relay/Lobby，确保 Authentication 可匿名登录。双方使用同一个项目和环境；场景 `Session.EnvironmentName` 默认 `production`。
3. 关联后通过 Editor 保存必要项目设置，检查 diff 只包含共享标识与预期设置；不提交 Token、账号缓存或密钥。所有构建需重新生成以包含项目标识。
4. 关联后旧版（非水面）大厅的「创建房间」自动改用 Relay；水面大厅左隔间始终建局域网房间、右隔间建课程服务器房间，Relay 只能在右隔间输入 Join Code 加入。旧版大厅中：房主把显示的 **Session join code** 发给队友，队友在「加入房间」输入。不是手工创建 Relay allocation 或复制底层 Relay Code。
5. 分别测试 Mac Host → Windows Client 和 Windows Host → Mac Client，记录同一 Git SHA、操作、错误及退出结果。

代码通过 MPS `CreateSessionAsync(...WithRelayNetwork())` / `JoinSessionByCodeAsync` 使用官方连接生命周期；不同时手动 StartHost 干预 SDK。每进程使用独立匿名认证 profile，便于同机测试；不是正式账号系统。未关联 Cloud 会显示原因并保留离线角色；空 Code 会提示输入。真实服务权限、配额、过期 Code、服务端异常仍需关联后验收。

**Relay over WebSocket**：Relay 默认走 UDP/DTLS。有些校园网或公司网络屏蔽 UDP，但放行 443 端口的 HTTPS 流量，这时可以改走安全 WebSocket（WSS）。WSS 仍然使用 Unity Cloud 的 Relay 服务，并不是脱离 Unity Cloud 的联机方式，它只是换一种连接协议。实现方式：
- 建房和加入都通过 `WithNetworkOptions(new NetworkOptions { RelayProtocol = WSS })` 选择协议，并把 `UnityTransport.UseWebSockets` 设为 true。
- 默认协议连接失败时自动改用 WSS 重试一次；重试成功后记住这个选择（PlayerPrefs `NatureCalls.RelayWebSockets`），以后直接用 WSS。
- 启动参数 `-relay-wss` 强制使用 WSS，例如 `open -n NatureCalls.app --args -relay-wss`。
- 连接成功时状态行末尾显示「(WebSocket)」。局域网直连始终是 UDP。

房主和加入者可以分别用不同的协议；Relay 是否支持这种混用，以 Unity 文档为准，本项目尚未用两台机器验证。

## 验证状态与限制

## 对抗身体规则（feat/versus-body）

离线 Playground 中由 `PlayerNeeds` 驱动两条急迫值：`Bladder`、`Bowel`，`Urgency` 是两者最大值的兼容读数。UI 可读取 `Composure`、`InEndurance`、`EnduranceIntensity`、`Vomiting`、`VomitProgress`、`ComfortIntensity` 和 `ComfortBoostRemaining`；事件 `OnAccident`、`OnRelieved`、`OnVomit` 可用于统计与表现。移动限制通过 `IMovementEffectSource` 自动合并。网络模式由 `NeedsSync` 发布相同状态，极限忍耐只发布布尔值。

对抗身体回归使用 `python3 scripts/unity.py test-playmode --test-filter NatureCalls.Tests.VersusBodyTests`；该组覆盖分段限制、极限忍耐、溅射/敲门、呕吐暂停与溅射、捣蛋者便意速率。完整回归仍使用不带筛选器的 `test-playmode`。

实际日志及测试结果见 [M1 验收记录](records/m1-2026-10-20.md)，2026-10-30 居中镜头回归见 [开发记录](records/status-log.md)。本机通过与待验收项分别列出；直连不能证明 Relay 可用。

原型以可调镜头验证为目标；马桶有一人占用和准度结算，但没有排队或冲水，不要把它当成正式如厕系统。Owner-authoritative 移动只用于 M1 响应速度，设施与需求结算以 IsServer 为准，兼容 Host 与专用服务器；Tidiness 尚未实现。没有 Host 迁移、完整大厅、玩家间推挤或正式动画。隔间外 C 原地如厕不保证有马桶构图；马桶 Anchor 是可复现的可见性测试位置。

## 官方依据

- [Sessions + NGO](https://docs.unity.com/en-us/mps-sdk/tutorials/build-your-first-session)
- [Casual co-op quickstart](https://docs.unity.com/en-us/multiplayer/quickstarts/casual-co-op-quickstart)
- [Relay + NGO](https://docs.unity.com/en-us/mps-sdk/tutorials/relay-and-ngo)
- [Create session](https://docs.unity.com/en-us/mps-sdk/create-session)
- [Cinemachine Third Person Follow](https://docs.unity.cn/Packages/com.unity.cinemachine%403.1/manual/CinemachineThirdPersonFollow.html)

实现 API 还直接核对了本次安装包的源代码，避免使用旧版 Cinemachine 2 或 NGO 旧继承式权限示例。

## 表现层读取接口

本地玩家 `Input/PlayerInput` 的只读属性：`Phase`（None/Aligning/Holding/Urinating/Cleaning）、`TargetCenter`、`TargetRadius`（世界坐标/米）、`CurrentLanding`（世界坐标）、`Elapsed`/`Duration`（秒）、`CurrentAccuracy`（0–1 时间比例）、`Unwiped`、`HeldItem`（Item，具体为 PaperRoll/Mop）、`PaperRemainingUses`、`CleaningProgress`（0–1）、`FocusPrompt`、`FPrompt`；需要刷新当前目标时调用 `RefreshFocus`。`Stage.None` 时瞄准值为上一次记录，不应用于显示目标区。另有 `TargetAxes`（目标圈所在平面的两个轴：马桶为地面，小便池为墙面）。`UI/ToiletingHud`（在 `MatchHud.prefab` 上，由 `python3 scripts/unity.py match-flow` 生成）只读取这些属性：底部显示阶段提示、进度条与实时准度（按结算阈值显示绿/黄/红），对位与如厕时在画面上投影目标圈与落点（落点在圈内时变绿），大便后未擦拭时显示红色提醒，左下角显示手持物品与卷纸剩余次数。布局、颜色与字体都在 Prefab 中，美术可直接替换；目前为纯色占位，没有图标与动画。

Owner 本地累计落点在圈内的时间；结束经 `FacilitySync` 上报准度与玩家位置。服务器验证请求者、开始记录、最低时长、设施占用、所在位置以及数值范围后应用需求和污物。此模式不验证客户端每一帧轨迹，不是反作弊方案。清洁服务器记录开始时间，并要求持续心跳、距离、朝向及持有拖把；松开时主动取消，缺失心跳也取消。
## 联机大厅补充

Map3（后室）接入：运行 `python3 scripts/unity.py map3-integration`。两段可走区通过 `Map/Portal` 循环连接，缓冲段只保留静态外观；本版本不做缓冲段真实状态同步、不加入恐怖元素，也不做小便池。

远程隔间的「创建房间」向 broker 请求随机三位房间号；在远程隔间选「加入房间」时，水面列出 broker 上的现存房间（`GET /rooms`，每 3 秒刷新，服务器不可达时显示原因），点选或输入三位数字即可加入；局域网隔间继续输入 `IP:端口` 直连。大厅监听 UDP 47777，自动列出同一局域网的直连 Host；信标 4 秒未刷新就移除，满员或已进入选图的房间显示但不可点击。

冲水：准星对准无人马桶按 E；堵塞时会倒灌。洗手：用手擦拭后，准星对准洗手台按 E 清除脏手。卷纸每卷 6 单位；完美、弄脏马桶、落地分别需要 1、2、3 单位，纸量不足时保留未擦拭状态。
