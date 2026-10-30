# NatureCalls

软件工程课程的厕所主题合作游戏。人类定义系统逻辑，Coding Agent 负责遵守语义的技术实现。共同依据是 [GLOSSARY.md](GLOSSARY.md)，人类可见的信息也是 Agent 必须读取的信息。

目前已完成 Unity 工程、配置验证、场景冒烟测试和 macOS 构建基础设施。`Smoke` 只有默认摄像机和灯光，`Sandbox` 是保留的工作场景；玩家操作、设施交互、Gameplay 闭环和 LAN 均未实现。详细状态见 [DEVELOPMENT_STATUS](docs/DEVELOPMENT_STATUS.md)。

## 打开工程

1. 安装 Unity Hub 和 **6000.3.26f1**，本人完成登录与许可。Mac/Windows 使用同一补丁版本。
2. 克隆仓库，切换团队的 `dev` 分支；在根目录运行 `git lfs install --local`、`git lfs pull`。
3. Hub → Add project from disk，选择包含 `Assets`、`Packages`、`ProjectSettings` 的根目录。
4. 打开 `Assets/Scenes/Smoke.unity`，点击 Play。测试入口是 Window → General → Test Runner。

平台安装、VS Code 和命令行入口见 [development](docs/development.md)；当前治理变更在独立 PR 中，合入 `dev` 前以 PR 分支预览新文档。

## 工程概览

`Core` 留作纯 C# 逻辑位置；`Player`、`Facility`、`UI` 是当前逻辑目录；`Editor` 提供配置与构建，`Tests` 提供验证。目录不是玩法定义，逻辑概念树与 C# 类型不要求一一对应。详见 [ARCHITECTURE](docs/ARCHITECTURE.md)。

## 文档导航

- 共同语义：[GLOSSARY](GLOSSARY.md)；Agent 入口：[AGENTS](AGENTS.md)。
- 开发：[CONTRIBUTING](docs/CONTRIBUTING.md) · [IMPLEMENTATION](docs/IMPLEMENTATION.md) · [TESTING](docs/TESTING.md)。
- 计划与记录：[ROADMAP](docs/ROADMAP.md) · [DEVELOPMENT_STATUS](docs/DEVELOPMENT_STATUS.md) · [RELEASES](docs/RELEASES.md) · [治理记录](docs/governance-2026-10-19.md)。
- 历史资料：[C3 转录](docs/C3.md) · [原始 PDF](docs/C3.pdf) · [首次环境验收](docs/setup-2026-10-19.md)。历史设计不覆盖 Glossary。
