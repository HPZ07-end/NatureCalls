# 实际开发状态

更新日期：2026-11-02。本文件只描述**当前**状态；逐次变更说明与验收细节见 [开发记录](records/status-log.md) 和 [M1 验收记录](records/m1-2026-10-20.md)。

| 类别 | 实际状态 |
| --- | --- |
| 已实现 | 配置/编译/测试/构建工具、语义治理；URP Playground（8m×10m 厕所，三个 `WCStall` 隔间、小便池、洗手池与镜子、卷纸架）；第一人称移动、Space 跳跃、C 姿态与第三人称 Blend、马桶 Anchor 对齐；隔间门 E 开/关/锁；卷纸 E 拾取、R 丢掷；Host/Join/Leave UI、NGO 位置/朝向/姿态同步、本地双 Player 验证工具；DressingRoom 灰盒（镜子 E 换装面板入口、门前 E 进入独立 LobbyUI 场景、Lobby 返回准备房）与本地四人自动开始 Mock；ChangingRoom 换装台原型（本地改变角色颜色） |
| 已编写但缺服务验收 | MPS Session + Relay Join Code、匿名认证及连接失败反馈；Cloud 尚未关联，不能声称真实 Relay 联机成功 |
| 正在推进 | MPPM GUI/人工键鼠手感、Cloud 关联、Windows 与真实跨设备验收 |
| 已规划 | 依实际需求进行 Stage 2/3、正式 Gameplay 闭环、Map 配置、共享规则的 Host 权威、跨平台联调和发布 |
| 尚未确定 | 正式玩法数值/阈值、失败惩罚细节、事故规则、子树负责人；由人类讨论，不沿用 C3 自动补全 |
| 尚未实现 | Urgency/Tidiness 等玩法规则、正式设施状态与占用、冲水堵塞、排泄物生成、背包、门和卷纸的网络同步、真实联机大厅/匹配、Host 迁移、正式美术/音频/动画 |

当前仍为 Stage 1。为 Editor/测试引用增加一个总运行时程序集，没有拆分各模块程序集；Core 目前只有换装选择的纯 C# 类型 `OutfitSelection`，不代表 Stage 2 完成。后续路线与课程截止日 **2026-12-21** 见 [ROADMAP](ROADMAP.md)。游戏版本保持 `0.1.0-alpha.1`，不增加发布 Tag。

分支与集成：M1 已合入 `dev`；`main` 仍只有 Bootstrap 提交。GitHub 默认分支为 `main`（2026-11-02 读取）。`dev`/`main` 均未启用服务端分支保护，Review 要求目前只由 [CONTRIBUTING](CONTRIBUTING.md) 约定。

已知并存与待决：DressingRoom 与 ChangingRoom 两套换装原型并存，取舍见 [ARCHITECTURE](ARCHITECTURE.md)。Windows Unity、真实 LAN/Relay、签名/公证尚未验收；CI 的三个 OS 仅覆盖 Python/仓库检查。
