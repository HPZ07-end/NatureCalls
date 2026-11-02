# NatureCalls

软件工程课程的厕所主题合作游戏。人类定义系统逻辑，Coding Agent 负责遵守语义的技术实现。共同依据是 [GLOSSARY.md](GLOSSARY.md)，人类可见的信息也是 Agent 必须读取的信息。

游戏从换装准备房（DressingRoom）启动：镜子前换装，左侧门进入联机大厅，可单人离线测试、创建或加入房间（2–4 人），开始后由房主选择地图。地图 Playground 提供第一人称移动、C 切换如厕第三人称、E 对齐测试马桶、隔间门与卷纸交互，以及 NGO Host/Client 原型。已实现 Relay Session 创建/加入界面；仓库尚未关联 Unity Cloud，现阶段可直接进行离线练习及本地直连测试。完整玩法、设施规则和胜负判定尚未实现。实际验收范围见 [DEVELOPMENT_STATUS](docs/DEVELOPMENT_STATUS.md)。

## 打开工程

1. 安装 Unity Hub 和 **6000.3.26f1**，本人完成登录与许可。Mac/Windows 使用同一补丁版本。
2. 克隆仓库，切换团队的 `dev` 分支；在根目录运行 `git lfs install --local`、`git lfs pull`。
3. Hub → Add project from disk，选择包含 `Assets`、`Packages`、`ProjectSettings` 的根目录。
4. 打开 `Assets/Scenes/DressingRoom.unity`，点击 Play。镜子前 E 换装；左侧门 E 打开联机大厅，选「单人离线测试」再选地图即可进入。地图中 WASD / 鼠标操作，Space 跳跃、C 切姿态、E 拾取卷纸或操作隔间门/马桶、R 丢掷卷纸、Esc 打开菜单并可返回准备房。
5. 双客户端：一端在大厅「创建房间」，另一端输入房主局域网地址（同机 `127.0.0.1`）「加入房间」；准备后房主开始并选择地图。直接打开 Playground 的开发诊断方式与 Relay 配置见 [Playground 指南](docs/PLAYGROUND.md)。

平台安装、VS Code 和命令行入口见 [development](docs/DEVELOPMENT.md)。

## 工程概览

`Player` 管理角色及姿态，`Input` 接收本地操作，`Camera` 响应姿态，`Network` 管理 Session/角色同步，`Facility` 与 `Map` 提供测试环境与门、卷纸、镜子等交互对象，`UI` 提供地图内菜单与 DressingRoom 中的换装/大厅/选图界面。`Core` 是换装选择、房间开始条件与地图选择的纯 C# 规则，`Editor` 生成资产与构建，`Tests` 验证实际组件。目录不是玩法定义，逻辑概念树与 C# 类型不要求一一对应。详见 [ARCHITECTURE](docs/ARCHITECTURE.md)。

## 文档导航

- 共同语义：[GLOSSARY](GLOSSARY.md)；Agent 入口：[AGENTS](AGENTS.md)。
- 全部技术文档及放置规则：[docs 索引](docs/README.md)。
- 常用：[PLAYGROUND](docs/PLAYGROUND.md) · [ARCHITECTURE](docs/ARCHITECTURE.md) · [CONTRIBUTING](docs/CONTRIBUTING.md) · [TESTING](docs/TESTING.md) · [DEVELOPMENT_STATUS](docs/DEVELOPMENT_STATUS.md)。
- 历史资料：[C3 转录](docs/history/C3.md) 与 `docs/records/` 下的验收记录。历史设计不覆盖 Glossary。
