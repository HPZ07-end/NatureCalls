# 演进路线

课程截止日期：**2026-12-21**。以下是顺序与验收目标，不是未经团队确认的人员分工或完成日期。领域范围引用 [Glossary](../GLOSSARY.md)，M1 人数已批准为 2–4，正式玩法数值、事故规则与失败边界仍需人类决定。

| 阶段 | 内容 | 完成证据 |
| --- | --- | --- |
| Stage 1 — Logical Modules（当前） | 统一 Glossary、逻辑职责、合理目录、基本依赖、PR 与语义同步；不做大规模重构 | 治理 PR #1 已合并；M1 另走功能 PR，语义及实现须 Review |
| Stage 2 — Extract Core（后续） | 随实际需求提取公共契约与稳定数据类型，分离适合测试的纯 C# 逻辑；合适时启用/完善已有空 Core `.asmdef` | 有真实规则单测与清晰依赖，避免为了阶段名称造抽象 |
| Stage 3 — Assembly Boundaries（再后续） | 按需要拆分 Player、Facility 等程序集，用 `.asmdef` 管理静态依赖 | 无循环依赖，编译与测试范围合理 |

业务推进以人类批准的最小任务为单位，可与后续技术阶段按实际需求协调，不要求先拆完程序集才实现玩法：

1. M1 Playground（当前）：第一人称移动、姿态驱动第三人称、厕所占位环境、本地 NGO 验证；补齐 Cloud/Relay 和 Windows 验收后集成。
2. 基础 Gameplay 闭环：先澄清相关子树的未定规则，再逐项实现与验证；不在 M1 提前实现。
3. Map 初始化与配置：使用 Unity API 管理场景及每局配置，保留可复现的测试场景。
4. Network：已采用 NGO + MPS/Relay；完成真实 LAN/Relay 跨设备验证及连接失败回归，再按批准需求扩展，暂不做完整大厅或 Host 迁移。
5. 跨平台：Mac/Windows 在同一 Commit 验证导入、规则、场景与构建；LAN 用实际 Host/Client 组合记录。
6. 发布：在截止日前留出团队联调、回归、课程材料整理时间。通过验收的版本经 PR 进入 main，Tag 与 Player Settings 版本一致，产物另行分发。

仍需讨论的内容单独走 Proposed Change；M1 的 2–4 人来自本次人类授权，不得把 C3 的固定局长或其他未进入基线的参数当作承诺。当前实绩见 [开发状态](DEVELOPMENT_STATUS.md)。
