# NatureCalls

软件工程课程的厕所主题合作游戏。人类定义系统逻辑，Coding Agent 负责遵守语义的技术实现。共同依据是 [GLOSSARY.md](GLOSSARY.md)，人类可见的信息也是 Agent 必须读取的信息。

当前 M1 Playground 提供第一人称移动、C 切换如厕第三人称、E 对齐测试马桶，以及 NGO Host/Client 原型。已实现 Relay Session 创建/加入界面；仓库尚未关联 Unity Cloud，现阶段可直接进行离线练习及本地直连测试。完整玩法、设施规则和胜负判定尚未实现。实际验收范围见 [DEVELOPMENT_STATUS](docs/DEVELOPMENT_STATUS.md)。

## 打开工程

1. 安装 Unity Hub 和 **6000.3.26f1**，本人完成登录与许可。Mac/Windows 使用同一补丁版本。
2. 克隆仓库，切换团队的 `dev` 分支；在根目录运行 `git lfs install --local`、`git lfs pull`。
3. Hub → Add project from disk，选择包含 `Assets`、`Packages`、`ProjectSettings` 的根目录。
4. 打开 `Assets/Scenes/Playground.unity`，点击 Play。点击菜单右侧画面后用 WASD / 鼠标操作，Space 跳跃、C 切姿态、E 拾取卷纸或操作隔间门/马桶、R 丢掷卷纸、Esc 释放鼠标。
5. 双客户端：展开 `Local / LAN test tools`，一端 `Local Host`，另一端填 `127.0.0.1` 后 `Local Join`。跨机器改填 Host 局域网地址。Relay Join Code 需要先配置 Cloud，见 [Playground 指南](docs/PLAYGROUND.md)。

平台安装、VS Code 和命令行入口见 [development](docs/DEVELOPMENT.md)。

## 工程概览

`Player` 管理角色及姿态，`Input` 接收本地操作，`Camera` 响应姿态，`Network` 管理 Session/角色同步，`Facility` 与 `Map` 提供测试环境与门、卷纸、镜子等交互对象，`UI` 提供连接入口、换装面板与 Lobby Mock。`Core` 目前只有换装选择的纯 C# 状态，`Editor` 生成资产与构建，`Tests` 验证实际组件。目录不是玩法定义，逻辑概念树与 C# 类型不要求一一对应。详见 [ARCHITECTURE](docs/ARCHITECTURE.md)。

## 文档导航

- 共同语义：[GLOSSARY](GLOSSARY.md)；Agent 入口：[AGENTS](AGENTS.md)。
- 全部技术文档及放置规则：[docs 索引](docs/README.md)。
- 常用：[PLAYGROUND](docs/PLAYGROUND.md) · [ARCHITECTURE](docs/ARCHITECTURE.md) · [CONTRIBUTING](docs/CONTRIBUTING.md) · [TESTING](docs/TESTING.md) · [DEVELOPMENT_STATUS](docs/DEVELOPMENT_STATUS.md)。
- 历史资料：[C3 转录](docs/history/C3.md) 与 `docs/records/` 下的验收记录。历史设计不覆盖 Glossary。
