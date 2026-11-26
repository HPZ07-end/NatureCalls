2026-11-26 PR #50 冲突处理：将 dev 0b0f6da 合入 integrate/all，唯一文本冲突位于 ROADMAP 的按需建房标题；保留已实现的 broker 说明和镇定值惩罚 Proposed Change，不改代码、场景或 Glossary。PR 后续追加了 V/V 小便、蹲姿小便与按粪便硬度决定排出时长等行为，现有 PR Semantic Diff 只声明 Lobby，需人类核对新增规则与语义审批；尚缺其他成员 Review。初始修复树（基于 8945969）仓库检查通过（1736 文件、874 GUID）、Python 45/45；随后完整保留远程 3377512 的 Map3 及验收记录，新增资产检查以本次 CI 为准。本轮未运行 Unity 或双客户端验收，不能以脚本检查代替。冲突处理本身 Semantic-Diff: none；PR 整体仍为 changed。

2026-11-26 水面大厅分页与点击水波（分支 `feat/water-lobby-pages`，基于 #44）：去掉「✓」意图切换。首页点「创建房间」立即建房并进入房间页（房间号与玩家列表）；点「加入房间」进入标题为「隔间大厅」的加入页，上方按房间号列出现存房间可直接点选，下方输入房间号回车或冲水加入，「返回」回首页、Esc 同样返回。每次点击水面从点击处荡开三圈水波（运行时生成的环形贴图，UI Image，不新增 shader 或资源文件），文字随之轻晃。水面所有文字挂 `UnderwaterText`（BaseMeshEffect）：字形随两组交错波弯曲、带水色与漂移光斑，下方叠四层错开的淡深色倒影模拟水下模糊，点击时波动短暂加强。各页（首页、隔间大厅、房间页、选图页）以水面中心为原点重新排布，全部落在水面开口（半径约 0.14 m，面板约 269 单位）以内，文字靠近边缘时淡出，水波不超出水面；`ToiletLobbyTextStaysInsideTheWater` 逐页检查。全量 PlayMode 101 通过 + 1 Relay Ignored（`Artifacts/20261126T064726Z-b47ff0-test-playmode`）；中途一次 `HostLoadsMap1AndPublishesCrawlingPosture` 因计时超限 0.1 失败，重跑通过。加入水下文字后 DressingRoomTests 再次 27/27（`Artifacts/20261126T062824Z-d7fe3b-test-playmode`）。此前 DressingRoomTests 27/27（`Artifacts/20261126T061915Z-4b4262-test-playmode`，含首页、隔间大厅、房间页与水波截图）；全量 EditMode 70/70（`Artifacts/20261126T062156Z-9be36d-test-editmode`）、PlayMode 100 通过 + 1 Relay Ignored（`Artifacts/20261126T062220Z-2eb137-test-playmode`）。水面大厅不再提供 Relay 建房（左隔间建局域网、右隔间建服务器房间），Relay 只能在右隔间输入 Join Code 加入。未执行：真人点击手感、Windows 客户端。

2026-11-26 远程隔间连上课程服务器并显示房间列表（分支 `fix/lobby-server-rooms`）：此前双隔间水面大厅的远程隔间「创建房间」只走 Relay 或本机直连，从不请求课程服务器，原「创建服务器房间」按钮在水面布局中没有位置。现在远程隔间冲水创建即向 broker 创建服务器房间；选「加入房间」时水面列出 broker 上现存房间（新接口 `GET /rooms`，每 3 秒刷新，服务器不可达时显示原因），点选或输入三位房间号加入，Relay Join Code 仍可输入。查询列表不创建 Session；PlayMode 测试注入假列表，不访问课程服务器。Python 42/42、仓库检查、EditMode 70/70（`Artifacts/20261126T053548Z-a40e22-test-editmode`）、PlayMode 98 通过 + 1 Relay Ignored（`Artifacts/20261126T053615Z-10e378-test-playmode`）；水面截图 `Artifacts/20261126T053351Z-b1059e-test-playmode/toilet-lobby-server-rooms.png`。未执行：真人从水面列表加入公网房间、Windows 客户端。房间列表只显示房间号，不显示人数。已部署到课程服务器：版本目录 `/opt/naturecalls/releases/c1e547c-20261126T053958Z-83d38f`，未回滚；`ROOM_OK 8000`，8000 与 broker active；公网 `POST /rooms` 建出房间 841 后，`GET /rooms` 立即列出该房间。记录 `Artifacts/20261126T061154Z-deploy-server/`。服务器运行的是尚未合入 dev 的 `fix/lobby-server-rooms`。

