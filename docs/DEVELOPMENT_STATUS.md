# 实际开发状态

2026-11-25：E/F 交互方案已实现并通过 42 项 PlayMode；冲水与堵塞、倒灌、洗手台、统一如厕过程与滑倒仍未实现，实施顺序见 [ROADMAP](ROADMAP.md)。课程服务器仍运行 #30 之前的版本，需重新部署才能与当前客户端（三款体型外观字段）联机。

2026-11-25 E/F 交互与通用状态：E 按准星目标选择设施，F 处理擦拭和拖把清洁；设施状态和移动效果均通过可注册接口复用。Unity 6000.3.26f1 EditMode 36/36，PlayMode 42/43（1 Relay 跳过），build-server 通过。

2026-11-24 Map2 井口及卷纸修复：A 井使用井口周边的站立范围，遮挡检查略过井口自身构件，提示与 E 键共享目标判定。应急卷纸移到出生点旁的柜台，拾取距离从角色躯干附近计算。售纸仍按一枚金币换一卷纸，并在手中有物品时显示限制。Unity 6000.3.26f1 PlayMode 42 项通过，1 项 Relay 跳过。从真实出生点步行拾纸并从 A 井侧边到达 −3.20 m 落脚台通过，离线购纸和本机房主进井通过。`Semantic-Diff: none`。

2026-11-24 DressingRoom 与 Map2 接入（工作基线 `fbf02bb`，已同步 `origin/dev` `036f15f`）：Player、换装镜和房间共享矮快、均衡、高伸三款油画角色。服装色只覆盖可换色衣料。独立 Generic 骨架接入步态、跳跃、如厕、爬行及伸手。旧版性别型 PlayerPrefs 只在新字段缺失时用于迁移，保留至下一次存档版本升级。

Map2 进入单人、联机和随机选图目录，复用设施、HUD、计时和回房流程。地图交互包括 A 井下滑与 B 井爬梯、短暂湿地惯性、金币购纸及 A 支路五步维修。短阶视觉保留，通行碰撞改为连续坡面。Unity 6000.3.26f1 EditMode 35 项通过，PlayMode 42 项通过、1 项 Relay 云测试跳过。单人用例实际走通地下高差，房主用例验证了购纸、竖井授予及维修状态。未执行双设备联机。Semantic Diff 为 `changed`，覆盖 `Content/Map/DressingRoom`、`Content/Map/MapSelection`、`Content/Map/Map2`、`Gameplay/Facility/Pipe`、`Gameplay/Facility/PaperVendingMachine`，用户批准来源记录在 Glossary。

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
| 已实现 | 配置/编译/测试/构建工具、语义治理；URP Playground（8m×10m 厕所，三个 `WCStall` 隔间、小便池、洗手池与镜子、卷纸架）；第一人称移动、角色脚底接地校准、Space 跳跃、C 姿态与第三人称 Blend、C 键如厕（隔间内先对位再蹲下保持准度，小便池前站立瞄准，其他位置原地如厕）、马桶一人占用（联机由房主判定）、坐着可用 E 关门上锁；隔间门 E 开/关/锁；卷纸 E 拾取、R 丢掷；NGO 位置/朝向/姿态同步、本地双 Player 验证工具；隔间门与卷纸的房主权威状态同步代码（Host PlayMode 和 Windows 本机双 Player 双向同步验证通过）；DressingRoom 作为启动场景：镜子 E 换装（真实改变外观并记住）、左侧门 E 打开虚化背景的联机大厅；大厅提供单人离线测试、创建房间（已关联 Cloud，使用 Relay Join Code）、加入房间；房间 2–4 人，准备/开始同一按键；开始后房主选地图（含随机地图），所有端加载后生成角色并同步外观；地图左上角「退出」返回准备房；全局对局计时（默认 3 分钟，到点后回到准备房，联机时留在原房间）；急迫值（1 分钟涨满、如厕中不涨、大便后清零、小便减半）与恶心值（隔间外如厕 +10 并留下黑色小球），任意玩家任一项到阈值全队失败；大便 5 秒、小便 3 秒；三档大便准度、积水/脏马桶/黑球、擦拭与卷纸消耗、工具箱唯一拖把、长按清洁和中断；Owner 本地准度、IsServer 结算、只读 UI 接口与调试显示 |
| 已编写但缺服务验收 | MPS Session + Relay Join Code、匿名认证及连接失败反馈；2026-11-03 已关联 Cloud 项目，尚未验证真实 Relay 联机成功 |
| 正在推进 | 大厅两端真人联机（准备→开始→选图→同步外观）的人工验收、隔间门与卷纸的真实跨设备及 Relay 显示验收、MPPM GUI/人工键鼠手感、Relay 联机验收、Windows 与真实跨设备验收 |
| 已规划 | 依实际需求进行 Stage 2/3、正式 Gameplay 闭环、Map 配置、共享规则的 Host 权威、跨平台联调和发布 |
| 尚未确定 | 正式玩法数值/阈值、失败惩罚细节、事故规则、子树负责人；由人类讨论，不沿用 C3 自动补全 |
| 尚未实现 | 胜利判定、Tidiness（干净值）、事故规则、冲水与堵塞（与水管机制有关，待定；用户倾向用 F 键）、马桶排队、排泄物生成、背包、公开房间列表/匹配、Host 迁移、正式美术/音频/动画 |

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
