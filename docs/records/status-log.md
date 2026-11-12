# 开发记录（按日期）

本文件收集从 [DEVELOPMENT_STATUS](../DEVELOPMENT_STATUS.md) 和 [PLAYGROUND](../PLAYGROUND.md) 移出的逐次变更说明，按原文保留，作为历史记录；当前状态以 DEVELOPMENT_STATUS 为准。新的阶段性记录追加到末尾。

## 2026-10-20 至 2026-11-02 开发状态记录（原载 DEVELOPMENT_STATUS）

本任务未修改 GitHub 默认分支或保护规则；2026-10-20 读取到默认分支仍为 `dev1019`。旧治理记录中的权限限制不能视为已经解除，本次不绕过 Review 或更改长期分支。

2026-10-30 按用户反馈将 Squatting 镜头调整为正后方居中近景，距离 1.6、目标高度 0.8、俯角 50°、横向偏移 0；不变更角色或网络状态机制。源码、已有场景与生成默认值同时更新，最新回归记录见 [PLAYGROUND](../PLAYGROUND.md)。原 10 月 20 日验收记录保留为历史快照。

2026-10-31 按用户指定设施与 WC Prefab 实际 Renderer Bounds 重新生成 Playground：房间最终调整为 8m×10m 封闭空间；Back Wall 中央的三个紧凑隔间均为同一个 `WCStall` Prefab 实例，每个实例组合 WC `SM_toilet_cubicle` 与居中的 `SM_toilet_bowl`。Right Wall 中央放置三个 WC `SM_urinal`，用两块短隔板分开；Left Wall 中央放置两个 WC `SM_sink` 及上方 `SM_mirror`；左后角架子放置三个 WC `SM_toilet_paper`，Front Wall 靠小便池侧三分之一位置放置第二个架子。导入的 Peasant Nolant Renderer 实测高 2.043m，四组外墙统一采用 3.00m 高的 `Wall2m`（0.75m 头顶余量并按 0.25m 向上取整）。30m×24m 旧布局和用户先前试摆的 WC 对象保留为停用的 `legacy` 备份，后续确认无需回退时再移除。当前设施仅为美术与碰撞模块，未实现门、冲水、占用或取用规则；Unity GUI 检查未产生新的异常，尚未在 GUI 开启期间并行运行 Unity 测试。

同日继续按实例 Renderer Bounds 将左侧洗手池和镜子、右侧小便池和隔板贴齐对应墙体内表面，统一保留 0.01m 视觉间隙；左后置物架贴合 Left/Back 两面墙并同步移动架上卷纸，前侧置物架转向 90° 后贴合 Front Wall。该调整仅修改 Map/Art 的空间摆放，不增加设施交互语义。

2026-11-01 将导入的 `Peasant Nolant Blue` 作为嵌套 Prefab 接入现有可复用 `Player` Prefab，旧方块身体保留为停用的 `legacy` 子对象；CharacterController 与站立高度按实际 Renderer 高度设为 2.05m。Playground 离线练习继续复用既有 Input System WASD、Movement、CameraController 与网络 Player Prefab 引用，并修复缺失的第三人称 Cinemachine rig；场景运行时只有 `Local camera` 的 AudioListener 启用。Unity GUI 验证记录显示本地玩家 `controlled=True`、输入启用、Nolant Renderer 生效；WASD 输入探针经真实 InputAction 链路使角色在 20 个编辑器帧内由 `(0.000,0.080,-1.000)` 移动至 `(0.000,0.080,0.292)`，位移 1.292m。该工作仅完成已批准 Player Standing/WASD 的实现与美术接入，`Semantic-Diff: none`；GUI 开启期间未并行运行 batchmode 测试。

同日按用户后续反馈将 Player 的嵌套美术替换为 `Peasant Nolant Brown`，并生成绑定 URP/Lit 的 Brown 兼容材质；Blue 版本保留在停用的 `legacy` 子树。第一人称隐藏改为主输出摄像机排除本地身体专用层，镜面摄像机仍渲染该层；两个 WC 镜面实例各自创建运行时 RenderTexture，避免共享纹理互相覆盖。`WCStall` 原先覆盖整个 cubicle 的 BoxCollider 已替换为左右墙和后墙三个碰撞体，前方入口保持开放。Unity GUI 探针确认两个镜面有效、Brown Renderer 位于镜面视锥（`mirrorVisible=True`），并通过真实 W InputAction 将角色从入口前方 `(−0.350,0.080,1.823)` 移动至隔间内部 `(−0.350,0.080,2.621)`。这是既有 Player Standing/WASD 与 Map/Art 的技术修复，`Semantic-Diff: none`。

同日按用户明确授权扩展 `PaperRoll` 与 `Input`：Playground 中三个卷纸均可在玩家靠近后按 E 拾取；E 优先拾取最近卷纸，没有可拾卷纸时保留原有马桶交互。拾取状态记录具体玩家，卷纸随玩家保留并关闭 Renderer/Collider；每名玩家同时最多携带一个卷纸，按 R 可将其向前丢掷并恢复 Renderer、Collider 与 Rigidbody 运动，之后可以再次拾取。尚未实现使用、消耗和网络同步。`Semantic-Diff: changed`，影响 `NatureCalls/Gameplay/Facility/PaperRoll` 与 `NatureCalls/Infrastructure/Input`。

同日按用户明确授权扩展 `Player/Standing` 与 `Input`：Playground 本地玩家在站立且 CharacterController 着地时可按 Space 起跳，空中与如厕姿态拒绝再次起跳；继续复用现有垂直速度和重力。`Semantic-Diff: changed`，影响 `NatureCalls/Gameplay/Player/Posture/Standing` 与 `NatureCalls/Infrastructure/Input`。

同日按用户确认的位置判断逻辑扩展 `Door` 与 `Input`：当前 `WCStall.prefab` 直接复用 WC 包的独立 `Cubicle/door` 门扇，并增加同步旋转的 BoxCollider；三个隔间实例继承同一 Prefab。附近玩家按 E 时，打开门关闭；关闭门在隔间外打开、在隔间内上锁；锁门阻止外部操作，内部 E 解锁并打开。状态与门交互尚未网络同步。`Semantic-Diff: changed`，影响 `NatureCalls/Gameplay/Facility/Door` 与 `NatureCalls/Infrastructure/Input`。

