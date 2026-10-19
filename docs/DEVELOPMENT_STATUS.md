# 实际开发状态

更新日期：2026-10-20。治理 [PR #1](https://github.com/HPZ07-end/NatureCalls/pull/1) 已合并到 `dev`，提交 `ebdfa31879363864f9fa1b26ca0c2dab5084f447`。M1 从该基线建立 `feat/multiplayer-playground`；本次实现等待独立 PR Review，不自动合并。

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
