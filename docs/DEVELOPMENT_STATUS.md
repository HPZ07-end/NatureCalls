# 实际开发状态

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
