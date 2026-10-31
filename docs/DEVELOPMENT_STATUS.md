# 实际开发状态

更新日期：2026-11-01。治理 [PR #1](https://github.com/HPZ07-end/NatureCalls/pull/1) 已合并到 `dev`，提交 `ebdfa31879363864f9fa1b26ca0c2dab5084f447`。M1 从该基线建立 `feat/multiplayer-playground`；本次实现等待独立 PR Review，不自动合并。

| 类别 | 实际状态 |
| --- | --- |
| 已实现 | 原有配置/编译/测试/构建工具、语义治理；新增 URP Playground、Player/Toilet Prefab、第一人称移动、姿态与第三人称 Blend、Anchor 交互、Host/Join/Leave UI、NGO 位置/朝向/姿态同步、本地双 Player 验证工具 |
| 已编写但缺服务验收 | MPS Session + Relay Join Code、匿名认证及连接失败反馈；Cloud 尚未关联，不能声称真实 Relay 联机成功 |
| 正在推进 | M1 功能分支 Review、MPPM GUI/人工键鼠手感、Cloud 关联、Windows 与真实跨设备验收 |
| 已规划 | 依实际需求进行 Stage 2/3、正式 Gameplay 闭环、Map 配置、共享规则的 Host 权威、跨平台联调和发布 |
| 尚未确定 | 正式玩法数值/阈值、失败惩罚细节、事故规则、子树负责人；由人类讨论，不沿用 C3 自动补全 |
| 尚未实现 | Urgency/Tidiness 等玩法规则、正式设施状态与占用、冲水堵塞、排泄物生成、背包、完整大厅/匹配、Host 迁移、正式美术/音频/动画 |

M1 的 2–4 人、第一人称/如厕第三人称与官方网络技术栈来自本次明确授权，已以 `Semantic-Diff: changed` 扩展 [Glossary](../GLOSSARY.md)。姿态不是正式 Toileting 系统，没有新增未批准的失败条件。

实际 Unity 测试、构建及双进程结果以 [M1 验收记录](m1-2026-10-20.md) 为准。Windows Unity、真实 LAN/Relay、签名/公证尚未验收；CI 的三个 OS 仅覆盖 Python/仓库检查。之前的基础设施结果保留在 [首次验收](setup-2026-10-19.md) 与 [治理记录](governance-2026-10-19.md)。

当前仍为 Stage 1。为 Editor/测试引用增加一个总运行时程序集，没有拆分各模块程序集；既有 Core 仍为空，不代表 Stage 2 完成。后续路线与课程截止日 **2026-12-21** 见 [ROADMAP](ROADMAP.md)。游戏版本保持 `0.1.0-alpha.1`，不增加发布 Tag。

本任务未修改 GitHub 默认分支或保护规则；2026-10-20 读取到默认分支仍为 `dev1019`。旧治理记录中的权限限制不能视为已经解除，本次不绕过 Review 或更改长期分支。

2026-10-30 按用户反馈将 Squatting 镜头调整为正后方居中近景，距离 1.6、目标高度 0.8、俯角 50°、横向偏移 0；不变更角色或网络状态机制。源码、已有场景与生成默认值同时更新，最新回归记录见 [PLAYGROUND](PLAYGROUND.md)。原 10 月 20 日验收记录保留为历史快照。

2026-10-31 按用户指定设施与 WC Prefab 实际 Renderer Bounds 重新生成 Playground：房间最终调整为 8m×10m 封闭空间；Back Wall 中央的三个紧凑隔间均为同一个 `WCStall` Prefab 实例，每个实例组合 WC `SM_toilet_cubicle` 与居中的 `SM_toilet_bowl`。Right Wall 中央放置三个 WC `SM_urinal`，用两块短隔板分开；Left Wall 中央放置两个 WC `SM_sink` 及上方 `SM_mirror`；左后角架子放置三个 WC `SM_toilet_paper`，Front Wall 靠小便池侧三分之一位置放置第二个架子。导入的 Peasant Nolant Renderer 实测高 2.043m，四组外墙统一采用 3.00m 高的 `Wall2m`（0.75m 头顶余量并按 0.25m 向上取整）。30m×24m 旧布局和用户先前试摆的 WC 对象保留为停用的 `legacy` 备份，后续确认无需回退时再移除。当前设施仅为美术与碰撞模块，未实现门、冲水、占用或取用规则；Unity GUI 检查未产生新的异常，尚未在 GUI 开启期间并行运行 Unity 测试。

同日继续按实例 Renderer Bounds 将左侧洗手池和镜子、右侧小便池和隔板贴齐对应墙体内表面，统一保留 0.01m 视觉间隙；左后置物架贴合 Left/Back 两面墙并同步移动架上卷纸，前侧置物架转向 90° 后贴合 Front Wall。该调整仅修改 Map/Art 的空间摆放，不增加设施交互语义。

2026-11-01 将导入的 `Peasant Nolant Blue` 作为嵌套 Prefab 接入现有可复用 `Player` Prefab，旧方块身体保留为停用的 `legacy` 子对象；CharacterController 与站立高度按实际 Renderer 高度设为 2.05m。Playground 离线练习继续复用既有 Input System WASD、Movement、CameraController 与网络 Player Prefab 引用，并修复缺失的第三人称 Cinemachine rig；场景运行时只有 `Local camera` 的 AudioListener 启用。Unity GUI 验证记录显示本地玩家 `controlled=True`、输入启用、Nolant Renderer 生效；WASD 输入探针经真实 InputAction 链路使角色在 20 个编辑器帧内由 `(0.000,0.080,-1.000)` 移动至 `(0.000,0.080,0.292)`，位移 1.292m。该工作仅完成已批准 Player Standing/WASD 的实现与美术接入，`Semantic-Diff: none`；GUI 开启期间未并行运行 batchmode 测试。

同日按用户后续反馈将 Player 的嵌套美术替换为 `Peasant Nolant Brown`，并生成绑定 URP/Lit 的 Brown 兼容材质；Blue 版本保留在停用的 `legacy` 子树。第一人称隐藏改为主输出摄像机排除本地身体专用层，镜面摄像机仍渲染该层；两个 WC 镜面实例各自创建运行时 RenderTexture，避免共享纹理互相覆盖。`WCStall` 原先覆盖整个 cubicle 的 BoxCollider 已替换为左右墙和后墙三个碰撞体，前方入口保持开放。Unity GUI 探针确认两个镜面有效、Brown Renderer 位于镜面视锥（`mirrorVisible=True`），并通过真实 W InputAction 将角色从入口前方 `(−0.350,0.080,1.823)` 移动至隔间内部 `(−0.350,0.080,2.621)`。这是既有 Player Standing/WASD 与 Map/Art 的技术修复，`Semantic-Diff: none`。
