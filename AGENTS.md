# NatureCalls Agent 入口

- [GLOSSARY.md](GLOSSARY.md) 是最高且唯一的项目语义权威。Human-visible information ⊆ Agent-visible information；人类定义并批准语义，Agent 维护实现、测试、技术文档和进度。
- 开始任务先识别涉及的概念路径及子树，读取相关人类文档。不得擅自增加、删除、重命名或改变语义；未知机制先提交 Proposed Change 讨论，不写入权威树。发现冲突立即报告并请求人类决定。
- 一个职责一个标准名称；先搜索既有词汇，不建立同义体系。兼容代码标 `legacy`/`compat` 并记录替代项及删除阶段。架构报告说明采用与移除的名称。
- 强制遵循 [CONTRIBUTING](docs/CONTRIBUTING.md)（分支、PR、Review、Semantic Diff、同步）、[IMPLEMENTATION](docs/IMPLEMENTATION.md)、[TESTING](docs/TESTING.md)。开始任务和集成前 fetch 公共分支、记录 Commit、检查自上次同步以来的 Glossary 变化；无可靠记录则重读全文。
- 每次任务产出 Semantic Diff；每个进入 `dev`/`main` 的 PR 即使无语义变化也声明 `none`，并检查代码是否实际引入新领域语义。语义审批不等于普通 Review，合并记录须可追溯。不得自行伪造 Review 或绕过保护。
- 当前为 Stage 1 + 已获明确授权的 M1 联机 Playground，见 [ROADMAP](docs/ROADMAP.md)。M1 允许 URP、Input System、NGO、MPS/Relay、Cinemachine 3 和 Multiplayer Play Mode；不主动做 Stage 2/3 或完整 Gameplay；准确更新 [DEVELOPMENT_STATUS](docs/DEVELOPMENT_STATUS.md)。
- Unity 以 `ProjectSettings/ProjectVersion.txt` 为准；保留资源、场景、GUID 与 `.meta`，通过 Unity API 修改序列化资源。Core 禁止 UnityEngine/UnityEditor；MonoBehaviour 连接场景、输入和表现。
- GUI 与 batchmode 不得并行打开同一工程；先保存关闭 GUI，不自动删锁或强行结束 Editor。登录、许可和管理员授权由用户完成。
- 命令只是尝试，依观察到的文件、日志或 UI 验证结果再执行依赖操作。报告区分未执行、失败、通过；脚本单测不能冒充 Unity 测试。不提交缓存、产物、密钥、许可和个人路径；不强推、不丢弃改动或未经授权改写历史。
- 课程报告及提交材料：应弈劼，学号 2024012833。