2026-11-26 Map3（后室）接入修复：`Map3Integration` 改为结构网格非凸 `MeshCollider`，增加按 Unity 世界轴计算的地板支撑和出生点射线校正，避免走廊实心盒与出生点掉落；天花板渲染器保留并通过存在性测试。Portal 偏移从 `Room_R0` 与 `Room_R2` 世界坐标计算为 `(-40, 0, -8.8)`，Portal 支持标记本地轴判定；卷纸（含 `_Floor`）42、卷纸架 42、工具箱 4、拖把 4 均按挂点幂等实例化并关联，隔间门镜像开角/半开/锁死状态、马桶与 Pipe、PlanarMirror 最近选择和 Light_Flicker 实时点光已接线。Map3 专项 PlayMode 7/7 通过；全量 EditMode 70/70、PlayMode 97 Passed + 1 Ignored、仓库检查和 batchmode 截图均已完成。`Semantic-Diff: none`。

2026-11-26：对抗模式任务 A 第二轮完成；旧 PlayMode 断言已迁移，随机事件效果、结算快照、8 秒结算停留和离线主流程测试已接入。EditMode 48/48、PlayMode 53/54（1 Relay Ignored、0 Failed）、Server 构建及恢复通过。
# 实际开发状态

2026-11-26 晚 试玩反馈修复与集成验收（PR #50，分支 `integrate/all`，合并 #44 #45 #48 #49 与 Map3 修复）：准星精确选择且隔间门整块可选；大厅水面视图鼠标指向冲水键按 E；如厕改为 V 瞄准→再按 V 开尿（必须尿完）、C 蹲下→再按 C 开拉（按粪便种类持续，越硬越慢，`StoolRules.ReleaseSeconds` 暂定 1.4/2.6/4.2 s），开始前 Esc 退出瞄准，蹲着可直接按 V 尿；鼠标始终控制视角，低头看到马桶与三种不同的下落；尿流按视线落点判定，落在马桶/小便池开口范围算尿进，未进则边尿边留湿滑尿迹；WASD 小范围挪动；憋便红色状态在排泄后解除；修正对抗模式尿完尿意被重设的问题；Map2 竖井可从井底对准梯子；打包后自动部署（dev/main）。验收（Mac，Unity 6000.3.26f1，提交 ce8fc50）：EditMode 70/70；PlayMode 112 项 110 通过、1 Relay 跳过、1 失败为既有偶发 `PuddleAddsDisgustOnlyNearby`（单独重跑通过）；build-server、`scripts/check.py`、`unittest` 44 项通过。未执行：真实窗口手感（批处理无法驱动按键）、双设备联机、Map2 井底爬梯的实机确认。数值均为暂定。

2026-11-26 Map2 清洁交互排查：确认运行时 Player prefab 将 `PlayerInput.CaptureMouse` 序列化为 false，现由离线及联网本地玩家生成时启用鼠标捕获。工具箱提示显示实际取出的工具或归还动作；玩家输入新增受距离和准星约束的 Toolbox 目标兜底，并有 PlayMode 用例覆盖。仓库检查通过；Unity PlayMode 启动在许可证初始化失败，未执行用例，场景交互与真人键鼠未验收。Semantic-Diff: none。

2026-11-26 Map2 厕所状态牌：三个隔间门上方设有内外双面红/绿状态灯，依据角色是否位于隔间判断颜色；无人为绿、有人为红。双面牌 Unity API 场景集成与 Validate 通过；人物进入判断尚未进行 Unity PlayMode 或联机人工验收。`Semantic-Diff: changed`，路径 `NatureCalls/Gameplay/Facility/Door`，用户于本会话批准。

2026-11-26 Map2 A 支路维修：检修口仅在玩家手持疏通器时允许 E 清堵；未持有时仍显示“需要疏通器”。沿用现有五步共享维修状态，没有新增维修规则。未运行 Unity 测试；需在 Map2 人工走通完整流程并复核工具提示。Semantic-Diff: none。

2026-11-26 Map2 双向竖井：A、B 均改为井盖开关、打开后走入下井，并支持从任一井上行；用户已手动确认两井均可上、下。地下柱体开口、梯子模型展示及其余干涉/穿模仍未完成。当前分支 Unity PlayMode 自动化因许可证初始化失败而未执行；本轮人工场景检查通过，未完成双客户端验证。`Semantic-Diff: changed`，路径 `NatureCalls/Content/Map/Map2`，用户于本会话批准。

2026-11-26 Map2 井盖：A 井初始关闭，在井外按 E 开关；开盖后主动走入井口自动下滑，B 井仍按 E 爬梯返回。开关状态由服务器共享，井口附近有人时阻止关盖。Unity 6000.3.26f1 单人 Map2 PlayMode 专项 1/1 通过，覆盖初始状态、开关、自动下滑和 B 井返回；真实双客户端及公网联机未验收。Semantic Diff：`changed`，涉及 `NatureCalls/Content/Map/Map2`，用户在本会话确认交互。