同日修正 `WCStall.prefab` 的设施朝向：将 WC 包门扇由靠后墙端移至隔间入口端，并将马桶绕 Y 轴旋转 180° 朝向入口；三个实例继续继承同一 Prefab，门碰撞 pivot 随新位置重建。该调整只修复 Map/Art 空间摆放，`Semantic-Diff: none`。

同日继续校正 `WCStall.prefab` 的门体装配：门扇横向中心与隔间墙体中心对齐，底部落到隔间地面；门扇按自身厚度向隔间内侧嵌入，使外侧表面与入口边缘贴齐、中心面不再与墙体入口边缘重合。门碰撞 pivot 随门扇位置重建，三个隔间仍复用同一 Prefab。该调整只修复 Map/Art 空间摆放，`Semantic-Diff: none`。

2026-11-02 将三个 `WCStall` 实例按实际 Renderer Bounds 作为整体对齐到 8m 房间地面中心，并保留实例间距。保留用户调整后的三个 Cubicle/门框实例位置与旋转，只将门扇沿门框局部深度轴平移，使门外表面贴合门框平面；各自独立的碰撞 pivot 按实际门扇世界姿态重建。原先错误留在 `WCStall` 根坐标系中的三面墙 Collider 改为 Cubicle 子对象并按 Cubicle 局部 Bounds 重建，后墙位于 `min.z`，`max.z` 门口保持开放；因此 Collider 会随门框平移和旋转，不再侵入相邻隔间入口。门状态切换后立即同步物理 Transform，避免重复开关后视觉门与碰撞位置不一致。运行时回归覆盖三扇门各两轮“外侧打开、通过入口、关闭、关门阻挡”。这是已批准 Door/Input 行为与 Map/Art 的修复，`Semantic-Diff: none`。

2026-11-01 已获用户确认：DressingRoom 为独立换装准备场景；玩家进门后进入联机大厅，房间达到四名玩家时自动开始正式游戏。本次完成准备场景的本地镜子互动与换装 UI 占位，以及门前 E 键进入独立 LobbyUI 场景和四人自动开始 Mock；不包含真实外观替换、真实联机、跨客户端房间加入或进入正式游戏场景。

## 2026-10-30 居中镜头回归（原载 PLAYGROUND）

按用户最新反馈将侧后方远景改为正后方近景；语义变化为 `NatureCalls/Gameplay/Player/Posture/Squatting`，批准来源记录于 Glossary。

- Editor API 更新只改变场景四个镜头字段，场景 GUID 和其他对象引用保留。
- PlayMode **5/5 通过**，新增实际镜头的横向居中、后方距离、朝向和人物屏幕中心断言；原马桶内部可见、全身入镜、Blend、返回方向断言仍通过。已查看实际 `squatting.png`。
- 导入/编译通过；EditMode **4/4**、Python **17/17** 通过。
- 本机日志：`Artifacts/20261029T151517Z-ce5a2d-camera/`、`Artifacts/20261029T151919Z-059aea-test-playmode/`、`Artifacts/20261029T152006Z-3e0dc2-test-editmode/`。目录使用 UTC，以上对应本地 10 月 30 日。
- 原有三份未提交渲染设置保持原样，不包含在修复提交中。本次不重复验证 Relay/Windows/双进程联机，也不重建旧 `.app`；用更新后的 Editor 场景进行人工手感复验，旧构建不会自动获得新镜头。

## 2026-11-02 DressingRoom → Lobby → 地图流程

按用户批准的语义（见 Glossary 2026-11-02 批准来源）合并两套换装并实现大厅与地图选择。`Semantic-Diff: changed`：新增 `NatureCalls/Content/Map/MapSelection`，修改 `NatureCalls/Gameplay/UI/Lobby` 与 `NatureCalls/Content/Map/DressingRoom`。

- 保留 DressingRoom 镜子换装与大厅入口，移除 ChangingRoom 场景/换装台/HUD/简易跟随相机与独立 LobbyUI 场景及其 Mock；`ChangingRoomPlayer.prefab` 更名为 `DressingRoomPlayer.prefab`（GUID 保留）并移除网络组件，避免 Host 启动时被当作场景网络对象生成。
- 新增 `OutfitCatalog`、`LocalProfile`、`MapCatalog`、`Match`、`LobbyRoom`、`LobbyView`/`LobbyController`/`BackgroundBlur`、Core `LobbyRules`/`MapSelection`；`Avatar.OutfitIndex` 同步外观；Session 改为跨场景持久并支持 Lobby 模式；`Editor/LobbyFlowBuilder` 生成全部资源，构建列表改为 DressingRoom → Playground → Smoke。
- 本机 Unity 6000.3.26f1 / macOS：`lobby-flow` 通过（`Artifacts/20261102T133207Z-7edeb8-lobby-flow/`）；EditMode 10 项 9 通过（`Artifacts/20261102T133213Z-a20fad-test-editmode/`）；PlayMode 9 项 5 通过（`Artifacts/20261102T133219Z-99cbe0-test-playmode/`），新增 DressingRoom 3 项全部通过；失败项均为 2026-11-01 起已失败的 Playground/第三方场景检查，本次未修改相关场景与测试。
- ARM64 Development 构建成功；`scripts/multiplayer.py` 双进程直连（探针从 DressingRoom 启动后切到 Playground）两端成员生成、所有权、移动/朝向/姿态同步、可见远端与退出清理均为 true，仅 `singleCamera` 因镜面摄像机为 false（`Artifacts/20261102T133322Z-95174c-multiplayer/`）。
- 未执行：两名真人经大厅「创建/加入→准备→开始→选图」的联机流程（自动测试以单 Host 绕过人数条件覆盖选图、加载与外观同步）、Relay、Windows。
- 同日补充：地图内 IMGUI `Panel` 菜单（返回 DressingRoom 时会闪现开发菜单）替换为 uGUI 左上角「退出」按钮 `MatchExit`（`Assets/UI/MatchHud.prefab`），Playground 增加 EventSystem，删除 `Panel.cs`；`lobby-flow` 改为保留已有 Canvas/HUD 实例，重复运行不再改动场景。单人离线测试用例增加「退出回到 DressingRoom」断言。EditMode 10 项 9 通过（`Artifacts/20261102T143109Z-582048-test-editmode/`），PlayMode 9 项 5 通过（`Artifacts/20261102T143306Z-92f41c-test-playmode/`），失败项与上条相同。
- 同日补充（测试修复与马桶交互）：Playground 重建时 WCStall 马桶丢失了 Toilet 交互组件，「靠近马桶按 E 进入如厕姿态」在地图里实际不可用。新增 `Editor/ToiletAnchorSetup`（`python3 scripts/unity.py toilet-anchors`）为 `WCStall.prefab` 的马桶补上 Toilet、座位前的姿态 Anchor 与马桶开口处的 Interior 标记；实测座位 Anchor 距门轴 0.89m，门交互半径 1.5m。按用户决定（Glossary 已记录）隔间内马桶优先于门，坐下后再按 E 站起；由此隔间内无法用 E 操作门，记为已知问题。测试修复：场景检查只覆盖 `Assets/Scenes`；镜头数只统计屏幕摄像机（测试与双进程探针）；撞墙测试从场景测量；马桶测试改为在子对象中查找并先开门。结果：EditMode 10/10（`Artifacts/20261102T151349Z-6e81f8-test-editmode/`），PlayMode 9 项 8 通过（`Artifacts/20261102T151355Z-7aafe4-test-playmode/`），仅 `CameraBlendsAndInteriorIsVisible` 失败（隔间内坐下镜头被挤到头部，截图 `squatting.png`）；重新构建后双进程探针两端全部通过（`Artifacts/20261102T151441Z-4864a6-multiplayer/`）。`toilet-anchors` 运行记录：`Artifacts/20261102T151338Z-fef4f9-toilet-anchors/`。

