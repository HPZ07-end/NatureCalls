# 实际开发状态

更新日期：2026-11-01。治理 [PR #1](https://github.com/HPZ07-end/NatureCalls/pull/1) 已合并到 `dev`，提交 `ebdfa31879363864f9fa1b26ca0c2dab5084f447`。M1 从该基线建立 `feat/multiplayer-playground`；本次实现等待独立 PR Review，不自动合并。

| 类别 | 实际状态 |
| --- | --- |
| 已实现 | 原有配置/编译/测试/构建工具、语义治理；新增 URP Playground、Player/Toilet Prefab、第一人称移动、姿态与第三人称 Blend、Anchor 交互、Host/Join/Leave UI、NGO 位置/朝向/姿态同步、本地双 Player 验证工具；独立 DressingRoom 灰盒、无网络的本地练习玩家/相机、镜子范围内 E 键换装面板入口、门前 E 键进入独立 LobbyUI 场景、固定的 Lobby 返回准备房入口、本地四人自动开始 Mock 与回归测试 |
| 已编写但缺服务验收 | MPS Session + Relay Join Code、匿名认证及连接失败反馈；Cloud 尚未关联，不能声称真实 Relay 联机成功 |
| 正在推进 | M1 功能分支 Review、MPPM GUI/人工键鼠手感、Cloud 关联、Windows 与真实跨设备验收 |
| 已规划 | 依实际需求进行 Stage 2/3、正式 Gameplay 闭环、Map 配置、共享规则的 Host 权威、跨平台联调和发布 |
| 尚未确定 | 正式玩法数值/阈值、失败惩罚细节、事故规则、子树负责人；由人类讨论，不沿用 C3 自动补全 |
| 尚未实现 | Urgency/Tidiness 等玩法规则、正式设施状态与占用、冲水堵塞、排泄物生成、背包、完整大厅/匹配、Host 迁移、正式美术/音频/动画 |

M1 的 2–4 人、第一人称/如厕第三人称与官方网络技术栈来自本次明确授权，已以 `Semantic-Diff: changed` 扩展 [Glossary](../GLOSSARY.md)。姿态不是正式 Toileting 系统，没有新增未批准的失败条件。

实际 Unity 测试、构建及双进程结果以 [M1 验收记录](m1-2026-10-20.md) 为准。Windows Unity、真实 LAN/Relay、签名/公证尚未验收；CI 的三个 OS 仅覆盖 Python/仓库检查。之前的基础设施结果保留在 [首次验收](setup-2026-10-19.md) 与 [治理记录](governance-2026-10-19.md)。

当前仍为 Stage 1。为 Editor/测试引用增加一个总运行时程序集，没有拆分各模块程序集；Core 目前只有换装选择的纯 C# 类型 `OutfitSelection`，不代表 Stage 2 完成。后续路线与课程截止日 **2026-12-21** 见 [ROADMAP](ROADMAP.md)。游戏版本保持 `0.1.0-alpha.1`，不增加发布 Tag。

本任务未修改 GitHub 默认分支或保护规则；2026-10-20 读取到默认分支仍为 `dev1019`。旧治理记录中的权限限制不能视为已经解除，本次不绕过 Review 或更改长期分支。

2026-10-30 按用户反馈将 Squatting 镜头调整为正后方居中近景，距离 1.6、目标高度 0.8、俯角 50°、横向偏移 0；不变更角色或网络状态机制。源码、已有场景与生成默认值同时更新，最新回归记录见 [PLAYGROUND](PLAYGROUND.md)。原 10 月 20 日验收记录保留为历史快照。

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