2026-11-26 课程服务器部署：`scripts/deploy_server.py` 将 dev `2bca57f`（PR #41，完整对抗版）部署到 `ubuntu@49.233.132.244`，版本目录 `/opt/naturecalls/releases/2bca57f-20261126T012947Z-8d028c`（`current` 已指向它）。Linux 服务器构建与构建后恢复 macOS Player 通过；脚本检查 `ROOM_OK 8000`，未触发回滚。之后 SSH 只读复核：`naturecalls@8000` 与 `naturecalls-broker` active，8000/UDP 监听，broker `/health` 返回 ok（rooms 0、max 12）。公网 broker `/health` 正常。记录 `Artifacts/20261126T013246Z-deploy-server/`，详见 [开发记录](records/status-log.md#2026-11-26-课程服务器部署-2bca57f)。未执行：本版本的公网 `room_e2e.py` 与跨公网双设备真人对局。旧版本 `9dd8172` 的客户端需更新后才能联机。

2026-11-26 如厕视觉交互（第二阶段，同一分支，已合入 `feat/versus-body` 20ac426）：`Input/ToiletingView` 实现限时低头（约 3 秒额度、落点显示、镜头低头、漂移 ×0.6，与 PlayerNeeds 的低头舒爽度对接）、大便下落与水花和溅屁股画面、小便正对小便池的抛物线尿流与按入射角反弹、溅到自己扣镇定值、舒爽金色闪光；粪便种类抽取改读 PlayerNeeds 的便意、舒爽度、极限忍耐与失控。修正 PlayerNeeds 低头判断的符号（原 `Pitch < -45` 是抬头）。新 PlayMode `ToiletingViewTests` 6/6。未做：低头舒爽度在联机时只在本机有效（服务器看不到客户端的 Pitch）；尿流与下落只在本机显示；没有音效；HUD 在保持阶段仍画目标圈（盲拉需由 HUD 改成只在低头时显示落点）。

2026-11-26 污物与粪便种类第一阶段（分支 `claude/versus-mess-0b373a`，基于 `feat/versus-base`，尚未合入）：实现 GLOSSARY `Mess/StoolKind`。Core `StoolRules`（概率、堵塞量、气味、脏度权重，均为暂定）；`StoolOutput` 在大便结算时抽取种类；软便落地为 `Dropping`，拉稀为新的 `MessKind.LooseStool`（湿滑，惯性同 WetZone），硬便为 `HardStool` 物品（可踢、拾取扣镇定值、可丢、进马桶加堵塞，联机沿用 SharedItems，踢飞新增 `FacilitySync.KickItemRpc`）；管路已堵时拉稀进马桶触发倒灌。Blender 后台生成 8 个正式模型和 3 张精灵图（`scripts/blender/mess_models.py` → `Assets/Art/Mess/`，`scripts/unity.py mess-art` 生成 URP 材质与 `MessArt` 目录），`MessLook` 替换全部污物外观（软便堆、拉稀、失禁、呕吐、尿迹、倒灌、马桶抹痕）并按气味权重放出臭气精灵。EditMode 61/61、PlayMode 58/59（1 个既有 Relay 跳过）、build-server 与 `scripts/check.py` 通过。未做：舒爽度对抽取的影响（等 PlayerNeeds 发布 Comfort）、如厕视觉交互（低头、看到落下、小便抛物线，第二阶段）、马桶抹痕在真实马桶里的位置只按模型尺寸估算、联机踢飞只做了 SharedItems 计数验证没有双端实测。`Semantic-Diff: none`（实现已批准的 StoolKind；`LooseStool` 是拉稀的实现名）。

2026-11-26 对抗模式 C（捣蛋者与维修工具）施工中：新增 Breakable、Wrench、Plunger、塞纸/敲门/洒水入口，以及马桶/洗手台损坏状态和 DirtField 权重；尚未运行 Unity 导入、场景接线或 EditMode/PlayMode/build-server 验证。

2026-11-25 服务器房间号与局域网自动发现（分支 `feat/server-room-codes`，尚未合入 dev）：broker（TCP 8100）分配随机三位房间号，按需启动 UDP 8001–8099 动态房间，空置 300 秒后房间自行退出；8000 常驻房间保留。局域网直连房间经 UDP 47777 广播，大厅自动列出，IP:端口直连与 Relay 保留。EditMode 41/41、PlayMode 42/43（1 个既有 Relay Ignored）、macOS 与 Linux Server 构建通过；本机 broker 生命周期、两客户端房间号加入、局域网列表点击加入通过。已部署到课程服务器：版本目录 `/opt/naturecalls/releases/9dd8172-20261125T165824Z-f3ed9a`，8000 与 broker active；从公网用两个 macOS 客户端经 UI 创建并以房间号 328 加入同一房间通过，离开后房间 300 秒自行退出（退出码 0）、房间号查询返回 404。记录 `Artifacts/20261125T172149Z-deploy-server/summary.json`、`Artifacts/20261125T172222Z-2730b3-room-e2e/`。未执行：真人跨公网联机、Windows 客户端、两台真实设备的局域网发现。
2026-11-26 对抗身体分支：PlayerNeeds/NeedsRules 已加入 Bladder、Bowel、Composure、Endurance、Vomiting、Comfort 与移动限制接口；NeedsSync 已同步新状态。主流程联机/失控冲向马桶和完整视野射线扣除仍待合并验收。