## 2026-11-03 马桶站起、俯视镜头与跳跃越界记录

- 用户反馈坐上马桶后再按 E 无法离开。排查发现上一版马桶 Anchor 按 `WCStall.prefab` 内的布局计算，而 Playground 中三个隔间实例覆盖了布局（实例中马桶靠后墙、门朝房间），导致 Anchor 落在隔间外、与房间后墙碰撞体重叠，站起后被卡住。`ToiletAnchorSetup` 改为在 Playground 实例上沿隔间纵深轴测量，并逐个验证 Anchor 位于隔间内、站立胶囊体不与任何碰撞体重叠（三个隔间均通过，距门轴 0.94m）。站起时回到坐下前的位置。
- 按用户同意，坐在马桶上时第三人称改为俯视（`CameraController.SeatedDistance`/`SeatedPitch`）；Glossary Squatting 已更新并记录批准来源。
- 测试：`CameraBlendsAndInteriorIsVisible` 改为验证俯视镜头，身体遮挡改用烘焙网格射线检测，第一人称隐藏身体改为检查摄像机 Culling Mask（镜子仍可渲染本地身体）；马桶测试增加「站起后回到原位并能走出隔间」。EditMode 10/10（`Artifacts/20261102T153838Z-a88c19-test-editmode/`），PlayMode 9/9（`Artifacts/20261102T153821Z-aee617-test-playmode/`），重新构建后双进程探针两端通过（`Artifacts/20261102T153855Z-da88a4-multiplayer/`），`toilet-anchors` 记录 `Artifacts/20261102T153611Z-e15ec4-toilet-anchors/`。
- 记录玩家测出的跳跃越界问题（未修复）：跳上小便池可以跳出房间；跳上卷纸架可以跳进隔间或跳出房间；跳上马桶可以跳出隔间并卡在隔间顶部的杆子上。
- 同日双机实测（同一 iPhone 热点）：加入方报 `Failed to connect to server`，Editor 日志 35 次 `Baselib failed to send 1 packets`；房主大厅显示的房间号是 `169.254.2.43`（未取得地址的网卡的自分配地址），加入方无路由可达，ping 不通。`Session.LocalAddresses()` 改为跳过 `169.254.x.x`、按「有网关 → 局域网网段 → 其他」排序，大厅显示首选地址并列出其他候选；新增 EditMode 断言。仅以 Unity 自带编译器编译通过，Unity 测试与双机复测待执行（当时 Unity GUI 正在使用）。
- 同日关联 Unity Cloud 项目（`cloudProjectId` 写入 `ProjectSettings.asset`）：大厅「创建房间」由此改走 Relay Join Code。Editor 日志只见 Services 初始化，未记录 Relay 建房结果；Relay 真实联机与 Unity 测试待执行。
- 同日 Relay 双机实测（Mac 房主、Windows 加入）：建房、加入、选图、加载都成功，房主为两名成员各生成了角色，但加入方的角色出生在 Spawn 2 (-3, 0, -2)，与地图左墙重合，被挤到地板下持续下落（日志 y 从 -14.4 降到 -15.3）。四个出生点仍是旧布局坐标。新增 `Editor/SpawnPointSetup`（`python3 scripts/unity.py spawn-points`）在场景中实测放置出生点：Spawn 1 (0,0,-1) 保留，Spawn 2–4 移到 (-1.25,0,-1)、(0,0,-2.25)、(0,0,0.25)（`Artifacts/20261103T025925Z-91b054-spawn-points/`），并新增 PlayMode 出生点检查。EditMode 11/11（`Artifacts/20261103T025946Z-154af5-test-editmode/`），PlayMode 10/10（`Artifacts/20261103T025952Z-adf78b-test-playmode/`）；Host 建房用例在关联 Cloud 后走真实 Relay 并通过。双机复测待执行。

## 2026-11-03 Player 脚底接地修复

- 用户反馈站立角色脚部离地。回归检查实测脚底高出地图地面 `0.067m`；CharacterController 根节点与地面的间隙为 `0.080m`，与 `skinWidth=0.080m` 一致。角色模型此前直接跟随碰撞体根节点，因此把碰撞余量表现成了可见悬空。
- `Movement` 在首帧站立姿态的 SkinnedMesh Bounds 可用后，将 `Body` 一次性对齐到 CharacterController 的接触平面；不改变碰撞体、玩家根 Transform、移动、跳跃或网络同步位置。新增 PlayMode 回归从场景地面射线与实际 Renderer Bounds 验证脚底接触地面。
- 修复前新增用例按预期失败（PlayMode 9/10，`Artifacts/20261103T030302Z-3da449-test-playmode/`）；修复后 Unity 6000.3.26f1 PlayMode 10/10（`Artifacts/20261103T030547Z-f0e5b5-test-playmode/`）、EditMode 11/11（`Artifacts/20261103T030727Z-a06596-test-editmode/`）、导入/编译验证通过（`Artifacts/20261103T030801Z-1594fb-validate/`）。这些是接地分支当时的测试结果；合并后的结果见下条。这是既有 Player/Standing 表现的技术修复，`Semantic-Diff: none`。

