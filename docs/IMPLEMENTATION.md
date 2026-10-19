# 实现规范

先读 [Glossary](../GLOSSARY.md) 和 [架构](ARCHITECTURE.md)。Agent 决定技术方案，不能替人类补完规则。C3 是历史资料，出现差异时不得把旧设计自动实现为规则。

- 使用简单普通 C# 类管理可分离的状态与规则；Core 不依赖 UnityEngine/UnityEditor。MonoBehaviour 连接场景对象、输入、生命周期和表现。当前不主动抽取 Core，见 [路线](ROADMAP.md)。
- 先搜索标准概念名与现有代码。避免同义层、无需求的 DI、复杂事件总线、网络框架及大型架构。公共接口变化在 PR 单列，领域语义变化须先批准。
- Editor 为 **6000.3.26f1**（以版本文件为准）；VS Code 集成使用 `com.unity.ide.visualstudio` **2.0.28**，禁止弃用的 `com.unity.ide.vscode`。Test Framework **1.6.0**，保留包锁，不自行升级或降级。
- `.asmdef` 是依赖边界而非概念目录清单。保留现有程序集，不在 Stage 1 拆出 Player、Facility 等程序集。
- Scene、Prefab、ScriptableObject、组件及引用通过 Unity Editor/API 修改，不拼 Unity YAML。移动使用 `AssetDatabase.MoveAsset`，保留 `.meta` 和 GUID；不要批量重序列化无关资源。
- Force Text、Visible Meta Files 是项目约束。`NatureCalls/Setup` 只补缺失 Smoke 场景、加入构建列表并设定配置；不会覆盖已有场景或删除其他构建场景。
- 文本使用 LF，路径严格大小写一致；处理仅大小写改名时先用中间名。Unity 自己写出的 `.meta`/`.asset` 空值行尾空格保留，避免格式工具修改 YAML。
- 平台专属 API 用条件编译隔离；`BuildMac` 在 `UNITY_STANDALONE_OSX` 下临时选择 ARM64，完成后恢复架构设置。不得把个人绝对路径写进共享配置。
- 新增第三方资产必须记录来源与许可证；未知授权的素材不提交。大型二进制按实际路径/类型加入 LFS，文本资产不统一放 LFS。不提交密钥和许可文件。

每次实现结束同时维护必要测试、实际开发状态和 Semantic Diff；未确定机制应在 PR/讨论中作为 Proposed Change，获批后才更新 Glossary。不要通过技术文档复制或重新解释领域定义。