2026-11-25 服务器房间号与局域网自动发现（分支 `feat/server-room-codes`，尚未合入 dev）：broker（TCP 8100）分配随机三位房间号，按需启动 UDP 8001–8099 动态房间，空置 300 秒后房间自行退出；8000 常驻房间保留。局域网直连房间经 UDP 47777 广播，大厅自动列出，IP:端口直连与 Relay 保留。EditMode 41/41、PlayMode 42/43（1 个既有 Relay Ignored）、macOS 与 Linux Server 构建通过；本机 broker 生命周期、两客户端房间号加入、局域网列表点击加入通过。已部署到课程服务器：版本目录 `/opt/naturecalls/releases/9dd8172-20261125T165824Z-f3ed9a`，8000 与 broker active；从公网用两个 macOS 客户端经 UI 创建并以房间号 328 加入同一房间通过，离开后房间 300 秒自行退出（退出码 0）、房间号查询返回 404。记录 `Artifacts/20261125T172149Z-deploy-server/summary.json`、`Artifacts/20261125T172222Z-2730b3-room-e2e/`。未执行：真人跨公网联机、Windows 客户端、两台真实设备的局域网发现。
2026-11-25：PR #35「服务器房间号和局域网自动发现」已在 `origin/dev` 合入。broker 负责随机三位房间号、动态房间生命周期；大厅保留 Relay、IP:端口，并增加服务器房间号输入和局域网发现列表。此次合并接入当前双隔间水面大厅，尚未在本分支完成本轮回归。

2026-11-26：PR #37 `feat/art-model-import` 已整合到 `integrate/art-model-import`，基线为 `origin/docs/floor1-approval`（`0f88a44`）。美术资源、换装间和双隔间大厅按已批准定义接入；代码结构以 dev 为准，Map2 已用队友场景作底并经 Unity API 重跑。EditMode 43/43、PlayMode 59 Passed + 1 Relay Ignored、build-server 及恢复 Player 通过；未执行真实双端 LAN/Relay。

2026-11-25：E/F 交互方案已实现并通过 42 项 PlayMode；冲水与堵塞、倒灌、洗手台、统一如厕过程与滑倒仍未实现，实施顺序见 [ROADMAP](ROADMAP.md)。课程服务器已于同日更新到 dev `a58cf77`（#32），见下方课程服务器记录。

2026-11-25 课程服务器部署：`scripts/deploy_server.py` 将 dev `a58cf77`（PR #32）部署到 `ubuntu@49.233.132.244`，版本目录 `/opt/naturecalls/releases/a58cf77-20261125T092630Z-ff2725`（`current` 已指向它），房间 8000。Linux 服务器构建与构建后恢复 macOS Player 均通过；脚本检查 `ROOM_OK 8000`，未触发回滚。之后 SSH 只读复核：`naturecalls@8000` active，NRestarts=0，端口 8000/UDP 处于监听，日志含 `Server listening on UDP 8000.`。记录 `Artifacts/20261125T095101Z-deploy-server/summary.json`。尚未做跨公网的真人联机验收。
2026-11-25 团队分工：用户负责一层（Map2）装置与如厕体验，GLOSSARY 记录了角色参数（CharacterProfile）、小便镜头与反弹、卷纸纸量耐久、全部用水装置倒灌、Map3 后室，以及急迫值与一局推进方式的重做方向；均未实现，对象拆分见 [ROADMAP](ROADMAP.md)。

2026-11-25：E/F 交互方案已实现并通过 42 项 PlayMode；冲水与堵塞、倒灌、洗手台、统一如厕过程与滑倒仍未实现，实施顺序见 [ROADMAP](ROADMAP.md)。课程服务器仍运行 #30 之前的版本，需重新部署才能与当前客户端（三款体型外观字段）联机。

2026-11-25 E/F 交互与通用状态：E 按准星目标选择设施，F 处理擦拭和拖把清洁；设施状态和移动效果均通过可注册接口复用。Unity 6000.3.26f1 EditMode 36/36，PlayMode 42/43（1 Relay 跳过），build-server 通过。同日整理：`Map2Interactions` 拆为 Facility `PaperVendingMachine` 与 Map `WetZone`、`Map2Repair`；门、马桶、小便池改用通用 `SharedFacilityState`，Map2 场景与 Facilities prefab 经 Unity API 重建；整理后 EditMode 36/36，PlayMode 42/43（1 Relay 跳过），build-server 通过。