- 合入联网分支后，Unity 6000.3.26f1 导入/编译验证通过（`Artifacts/20261103T105454Z-7be5f1-validate/`），EditMode 11/11（`Artifacts/20261103T105725Z-ca9d88-test-editmode/`），PlayMode 11/11（`Artifacts/20261103T105814Z-f0b993-test-playmode/`）；仓库检查 837 个文件/417 个 GUID 与 Python 单测 21/21 通过。真实两机 Relay 复测未执行。

## 2026-11-03 Playground 设施状态同步

- 基线 `eaa77453b38bb09a6a9dacb64f0a94c8d08cb689`，Windows Unity 6000.3.26f1。用户反馈联网中一端拾取卷纸或操作隔间门，另一端看不到变化。检查 Playground 交互路径后确认门与卷纸只有本地状态；马桶姿态、镜子外观和大厅阶段已有各自同步路径。
- 现有玩家 Avatar 接收交互请求，Host 使用原有距离、携带容量及门锁规则验证，再发布隔间门状态和卷纸的携带者、显隐、丢掷位置与飞行位置。卷纸在非 Host 端使用 Host 位置和非模拟 Rigidbody；同一地图的设施按层级路径固定索引。单人离线交互仍直接使用设施组件。
- Unity batchmode 导入/运行时代码编译验证通过（`Artifacts/20261103T113830Z-56d1cd-validate/`）；随后 Unity GUI Test Runner PlayMode 12/12、EditMode 11/11 通过。新增 PlayMode 用例覆盖 Host 发起开门、拾取、丢掷并写入共享列表。GUI 结果未导出 XML；双端本地网络、Relay 和真实跨设备的可见性与丢掷落点仍未执行。`Semantic-Diff: changed`：`NatureCalls/Infrastructure/Network` 增加 Door 与 PaperRoll 状态在玩家间共享的关系；批准来源为用户 2026-11-03 的明确请求，设施交互规则未变。

## 2026-11-03 本地双 Player 设施共享验证

- 测试基线 `e08bb86ccd4a2a212135f5e99966e05ce76e2b5e` 加本次 Development 探针与 Windows 构建入口修改。Host / Client 均为 Windows x86_64、Unity 6000.3.26f1，同一构建；实际解释器为 Codex bundled Python（`py -3` 未登记可用解释器）。
- 新增 `build-windows`，输出独立 Windows Development Player 并核查 BuildReport、回执和产物；不提交生成的 Player 或日志。探针仅在显式 `-m1-probe` 下启用，Release 不包含。
- 验证通过：Unity 导入/编译 `Artifacts/20261103T122405Z-252925-validate/`；EditMode 11/11 `Artifacts/20261103T122606Z-6ebe27-test-editmode/`；PlayMode 12/12 `Artifacts/20261103T122621Z-5807a1-test-playmode/`，两者均有非空且全部 Passed 的 XML；Windows Development 构建 `Artifacts/20261103T122656Z-2ff797-build-windows/`。
- 真实双 Player localhost UnityTransport 验证通过：`Artifacts/20261103T123015Z-d52d2e-multiplayer/summary.json`。Host、Client 均实际发起门开/关/锁/解锁、卷纸拾取与丢掷，两端均验证实际场景携带者、Renderer/Collider 显隐、丢掷落点一致（差值 < 0.15m），角色同步、独立镜头及退出清理也全部通过。步骤标记文件只同步测试时序，不传输或修改游戏设施状态。
- 上述不代表人工键鼠、大厅路径、真实 LAN/Relay 或 Mac/Windows 互联通过；这些仍待验收。更新架构中门/卷纸仍为纯本地状态的过期描述。此次新增测试与构建工具没有引入领域语义；整个设施共享 PR 相对 dev 的声明仍为 `Semantic-Diff: changed`，影响 Network，批准来源沿用上条设施共享请求。PR 合并仍须其他成员 Review，不以本地测试代替。

## 2026-11-04 C 键如厕、马桶占用、全局计时与准备房地板

- 按用户决定（GLOSSARY 已记录批准来源）：所有如厕改由 C 触发。隔间内按所在隔间（`StallDoor.Containing`：门后方且在隔间范围内）坐到该隔间的马桶上，取代按距离选马桶，修复在隔间 B 坐下时选中隔间 A/C 马桶的问题；隔间外原地如厕；再按 C 站起。E 不再处理马桶，坐着时也能关门上锁，原「隔间内马桶优先于门」导致的困在隔间问题随之消失。`Toilet.SeatPose` 为以后的如厕准度机制留位。（历史接口，2026-11-13 已删除，当前使用目标区与玩家对位。）
- 马桶一人占用：离线由 `Toilet` 本地判定；联机由房主检查占用和所在隔间后回复 Owner 坐下，占用经房主 Avatar 的 `SharedToilets` 同步，站起、离开地图时释放。
- 全局计时：联机由 `LobbyRoom.RoundEndTime`（服务器时间）计时，到点后房主 Despawn 角色、阶段回到 Waiting、清除准备，所有端回到准备房并留在原房间；离线由 `Match` 本地计时。时长为 `MapCatalog` 每张地图的 `RoundSeconds`，默认 180 秒；HUD 顶部 `MatchClock` 显示剩余时间。
- 准备房地板闪烁：默认材质的 `Plane` 与 `Floor` 顶面同在 y=0 造成 z-fighting，已关闭 `Plane` 的渲染（保留碰撞体），由 `python3 scripts/unity.py match-clock` 执行（`Artifacts/20261104T115823Z-15a776-match-clock/`）。
- 大厅新增 `LobbyController.PreferLan`：已关联 Cloud 时也可创建局域网房间。首次 PlayMode 运行中 Relay 建房超过 10 秒导致建房用例超时，未完成的 Relay 连接又干扰了后续镜头用例；建房用例改为固定走局域网后稳定。
- 结果：EditMode 12/12（`Artifacts/20261104T120013Z-fc8786-test-editmode/`），PlayMode 15/15（`Artifacts/20261104T115950Z-015d0a-test-playmode/`），macOS 构建 `Builds/macOS/20261104T120020Z-10dee6/`，仓库检查通过。真人键鼠、两台机器联机下的占用与计时尚未验收。

## 2026-11-04 急迫值、恶心值、一次性 C 与全体判负

