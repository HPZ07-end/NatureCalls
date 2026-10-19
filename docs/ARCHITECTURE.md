# 技术架构

领域含义只在 [Glossary](../GLOSSARY.md) 定义。这里描述技术职责与依赖，当前为 Stage 1，不要求概念树和目录同构。

| 目录 | 技术职责 / 当前内容 |
| --- | --- |
| `Assets/Core` | 预留可独立测试的纯 C# 状态与规则；已有空 `NatureCalls.Core.asmdef`，`noEngineReferences: true`，尚无业务类型 |
| `Assets/Player` | 玩家组件位置；当前占位 |
| `Assets/Facility` | 设施组件位置；从空 `Environment` 目录改名，保留原 GUID；当前占位 |
| `Assets/UI` | 显示与交互适配位置；当前占位 |
| `Assets/Scenes` | 保留 Smoke、Sandbox 场景；不为美观移动场景 |
| `Assets/Editor` | 配置、验证、构建；Editor-only 程序集 |
| `Assets/Tests/EditMode` | Editor-only 测试程序集；项目设置和场景引用检查 |
| `Assets/Tests/PlayMode` | Unity 场景加载测试程序集 |
| `scripts` | Python 标准库命令与仓库/语义检查；可在 Mac/Windows 使用 |

已有 Core `.asmdef` 是初始化遗留的空约束，不表示完成 Stage 2。本次不添加新运行时程序集、不提取业务契约、不制造公共基类。新增概念对应目录只在有实际文件时建立；GameFlow、Incident、Input、Network、Map、Art、Audio、Animation 当前无需空目录。

依赖方向：场景适配组件可以使用纯 C# 逻辑；纯 C# 逻辑不能依赖 Unity；运行时不能依赖 Editor 或测试。Editor 工具依赖 UnityEditor，测试依赖其验证对象与 Test Framework。将来只有需要独立编译边界时才添加程序集引用，禁止循环依赖。

标准命名采用 Facility，移除空目录旧名 Environment。Core 仅为技术模块，不加入逻辑概念树。其他既有名称保留。资源引用使用 GUID；目录改名通过 `AssetDatabase.MoveAsset`，场景路径与原场景字节保持不变。