2026-11-24 Map2 井口及卷纸修复：A 井使用井口周边的站立范围，遮挡检查略过井口自身构件，提示与 E 键共享目标判定。应急卷纸移到出生点旁的柜台，拾取距离从角色躯干附近计算。售纸仍按一枚金币换一卷纸，并在手中有物品时显示限制。Unity 6000.3.26f1 PlayMode 42 项通过，1 项 Relay 跳过。从真实出生点步行拾纸并从 A 井侧边到达 −3.20 m 落脚台通过，离线购纸和本机房主进井通过。`Semantic-Diff: none`。

2026-11-24 DressingRoom 与 Map2 接入（工作基线 `fbf02bb`，已同步 `origin/dev` `036f15f`）：Player、换装镜和房间共享矮快、均衡、高伸三款油画角色。服装色只覆盖可换色衣料。独立 Generic 骨架接入步态、跳跃、如厕、爬行及伸手。旧版性别型 PlayerPrefs 只在新字段缺失时用于迁移，保留至下一次存档版本升级。

Map2 进入单人、联机和随机选图目录，复用设施、HUD、计时和回房流程。地图交互包括 A 井下滑与 B 井爬梯、短暂湿地惯性、金币购纸及 A 支路五步维修。短阶视觉保留，通行碰撞改为连续坡面。Unity 6000.3.26f1 EditMode 31 项通过，PlayMode 41 项通过、1 项 Relay 云测试跳过。单人用例实际走通地下高差，房主用例验证了购纸、竖井授予及维修状态。未执行双设备联机。Semantic Diff 为 `changed`，覆盖 `Content/Map/DressingRoom`、`Content/Map/MapSelection`、`Content/Map/Map2`、`Gameplay/Facility/Pipe`、`Gameplay/Facility/PaperVendingMachine`，用户批准来源记录在 Glossary。

2026-11-24 油画风角色资产：矮快、均衡、高伸三款角色各用独立 15 骨 Generic 绑定、专属油画贴图、Animator 与 Prefab。每款含站立、走路、跑步、伸手四段原地动画，各自的走跑周期已调整。Unity 6000.3.26f1 标准 `validate` 通过，逐一采样 12 段动画并渲染检查贴图和朝向。尚未替换 Player 或接入换装、角色属性与物品交互。`Semantic-Diff: none`。

2026-11-23 Map2 场景：新增 `Assets/Scenes/Map2.unity`。地上 15.4 × 11.6 m，布置错位隔间、双洗手台、湿滑近路外观、破损绕行和补给角。A 竖井对接 −3.20 m 落脚台，地下有 −3.80 m 检修槽、−2.70 m 回流栈道与 B 短梯井；A 支路布置上游观察窗、阀门、检修口和回流观察窗。场景使用 Blender 样板笔触贴图与独立 URP 油画材质。Unity 6000.3.26f1 保存并重开后核对井口净空、落点、楼层标高和坡面，标准 `validate` 通过，地上与地下预览已检查。尚未加入大厅选图、角色通行控制、湿滑判定、售纸和维修联动。`Semantic-Diff: none`。

2026-11-23 Map2 美术资产：新增并导入 14 个模型，包括井盖与竖井、地下高差通行模块、检修管件和地面障碍及补给道具。Blender 4.5 逐个重开验证，确认井盖和检修盖打开后中心通道无网格遮挡。Unity 6000.3.26f1 核对模型层级、铰链、米制定位点、共享材质及观察窗透明材质，标准 `validate` 通过。README 清单共 73 个基础模型。尚未放入 Map2 场景或接入交互。`Semantic-Diff: none`。

2026-11-23 Map1 布局与美术细化：地上占地由 26 × 20 m 收到 23.5 × 16.85 m。三间隔间后端接外墙，门扇、门框、门碰撞与前板统一正面基准。B1 门前净空与风道槽口分开；隔间归属同时核对楼层高度。新制 6 件 Blender 模块已导入 Unity 并映射共享材料，README 清单为 59 件。Unity 6000.3.26f1 保存并重开核对，Map1 与原模块厕所 PlayMode 7/7。未进行四人实机走查。`Semantic-Diff: none`。

2026-11-23 Map1 大厅与风道：MapCatalog 和构建场景列表加入 Map1。A、B 风道都配置了双向爬行，使用独立姿态、0.85 m 胶囊与低位第一人称视点，通行后按净空恢复站立。Unity 6000.3.26f1 标准验证通过，Map1 专项 PlayMode 5/5，覆盖大厅离线选图、单进程 LAN Host 加载与姿态发布、两条风道双向通行及现有交互。爬行外观为低姿占位表现，未进行跨客户端或 Relay 验收。`Semantic-Diff: changed`（新增 Crawling，更新 MapSelection，批准见 Glossary）。