- 用户试玩反馈：隔间内按 C 仍是原地蹲下；非房主按 R 丢卷纸掉帧。本机日志显示这台 Mac 是房主，用的是新构建；没有非房主一端的日志。新增扫描测试，确认每个隔间从门内到座位、左右两侧都判定为本隔间；另外让 C 在所有构建里写一行 `TOILET_C|…|stall=…|started=…` 日志，方便下次对照。怀疑另一台机器运行的是没重新构建的旧版本，未证实。丢卷纸掉帧记为已知问题，暂不处理。
- 按用户要求（GLOSSARY 已记录，部分数值为 Agent 暂定、待确认）：急迫值上限 100、从 0 涨满 60 秒、开局随机 0–40、如厕中不涨、一次如厕结束后清零；C 改为一次性 5 秒如厕并自动站起；隔间外如厕站起后留下黑色小球、该玩家恶心值 +10；任意玩家急迫值或恶心值到 100 全队失败，按全局计时的结束流程回到房间并显示原因。规则在 Core `NeedsRules`，状态在 Player `PlayerNeeds`（`Player.prefab` 已添加），联机由房主驱动并同步；HUD 的 `NeedsView` 改为显示真实数值（取代 PR #12 的固定占位）。
- `scripts/unity.py match-clock` 更名为 `match-flow`（菜单 `NatureCalls/Configure Match Flow`），同时添加 PlayerNeeds、HUD 数值条与时钟（`Artifacts/20261104T123808Z-05698b-match-flow/`）。PlayMode 测试程序集补上对 `NatureCalls.Core` 的引用。一次因编译错误失败的 batchmode 运行留下了 `Temp/UnityLockfile`，确认没有 Unity 进程后，经用户同意删除。
- 结果：EditMode 13/13（`Artifacts/20261104T123819Z-89889f-test-editmode/`），PlayMode 16/16（`Artifacts/20261104T124015Z-1e07d2-test-playmode/`），macOS 构建 `Builds/macOS/20261104T124203Z-c5b7ef/`，仓库检查通过。两台机器下的人工验收未执行。

## 2026-11-05 卷纸飞行归丢掷者所有

- 原因：非房主丢卷纸时，卷纸由房主模拟，房主每 0.1 秒广播一次位姿，非房主每收到一次就瞬移卷纸，因此一跳一跳；拾取时还会对运动学刚体设置速度，产生警告。
- 改动：丢掷者本机立即模拟，约每 0.1 秒经 RPC 报告位姿，房主校验后写入 `SharedPaperRolls`（历史名称，2026-11-13 改为 `SharedItems`；新增 `Simulator`，`Mode` 增加 `Landed`），其他端用 `PaperRoll.FollowShared` 平滑跟随并沿抛物线外推；静止 0.3 秒后交还房主，丢掷者离开时房主留在最后位置。拾取、丢掷许可仍由房主判定。
- 结果：EditMode 13/13（`Artifacts/20261105T035210Z-5366d5-test-editmode/`），PlayMode 16/16（`Artifacts/20261105T035220Z-b7d16c-test-playmode/`，Host 丢掷用例改为验证 Flying→Landed 交还）；本机双进程探针两端通过，双方各丢一次，落点差 < 0.15 m，日志无运动学刚体警告（`Artifacts/20261105T035332Z-e9d3fd-multiplayer/`）。真实两台机器未复测。

## 2026-11-05 Relay 支持 WebSocket（WSS）

- 目的：在屏蔽 UDP 的网络里仍能使用 Relay。WSS 仍是 Unity Cloud 的 Relay 服务，只是连接协议不同，不是脱离 Unity Cloud 的联机方式。
- 改动：`Session.ConnectRelayAsync` 先用默认协议，失败后自动改用 WSS 重试一次，成功后记住（PlayerPrefs `NatureCalls.RelayWebSockets`）；启动参数 `-relay-wss` 强制使用 WSS；协议通过 MPS 的 `WithNetworkOptions` 设置，并同时打开 `UnityTransport.UseWebSockets`；局域网直连固定为 UDP。
- 结果：EditMode 13/13；默认 PlayMode 16 通过、1 项 Ignored（`Artifacts/20261105T035657Z-0b053b-test-playmode/`）。设置 `NATURECALLS_RELAY_TESTS=1` 后 17/17 通过，真实 Relay 以 WSS 建房并取得 Join Code（`Artifacts/20261105T035758Z-eec05b-test-playmode/`）。同样设置下的前一次运行里，`StandingVisualTouchesTheFloor` 失败过一次（站立模型低于地面 0.083 m，`Artifacts/20261105T035540Z-7d001b-test-playmode/`），重跑没有复现，原因未查明。两台机器经 WSS 联机、以及 UDP 被屏蔽时自动切换都未验证。

## 2026-11-07 按评分标准整理代码结构

- `Network/Avatar` 拆为四个组件，行为不变：`Avatar`（所有权、控制、镜头、姿态同步）、`AvatarAppearance`（身份颜色与外观编号）、`NeedsSync`（急迫值/恶心值同步与如厕结束上报）、`FacilitySync`（门、卷纸、马桶占用、黑色小球的共享状态与请求）。`match-flow` 为 `Player.prefab` 添加新组件，并从 `DressingRoomPlayer` 变体中移除。拆分过程中 NGO 曾把 DressingRoomPlayer 自动登记进 `DefaultNetworkPrefabs.asset`，导致 EditMode 资源检查失败；已恢复该文件，重新运行 `match-flow` 后不再出现。
- 黑色小球改用 `UnityEngine.Pool.ObjectPool`，`Playground.RemoveDropping` 回收，再次出现时复用。
- ARCHITECTURE 新增「分层（MVC）」与「设计模式」两节；IMPLEMENTATION 增加分层与"判定以 IsServer 为准"的规则。专用服务器改造由 Codex 在 `feat/dedicated-server` 上继续，设施同步迁移只需改 `FacilitySync`。
- 结果：EditMode 13/13（`Artifacts/20261107T125105Z-4318f3-test-editmode/`），PlayMode 16 通过 + 1 Ignored（`Artifacts/20261107T125317Z-17f522-test-playmode/`），本机双进程探针两端通过（`Builds/macOS/20261107T125122Z-6c477d/`）。


## 2026-11-07 无房主设施状态与领队

起点 40fbb4b，fetch 后 origin/dev 为 8aaf363；完整阅读任务 Glossary 并核对公共版本差异，领队审批为分支已有 28f304d。全部操作在独立 server 工作副本；没有手动删锁。

