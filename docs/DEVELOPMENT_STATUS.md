# 实际开发状态

更新日期：2026-10-19。原始基线 `e43122a22c61623e57930c34932912bbcdca4a9a`；本次治理在 `chore/repository-governance`，尚待 PR Review/集成。不得把分支上的文档视为已经进入公共分支。

| 类别 | 实际状态 |
| --- | --- |
| 已实现 | Unity 工程、Smoke/Sandbox、跨平台 Editor 路径检测、配置/编译验证、EditMode/PlayMode、Mac ARM64 构建、仓库检查 |
| 本次已编写 | Glossary 初始基线、文档体系、PR/Semantic Diff 检查与同步工具、场景引用测试、候选版本配置；以 PR 为集成边界 |
| 正在推进 | Stage 1 治理 PR 的验证与团队 Review；不是 Gameplay 编码 |
| 已规划 | Stage 2/3、经批准的 Gameplay 闭环、Map 配置、LAN、同 Commit 的 Mac/Windows 验收和发布 |
| 尚未确定 | 玩家数量、数值与阈值、基线未详述的失败/惩罚细节、事故规则、网络技术方案、子树负责人；由人类讨论，不沿用 C3 自动补全 |
| 尚未实现 | 玩家操作、设施交互、运行时规则/状态、游戏 UI、LAN、实际美术/音频/动画内容 |

原始基线已有真实 Mac 导入、EditMode 2/2、PlayMode 1/1、ARM64 构建通过记录，见 [首次验收](setup-2026-10-19.md)。本次重新验证：Mac 导入/编译通过，EditMode **3/3**、PlayMode **1/1**，ARM64 新构建成功，脚本测试 **17/17**。详细日志与覆盖边界见 [治理记录](governance-2026-10-19.md)。

Windows 的 Unity 导入/测试/构建、跨系统 LAN、Mac 构建人工运行、签名/公证均未验收。CI 的三个 OS 仅覆盖 Python/仓库检查。

当前仅 Stage 1；空 Core `.asmdef` 早已存在但没有规则实现，不代表 Stage 2 完成。后续路线与课程截止日期见 [ROADMAP](ROADMAP.md)。