2026-11-23 Map1 交互接入：复用共享 Player、镜头与 HUD，接入三扇隔间门、三个马桶、两个小便池、三卷纸和一个工具箱内的拖把。编辑器直接播放 Map1 时启动现有离线对局，使如厕、擦拭和污物正常结算。Map1 专项 PlayMode 2/2，覆盖站立通行、门锁、马桶和小便池的占用释放、卷纸消耗和丢掷、拖把取还和清洁中断。原模块厕所 PlayMode 回归 2/2。维修操作尚未接入。`Semantic-Diff: none`。

2026-11-23 Foundation 场景：新增 `Assets/Scenes/Map1.unity`，包含地上厕所和供给区、地下维修区、A/B 风道与四个出生点标记。Unity 6000.3.26f1 保存并重开场景，核对风道拼接点、净空和门槛，抽样验证地下环路和出生点地面，标准 `validate` 通过。`Semantic-Diff: none`。

2026-11-23 Foundation 地图美术：交付 5 个 Blender 源模型，包括可通行风口框、斜风道、会车段、地下出口收口和维修设备外壳。Blender 4.5 重开全部文件并核对风道净尺寸，按图纸坐标验证 A、B 两条路线的接缝和标高。5 个 FBX 已导入 `Assets/Art/Models/Systems/`，README 清单共 53 件。Unity 6000.3.26f1 验证网格分件、米制定位点、共享 URP 材质和导入 GUID，标准 `validate` 通过。尚未接入场景或测试角色通过性。`Semantic-Diff: none`。

2026-11-23 Demo 复用梳理：新增 [实现复用指南](REUSE_GUIDE.md)，记录角色控制、镜头、外观、设施交互、联机请求、地图与 UI 的现有接口和依赖，区分 HEAD 与工作区未提交角色扩展。用户提出后续重做地图、玩法与 UI；本次只整理技术文档，尚未归档/移动资产、创建新地图或实施新玩法与 UI。未运行 Unity 测试、构建及联机验收。`Semantic-Diff: none`（仅指本次文档新增，不包含工作区既有 Glossary 修改）。

2026-11-20 美术资产导入：48 个 Blender 模型已导出 FBX，按 10 类存放在 `Assets/Art/Models/`，并映射到 `Assets/Art/Materials/` 下的 55 个共享 URP 材质。Windows Unity 6000.3.26f1 逐件核对了网格、尺寸、定位点和材质引用，确认地板为 1 m × 1 m、两款角色保持 1.80 m / 1.74 m 并朝 Unity 的 +Z、隔间门原点在铰链侧、镜面槽独立。标准 Unity `validate` 通过。

2026-11-20 动画资产：两款角色使用 21 根骨骼和一套六个原地 Humanoid 动作，隔间门和准备房门支持双向转动预览，隔间门闩和水闸手轮有独立动画通道。Unity 6000.3.26f1 已验证 Animator 引用并采样动作。预览场景未加入构建，门和水闸动画仍只用于预览。

2026-11-20 玩家外观接入：换装准备房和两张地图共用同一 Player，换装可分别预览男性／女性模型及原有四色上衣，确认后一起保存和同步。玩家根节点、碰撞配置和玩法判定沿用现有实现，Humanoid Animator 读取移动、离地和姿态。原蹲起片段保持不变。当前 Playground 的俯视镜头下男性头部会遮住现有马桶开口中心，按用户要求留到新地图验收。

2026-11-23 玩家验收：Windows Unity EditMode 31/31；PlayMode 33 Passed、1 Failed、1 Relay Ignored。换装预览、取消和确认、两张地图继承外观、女性角色走路、跳跃、落地待机及蹲起、模块地图的门和最近卷纸交互均通过。唯一失败为旧 Playground 俯视时男性头部遮挡马桶开口。仓库检查通过（1278 文件、648 个资产 GUID）。

同日 Windows x86_64 Development 构建通过。模块地图本机专用服务器与两个客户端使用同一构建，两次均在两个客户端上确认自己和对方的性别、服装编号和启用的角色模型（`appearance: true`）。完整三进程设施回归两次均在对方观察马桶占用时超时，不能视为整体通过。证据见 `Artifacts/20261122T162651Z-1172f8-test-playmode/`、`Artifacts/20261122T163029Z-8496cf-build-windows/`、`Artifacts/20261122T163214Z-535961-server/` 和 `Artifacts/20261122T163407Z-eee481-server/`（目录名使用 UTC）。