- `unity.py detect`：6000.3.26f1；首次 `validate` 通过，Artifacts/20261107T142949Z-d3f676-validate。
- `unity.py facilities`：通过 Unity API 生成、注册和接线，Artifacts/20261107T143117Z-bdb052-facilities。
- EditMode 13/13：Artifacts/20261107T143138Z-4fa7d4-test-editmode。
- 默认 PlayMode 17 passed / 1 Relay ignored，严格运行器失败：Artifacts/20261107T143207Z-16371d-test-playmode；负向正则仍匹配父套件，同结果：Artifacts/20261107T143337Z-8cd84c-test-playmode。
- 明确本地方法名单 PlayMode 17/17：Artifacts/20261107T143505Z-e08271-test-playmode（筛选参数在 summary.json）。Relay 未执行，本次不允许访问 Cloud。
- macOS ARM64 Development 构建通过：Artifacts/20261107T143615Z-a091c1-build-macos；产物 Builds/macOS/20261107T143615Z-a091c1/NatureCalls.app。
- `multiplayer.py` 两端全部断言通过：Artifacts/20261107T143705Z-695a08-multiplayer。
- 仓库检查通过，Python 单测 21/21；Windows/跨设备/Relay 未执行。

Semantic-Diff: changed。落实已批准的 NatureCalls/Gameplay/UI/Lobby 与 NatureCalls/Content/Map/MapSelection 领队规则；没有增加其他语义。设施列表从玩家 FacilitySync 移到服务器 Facilities，玩家 RPC 保留；旧 FindHost 依赖移除。


## 2026-11-07 纯服务器入口与构建

第一步提交 125d0be，PR #18；第二步代码继续在 feat/dedicated-server。再次 fetch 后 origin/dev 仍为 8aaf363，Glossary 无新增差异。

- `validate` 通过：Artifacts/20261107T143907Z-684416-validate。
- 新 ServerProbe 最初编译失败（NetworkList 不提供 LINQ Any）：Artifacts/20261107T144059Z-c68bf2-test-editmode，已改索引遍历。该次异常退出留下 UnityLockfile，下一次命令被锁拒绝；确认没有 Unity Editor/Player、lsof 无持有者后，按本次用户异常退出恢复授权删除本目录锁，未结束其他进程。
- 地址用例首次 19/20（Mono 对带端口括号 IPv6 的 TryParse 忽略端口）：Artifacts/20261107T144158Z-a3a872-test-editmode；修正解析顺序后 EditMode 20/20：Artifacts/20261107T144232Z-234dfb-test-editmode。
- macOS 构建通过：Artifacts/20261107T144301Z-68693c-build-macos；Builds/macOS/20261107T144301Z-68693c/NatureCalls.app。
- 三进程服务器探针各端适用断言通过：Artifacts/20261107T144354Z-3002f5-server；服务器报告六项均 true。首位玩家退出后不再承担接任/返回/空房断言，这些由后续玩家与服务器核验。
- 本地 PlayMode 明确方法名单 17/17：Artifacts/20261107T144441Z-15c7f4-test-playmode；Relay 云用例未执行。
- 原 Host/Client 双进程探针全部通过：Artifacts/20261107T144548Z-a5f553-multiplayer。
- Linux x86_64 Dedicated Server 构建通过：Artifacts/20261107T144555Z-388ad8-build-server；Builds/LinuxServer/20261107T144555Z-388ad8/NatureCalls。Unity 自动添加官方 Linux SDK/交叉编译工具链 1.1.1，保留包锁；现有运行时版本未更改。
- 仓库检查与 Python 21/21 通过。
- SSH 只用指定密钥；预检日志 Artifacts/deploy/preflight.log，主机记录写在该目录，未修改本机 SSH 配置。x86_64、UDP 8000 空闲、约 3.1 GB 可用内存；没有已有 NatureCalls 单元。尚未部署。

Semantic-Diff: none（相对于第一步 125d0be）。本步实现纯服务器运行与运维工具，不增改游戏规则。


## 2026-11-08 JST 课程服务器部署（日志 UTC 2026-11-07）

实现提交 5b03702；PR #19 依赖 #18，暂以 feat/server-facilities 为 base，需人类先合并 #18 后改为 dev；两个 PR 均未合并。本条随后提交仅修改 systemd 配置和记录，不改变已验证的 Unity 代码与构建。

- SSH 单连接上传持续偏慢，停止了本次创建且已核实 PID/命令的上传进程，未结束既有 SSH。改为 8 块并发 SSH，各块均校验成功；整包 SHA-256 为 `ebd72efe5474e11a10d5444f1a8cce71a596789ab14b84d0126601a4af7026e2`，本机与远端一致。Artifacts/deploy/upload.py、part-*.log、upload-complete.log 保存实际命令与结果。
- 安装到 `/opt/naturecalls/releases/5b03702`，current 链接指向该版本；只安装本任务的 naturecalls@.service，启用 8000 一个房间。Artifacts/deploy/install.sh / install.log。tar 忽略 macOS provenance 扩展头并以 0 退出，systemd 初次启动成功。
- 初次启动日志发现 Unity 原生 PlayerPrefs 自动创建了自己的用户目录。先停止本次创建的服务，给单元增加 `StateDirectory=naturecalls-%i`、对应 XDG_CONFIG_HOME 和 ProtectHome。仅 rmdir 清理失败（目录非空），随后把本次新生成的 NatureCalls 数据完整迁到 `/var/lib/naturecalls-8000/unity3d/DefaultCompany/NatureCalls` 并移除空的 DefaultCompany 目录，没有删除其内容或改动其它用户配置；再启动服务。失败与恢复分别记录于 Artifacts/deploy/isolate.log、isolate-recovery.log。
- 最终核验：active/enabled，PID 1844231，NRestarts=0、ExecMainStatus=0、MemoryCurrent=122671104 bytes；`ss` 显示该 PID 监听 `0.0.0.0:8000/UDP`；新 PID 的日志有 `Server listening on UDP 8000.`、Null GfxDevice，没有再次创建用户配置目录。原 DefaultCompany 目录不存在的检查通过。Artifacts/deploy/verify-isolated.log；systemd-analyze verify 退出 0，仅报告系统既有其他单元的兼容性警告，未改动那些单元。
- SSH、系统用户和防火墙配置未修改；没有安装额外软件。仅运行 1 个房间，8001/8002 未启动。应用持久目录由 systemd 管理，CPUQuota=100%、MemoryMax=1G。
- 日志查询：`journalctl -u naturecalls@8000 -n 100 --no-pager`。客户端入口 `49.233.132.244:8000`；公网实际客户端联机未执行（本次联网授权仅包含 origin Git/PR 创建和部署 SSH），不能把监听成功写成公网验收通过。Windows、Relay、真人手感及负载测试未执行。

