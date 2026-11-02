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