2026-11-16 地图大厅接入（分支 `feat/map-lobby-integration`，尚未合入 dev）：MapLayoutPrototype 已通过 Unity API 接入已有 Playground、角色、镜头、四出生点、HUD、大厅地图目录及门/马桶/卷纸/唯一拖把组件。Unity 编译/资源验证通过；首次相机总数断言失败后按用户要求暂停，随后获授权修正为排除镜面 RenderTexture 相机，并检查屏幕相机身份及本地跟随目标。完整 PlayMode 33 项中 32 Passed、1 Relay Ignored，EditMode 30/30 通过。Windows x86_64 Development 构建和新地图专用服务器＋双客户端本机 UDP 验收通过，包括大厅准备/选图、本地屏幕相机、共享设施、领队接任、结束返回和空房保留。仓库检查通过（1003 files / 502 asset GUIDs），Python 31/31；人工手感、跨设备和公网部署未执行。详情见 [本次记录](records/map-lobby-integration-2026-11-16.md)。

更新日期：2026-11-13（JST，测试日志使用 UTC）。本文件只描述**当前**状态；逐次变更说明与验收细节见 [开发记录](records/status-log.md) 和 [M1 验收记录](records/m1-2026-10-20.md)。

| 类别 | 实际状态 |
| --- | --- |
| 已实现 | 配置/编译/测试/构建工具、语义治理；URP Playground（8m×10m 厕所，三个 `WCStall` 隔间、小便池、洗手池与镜子、卷纸架）；第一人称移动、角色脚底接地校准、Space 跳跃、C 姿态与第三人称 Blend、C 键如厕（隔间内先对位再蹲下保持准度，小便池前站立瞄准，其他位置原地如厕）、马桶一人占用（联机由房主判定）、坐着可用 E 关门上锁；隔间门 E 开/关/锁；卷纸 E 拾取、R 丢掷；NGO 位置/朝向/姿态同步、本地双 Player 验证工具；隔间门与卷纸的房主权威状态同步代码（Host PlayMode 和 Windows 本机双 Player 双向同步验证通过）；DressingRoom 作为启动场景：镜子 E 换装（真实改变外观并记住）、左侧门 E 打开虚化背景的联机大厅；大厅提供单人离线测试、创建房间（已关联 Cloud，使用 Relay Join Code）、加入房间；房间 2–4 人，准备/开始同一按键；开始后房主选地图（含随机地图），所有端加载后生成角色并同步外观；地图左上角「退出」返回准备房；全局对局计时（默认 3 分钟，到点后回到准备房，联机时留在原房间）；急迫值（1 分钟涨满、如厕中不涨、大便后清零、小便减半）与恶心值（隔间外如厕 +10 并留下黑色小球），任意玩家任一项到阈值全队失败；大便 5 秒、小便 3 秒；三档大便准度、积水/脏马桶/黑球、F 擦拭（卷纸或用手）与卷纸消耗、工具箱唯一拖把、持拖把长按 F 清洁和中断；E 按准星选择目标；Owner 本地准度、IsServer 结算、只读 UI 接口与调试显示 |
| 已编写但缺服务验收 | MPS Session + Relay Join Code、匿名认证及连接失败反馈；2026-11-03 已关联 Cloud 项目，尚未验证真实 Relay 联机成功 |
| 正在推进 | 大厅两端真人联机（准备→开始→选图→同步外观）的人工验收、隔间门与卷纸的真实跨设备及 Relay 显示验收、MPPM GUI/人工键鼠手感、Relay 联机验收、Windows 与真实跨设备验收 |
| 已规划 | 依实际需求进行 Stage 2/3、正式 Gameplay 闭环、Map 配置、共享规则的 Host 权威、跨平台联调和发布 |
| 尚未确定 | 正式玩法数值/阈值、失败惩罚细节、事故规则、子树负责人；由人类讨论，不沿用 C3 自动补全 |
| 尚未实现 | 胜利判定、Tidiness（干净值）、事故规则、冲水与堵塞（与水管机制有关，待定；用户倾向用 F 键）、马桶排队、排泄物生成、背包、匹配、Host 迁移、正式美术/音频/动画 |

当前仍为 Stage 1。为 Editor/测试引用增加一个总运行时程序集，没有拆分各模块程序集；Core 目前包含换装选择、房间开始条件、地图选择、对局时间、需求及准度等纯 C# 规则，不代表 Stage 2 完成。后续路线与课程截止日 **2026-12-21** 见 [ROADMAP](ROADMAP.md)。游戏版本保持 `0.1.0-alpha.1`，不增加发布 Tag。

分支与集成：M1 已合入 `dev`；`main` 仍只有 Bootstrap 提交。GitHub 默认分支为 `main`（2026-11-02 读取）。`dev`/`main` 均未启用服务端分支保护，Review 要求目前只由 [CONTRIBUTING](CONTRIBUTING.md) 约定。