Semantic-Diff: none。没有增加任何未批准游戏规则。


## 2026-11-08 JST PR #19 构建目标隔离回归（日志 UTC 2026-11-07）

任务起点 4197249875bba4fb3ff222b3085326042b518cb9，分支 feat/dedicated-server。fetch 后 origin/dev 为 8aaf363675668699de7c772085568f5f9a2d268b、origin/main 为 e43122a22c61623e57930c34932912bbcdca4a9a；完整重读 Glossary，保持分支已批准的 Lobby/MapSelection 语义。为遵守本次目录边界，同步阅读记录写在本工作区 Artifacts/target-isolation-sync.txt，不直接访问共享 Git 元数据目录。

复核证据 Artifacts/20261107T150526Z-1f348b-test-playmode/results.xml 显示 Linux 构建后 17 项 SetUp 失败；此前 17/17 位于构建之前，不能覆盖此回归。修复只改 Editor/Python 自动化：build-server 的 finally 在同一项目锁内另起 Editor 恢复本机桌面 Player，验证目标/子目标和 UNITY_SERVER 已关闭；所有非服务器动作也显式选择 Player。选择独立启动是为了在 executeMethod 前完成目标切换与重编译。macOS/Windows 分别使用 osxuniversal/Win64。Ignored 单列，不算测试失败或测试通过；零通过、其他跳过、实际失败仍拒绝。

以下按顺序真实运行，Unity 6000.3.26f1，macOS ARM64；运行时代码未改：

- `python3 scripts/unity.py build-server`：退出 0，Linux Development 构建通过；Artifacts/20261107T154016Z-1f43eb-build-server。其 restore/validate.ok 与 restore/editor.log 确认 StandaloneOSX / Player / UNITY_SERVER disabled，恢复进程退出 0。Linux 产物 Builds/LinuxServer/20261107T154016Z-1f43eb/NatureCalls，未执行 Linux 二进制。
- 紧接 `python3 scripts/unity.py test-playmode`，无过滤：退出 0，17 Passed + 1 Relay Ignored；Artifacts/20261107T154138Z-b76c53-test-playmode。
- `python3 scripts/unity.py test-editmode`：退出 0，20/20；Artifacts/20261107T154247Z-2fed2e-test-editmode。
- `python3 scripts/unity.py build-macos`：退出 0，ARM64 Development 构建通过；Artifacts/20261107T154304Z-cdc4a3-build-macos；产物 Builds/macOS/20261107T154304Z-cdc4a3/NatureCalls.app。
- `python3 scripts/server.py Builds/macOS/20261107T154304Z-cdc4a3/NatureCalls.app`：退出 0，server/leader/follower 均 passed，各端适用断言通过；Artifacts/20261107T154338Z-3f5aa2-server。退出后的接任/返回/空房断言由 follower/server 核验，不要求已退出的 leader 报告这些字段为 true。
- `python3 scripts/multiplayer.py Builds/macOS/20261107T154304Z-cdc4a3/NatureCalls.app`：退出 0，host/client 全部断言通过；Artifacts/20261107T154413Z-faf4de-multiplayer。
- Python 单测 28/28（含构建失败仍恢复、恢复缺回执拒绝、残留锁保留、三平台目标与 Ignored/失败区分）：Artifacts/target-isolation-python-tests.log；仓库检查通过：Artifacts/target-isolation-check.log。脚本单测不冒充 Unity 实测。
- Unity 自动补写的 Android 空图标默认值差异已存入 Artifacts/target-isolation-generated-settings.patch 后还原，未纳入提交。未删 UnityLockfile，未结束非本次启动的进程。
- 完成前再次 fetch，origin/dev、origin/main 和远端任务分支未变化；Glossary 无新增公共差异。
- 未执行：Windows Unity/Player、Linux 二进制运行、跨设备/公网 UDP、Relay 云用例、人工手感。未 SSH、未重新部署，运行时代码未修改；未合并 PR。

Semantic-Diff: none。沿用 Automation、Build、Player、Server 现有名称；无领域概念增加、重命名或删除。更新 TESTING、DEVELOPMENT、DEVELOPMENT_STATUS 与本记录。

## 2026-11-11 一键构建与部署专用服务器

- 新增 `scripts/deploy_server.py`：检查已跟踪文件无未提交改动 → `unity.py build-server` → 上传并解压到 `/opt/naturecalls/releases/<提交>-<build-id>` → 原子切换 `current` → 重启当前运行的房间 → 逐个检查 active、端口监听与 `Server listening` 日志，失败则切回上一版本；保留新版本加最近 3 个旧版本。新增 `scripts/tests/test_deploy_server.py`，Python 单测 31/31。
- 首次真实部署：提交 `5206a07`，构建 `Builds/LinuxServer/20261111T023339Z-1b16ad/`（构建后恢复桌面 Player 通过），部署为 `5206a07-20261111T023339Z-1b16ad`，房间 8000 `ROOM_OK`；服务器 `current` 已指向新版本，旧版本 `5b03702` 保留可回滚；记录 `Artifacts/20261111T024951Z-deploy-server/`。
- 用户已从公网客户端连上 `49.233.132.244:8000`（2026-11-11 告知，新部署之前的版本）。
- `.gitignore` 忽略工程根目录的 `*.mov`、`*.mp4` 录屏。


## 2026-11-13 JST 如厕对准、擦拭、小便池、工具箱与清洁（日志 UTC 2026-11-12）

