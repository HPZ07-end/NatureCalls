# 文档索引

语义唯一权威是根目录 [GLOSSARY](../GLOSSARY.md)；Agent 入口是 [AGENTS](../AGENTS.md)。本目录的技术文档不另建语义。

## 上手与使用

| 文档 | 内容 |
| --- | --- |
| [DEVELOPMENT](DEVELOPMENT.md) | Mac/Windows 安装、首次打开工程、IDE 与阅读代码顺序 |
| [PLAYGROUND](PLAYGROUND.md) | M1 Playground 操作、组件边界、镜头参数、本地双客户端、Relay 配置 |
| [PLAYER](PLAYER.md) | Player Prefab 结构、外观接口与跨场景契约 |

## 规范与流程

| 文档 | 内容 |
| --- | --- |
| [CONTRIBUTING](CONTRIBUTING.md) | 分支、提交、PR、Review、Semantic Diff、Glossary 同步 |
| [IMPLEMENTATION](IMPLEMENTATION.md) | 代码与 Unity 资源的实现约束 |
| [TESTING](TESTING.md) | 本地命令、测试覆盖范围、双客户端与平台验收 |
| [RELEASES](RELEASES.md) | 版本号、Tag 与发布步骤 |

## 架构与进度

| 文档 | 内容 |
| --- | --- |
| [ARCHITECTURE](ARCHITECTURE.md) | 目录与模块职责、依赖流、网络模型 |
| [DEVELOPMENT_STATUS](DEVELOPMENT_STATUS.md) | 当前实际状态（只写现状） |
| [ROADMAP](ROADMAP.md) | 阶段路线与课程截止日 |

## 记录与历史（只追加，不改写）

| 文档 | 内容 |
| --- | --- |
| [records/status-log.md](records/status-log.md) | 按日期的开发与回归记录 |
| [records/setup-2026-10-19.md](records/setup-2026-10-19.md) | 首次环境验收 |
| [records/governance-2026-10-19.md](records/governance-2026-10-19.md) | 治理任务记录与权限观察 |
| [records/m1-2026-10-20.md](records/m1-2026-10-20.md) | M1 验收记录 |
| [history/C3.md](history/C3.md)、[C3.pdf](history/C3.pdf) | 早期设计案转录与原件；非规范，不覆盖 Glossary |

## 放置规则

- 现状、用法和约束写进上面对应的常用文档，并在行为、命令、路径或名称变化时同次更新。
- 带日期的验收结果、逐次变更说明写入 `records/`（追加到 `status-log.md` 或新建 `records/<主题>-<日期>.md`），常用文档只链接，不堆积历史。
- 新文档先确认没有覆盖同一主题的现有文档，并在本索引登记。
