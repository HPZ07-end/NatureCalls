# 演进路线

课程截止日期：**2026-12-21**。以下是顺序与验收目标，不是未经团队确认的人员分工或完成日期。领域范围引用 [Glossary](../GLOSSARY.md)，具体数值、人数、事故规则与失败边界仍需人类决定。

| 阶段 | 内容 | 完成证据 |
| --- | --- | --- |
| Stage 1 — Logical Modules（当前） | 统一 Glossary、逻辑职责、合理目录、基本依赖、PR 与语义同步；不做大规模重构 | 团队 Review 本次治理 PR，语义基线可追溯，CI 通过 |
| Stage 2 — Extract Core（后续） | 随实际需求提取公共契约与稳定数据类型，分离适合测试的纯 C# 逻辑；合适时启用/完善已有空 Core `.asmdef` | 有真实规则单测与清晰依赖，避免为了阶段名称造抽象 |
| Stage 3 — Assembly Boundaries（再后续） | 按需要拆分 Player、Facility 等程序集，用 `.asmdef` 管理静态依赖 | 无循环依赖，编译与测试范围合理 |

业务推进以人类批准的最小任务为单位，可与后续技术阶段按实际需求协调，不要求先拆完程序集才实现玩法：

1. 基础 Gameplay 闭环：先澄清相关子树的未定规则，再逐项实现与验证；不在本治理任务实现。
2. Map 初始化与配置：使用 Unity API 管理场景及每局配置，保留可复现的测试场景。
3. LAN：针对 Infrastructure/Network 制定经批准的同步与 Host/Client 验收方案，之后再选择框架；当前不安装。
4. 跨平台：Mac/Windows 在同一 Commit 验证导入、规则、场景与构建；LAN 用实际 Host/Client 组合记录。
5. 发布：在截止日前留出团队联调、回归、课程材料整理时间。通过验收的版本经 PR 进入 main，Tag 与 Player Settings 版本一致，产物另行分发。

仍需讨论的内容单独走 Proposed Change；不得把 C3 中的 4 人、固定局长或其他未进入基线的参数当作承诺。当前实绩见 [开发状态](DEVELOPMENT_STATUS.md)。