- 分支 `feat/toileting-accuracy`；开始 HEAD `9fea68bb9faba36c615e0981c94e995bd213084c`，公共 `origin/dev` 为 `3788310598f688206869a34b9c560a9bceb7b91c`，已 fetch、全文对照 Glossary 并通过 semantic sync 显式确认。实施依据为分支已有 `bf123cf` 与 `9fea68b`，实现期间未改 GLOSSARY。完整 Semantic Diff 为 changed：Added Mess、Facility/Urinal、Facility/Toolbox；Changed Player/Posture/Squatting、Player/Urgency、Player/Disgust、Facility/Toilet、Facility/PaperRoll、Infrastructure/Input；Removed none（路径统一以 NatureCalls/Gameplay 开头，Input 以 NatureCalls/Infrastructure 开头）。
- Player 契约提交 `d659ad9`；实现与测试提交 `87cbb95cc2d190d3794bf0370fe476b2eccfed09`。测试和构建使用包含该提交运行时代码的工作树；构建后只新增工具箱位于公共区域的测试断言及交付文档，没有改构建所用运行时代码。macOS / Darwin arm64，Unity 6000.3.26f1。
- 规则集中于 NeedsRules，Accuracy 做时间积分。PlayerInput 阶段为对位、保持、小便、清洁，暴露世界目标区/落点、计时、准度、未擦拭、手持物与消耗/清洁进度。Movement 不再吸附 Anchor；站起返回进入前位置。Item 共用容量和丢掷；PaperRoll 消耗、Mop 唯一实例；Mess 三类共用对象池。网络仍走玩家 FacilitySync → IsServer 校验 → 服务器 Facilities 列表，NeedsSync 同步需求和未擦拭。未新增未经批准的领域规则，无待人类决定的实现阻塞。
- Unity API 两次执行 toileting 构建工具：为三个已有小便池安装组件，生成/放置工具箱和 brush 拖把，重新保存 Player/Facilities Prefab 清理过时列表字段。原资产 GUID 保留。Unity 导入自动产生的无关 Android 空图标配置已恢复至任务前状态；未改第三方资源或包版本。
- 新用例覆盖：阈值边界与按时间计分、漂移幅度、卷纸次数与长按中断；真实 Playground 中三档准度、对位不吸附、站起返回、小便占用/减半/积水、擦拭与拾纸/开门/走出分支、工具箱容量与唯一性/公共位置、拖把丢掷与最近拾取、清洁中断/完成、积水范围与恶心值、Host 共享状态，以及拒绝提前、NaN、远处位置和重复结算。

命令均实际执行；每个 Unity 目录含 editor.log、process.log、summary.json，测试另含 results.xml；构建另有 build.ok，服务器目标恢复另有 restore/validate.ok。以下 Artifacts 均相对仓库根目录，不提交生成物。

| 命令（python3 scripts/unity.py 前缀省略） | 实际结果 | Artifacts 路径 |
| --- | --- | --- |
| detect | 通过，6000.3.26f1 | 终端输出；版本亦见各 summary.json |
| `toileting --timeout 3600` | 通过 | `Artifacts/20261112T194018Z-b4b049-toileting/` |
| `test-editmode --timeout 3600` | 通过；29 Passed / 0 Ignored | `Artifacts/20261112T194227Z-050925-test-editmode/` |
| `test-playmode --timeout 3600` | 失败；工具箱唯一性断言失败，出现残留拖把；修正 Item 场景归属后通过 | `Artifacts/20261112T194304Z-ff390f-test-playmode/` |
| `test-playmode --timeout 3600` | 失败；既有脚底贴地测试出现 8 cm 偏差，单独复现及后续完整回归通过，原因未确定 | `Artifacts/20261112T194513Z-654422-test-playmode/` |
| `test-playmode --test-filter StandingVisualTouchesTheFloor;HostValidatesUrinalResultBeforeApplyingIt（完整方法名见 summary） --timeout 3600` | 通过；2 Passed / 0 Ignored | `Artifacts/20261112T194709Z-3d98ca-test-playmode/` |
| `toileting --timeout 3600` | 通过 | `Artifacts/20261112T194756Z-df5ed6-toileting/` |
| `test-playmode --timeout 3600` | 通过；28 Passed / 1 Ignored | `Artifacts/20261112T194850Z-541eef-test-playmode/` |
| `test-editmode --timeout 3600` | 通过；30 Passed / 0 Ignored | `Artifacts/20261112T195017Z-799d0f-test-editmode/` |
| `build-macos --timeout 3600` | 通过 | `Artifacts/20261112T195125Z-b324d4-build-macos/` |
| `build-server --timeout 3600` | 通过；Linux x86_64 产物及 macOS Player 恢复均通过 | `Artifacts/20261112T195350Z-fae134-build-server/` |
| `test-playmode --timeout 3600` | 通过；28 Passed / 1 Ignored | `Artifacts/20261112T195551Z-184b41-test-playmode/` |

- `python3 scripts/multiplayer.py Builds/macOS/20261112T195125Z-b324d4/NatureCalls.app`：Host 与 Client 均通过，Artifacts/20261112T195234Z-986ceb-multiplayer/。两端各自生成积水、取出拖把、持续清洁、归还；20 个双方观察标记已逐一核对，还检查原有移动、门、卷纸与退出。此为 localhost UDP，无 Relay。
- `python3 scripts/check.py --require-generated`：通过，887 files / 440 asset GUIDs；Artifacts/toileting/repository-check.log。
- `python3 -m unittest discover -s scripts/tests -v`：31/31；Artifacts/toileting/python-tests.log。此结果不是 Unity 测试。
- `python3 scripts/semantic.py check --base origin/dev --head HEAD --body-file Artifacts/toileting/pr-body.md`：changed 格式检查通过；Artifacts/toileting/semantic-check.log。格式检查不替代人类语义审批或其他成员 Review。
- macOS 产物：Builds/macOS/20261112T195125Z-b324d4/NatureCalls.app。Linux 产物：Builds/LinuxServer/20261112T195350Z-fae134/NatureCalls。Linux 二进制未执行。
- 未执行/不在范围内：Windows Unity、真实跨设备/LAN、Relay、MPPM GUI、真人键鼠手感、正式 HUD/美术、Linux 二进制运行、公网连接、部署。Relay 用例明确 Ignored；未访问课程服务器，未合并 PR。
- 已知限制：Owner 上报最终准度，服务器校验设施/占用/位置/时长/数值，不重放轨迹，不能当作反作弊验证。脚底测试的一次不稳定失败保留上表，未声称已查明根因。

- `python3 scripts/server.py Builds/macOS/20261112T195125Z-b324d4/NatureCalls.app`：服务器/首位玩家/第二位玩家均通过，Artifacts/20261112T195714Z-080549-server/。实际检查无本地玩家/镜头、设施和拖把共享、两次 C 占用及释放、污物、领队接任、计时返回和空服等待；各角色只验证自身职责（报告中不适用的布尔项不表示失败）。使用 macOS 同代码 `-server` 路径，不是 Linux 产物执行。