已知问题：
- 联机时非房主按 R 丢卷纸掉帧（用户 2026-11-04 试玩发现）。2026-11-05 改为丢掷者本机模拟、其他端平滑跟随，并去掉了对运动学刚体设置速度的调用；本机双进程探针两端落点一致，真实两台机器尚未复测。以下为当时记录：房主端日志有「给运动学刚体设置速度」警告（`PaperRoll.cs` 55–56 行）；可能原因：房主每 0.1 秒广播一次飞行中卷纸的位置，非房主每收到一次就瞬移卷纸并重新应用全部设施状态。还缺非房主一端的日志确认。用户认为房主权威方案不一定可行，暂不处理。
- 玩家测试发现的跳跃越界问题（2026-11-03 记录，未修复）：跳上小便池可以跳出房间；跳上卷纸架可以跳进隔间或跳出房间；跳上马桶可以跳出隔间并卡在隔间顶部的杆子上。原因是墙体、隔板高度与 Space 跳跃高度不匹配，且房间没有顶部阻挡。
Windows Unity、真实 LAN/Relay、签名/公证尚未验收；CI 的三个 OS 仅覆盖 Python/仓库检查。

专用服务器改造第一步：地图 `Facilities` 由服务器生成并拥有，玩家 `FacilitySync` 仅负责请求校验；Lobby 使用已批准的领队规则与进入顺序接任，纯服务器不计入成员。已通过首次导入/编译和 EditMode 13/13；此前默认 PlayMode 17 项通过、1 项 Relay Ignored，被旧运行器判为失败；明确本地用例名单运行通过 17/17（均在 Linux 构建前，不能作为构建后回归证据）。macOS Development 构建及现有双进程探针均通过。纯服务器 `-server -port`、IP:端口加入、30 FPS/无画面跳过与 Linux 构建命令已实现。新三进程本机探针通过（Artifacts/20261107T144354Z-3002f5-server）；地址新增测试后 EditMode 20/20。此前原双进程探针及 Linux x86_64 Dedicated Server 构建已通过，但随后未过滤 PlayMode 出现 17 项 SetUp 失败（Artifacts/20261107T150526Z-1f348b-test-playmode）：Editor 持久停留在 Server 目标。课程服务器已部署 1 个房间：`naturecalls@8000` active/enabled，PID 1844231，`0.0.0.0:8000/UDP`，无失败重启，空房内存约 117 MiB（本次观察值）。二进制对应 5b03702，目录 `/opt/naturecalls/releases/5b03702`；应用状态隔离到 `/var/lib/naturecalls-8000`，ProtectHome 已启用。完整日志见 Artifacts/deploy/verify-isolated.log。公网 UDP 客户端连入、Windows/Relay 尚未验收；当次部署没有运行跨公网游戏连接；本次回归不登录或部署课程服务器。


2026-11-08 JST PR #19 回归修复：服务器构建结束后在同一锁内恢复并验证本机桌面 Player，所有非服务器动作显式指定 Player；Ignored 单列而非失败。macOS ARM64 / Unity 6000.3.26f1 已按顺序真实通过 Linux build-server（含恢复验证）→ 无过滤 PlayMode 17 Passed + 1 Relay Ignored → EditMode 20/20 → build-macos → server.py 三进程 → multiplayer.py 双进程；详细 Artifacts 与命令见 [开发记录](records/status-log.md#2026-11-08-jst-pr-19-构建目标隔离回归日志-utc-2026-11-07)。Python 28/28，仓库检查通过。仅修改 Editor/Python 与文档，没有改运行时代码或重新部署；Windows Unity、Linux 二进制运行、Relay/公网/真人验收仍未执行。


2026-11-13 JST 如厕对准与清洁：已在 `feat/toileting-accuracy` 实现，运行时代码提交 `87cbb95`（Player 接口 `d659ad9`），尚待 PR Review/合入 dev。Mac ARM64 / Unity 6000.3.26f1：EditMode 30/30；无过滤 PlayMode 28 Passed + 1 Relay Ignored；build-macos 与扩展双 Player 探针通过；build-server（Linux x86_64）和桌面 Player 恢复通过，之后无过滤 PlayMode 再次 28 Passed + 1 Ignored；服务器+两客户端探针通过。仓库检查通过、Python 31/31、Semantic Diff changed 格式检查通过。完整命令、Artifacts、两次早期失败及后续回归见 [开发记录](records/status-log.md#2026-11-13-jst-如厕对准擦拭小便池工具箱与清洁日志-utc-2026-11-12)。一次既有脚底贴地测试的 8 cm 偏差未稳定复现，保留为测试不稳定记录。无未决定的规则阻塞；Windows、Linux 二进制执行、真实 LAN/Relay、真人手感与正式 HUD 仍未验收。本次不访问或部署课程服务器，不合并 PR。

- 2026-11-24：E/F controls, facility interaction registry, movement effect rules, and generic shared facility state implemented; EditMode 31/31.

当前：一层装置第一批已接入 Core 规则骨架、Pipe/Sink/Toilet 冲水入口与纸量耐久；堵塞阈值、倒灌概率及贡献值均为“暂定，待用户确认”。
