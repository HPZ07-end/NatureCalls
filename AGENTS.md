# NatureCalls 开发约束

- 当前阶段只做环境、文档和测试/构建基础设施；没有明确任务时不实现完整游戏。
- 初步玩法依据 `docs/C3.md`（原件 `docs/C3.pdf`）；保留待定项，不把 4 人建议、本地/联网或未验证机制当成已确认需求。跨平台入门见 `docs/development.md`。
- Unity 版本以 `ProjectSettings/ProjectVersion.txt` 为准（6000.3.26f1）。Mac/Windows 同版本，不自行升级 Editor 或依赖。
- 规范术语：Core（纯 C# 规则和状态）、Player（玩家控制）、Environment（厕所及设备）、UI（界面）、Editor（编辑器自动化）、Tests（测试）。新增模块先搜索现有词汇，一个职责一个名称；避免同义层。兼容代码标 `legacy`/`compat` 并写明替代项和删除阶段。报告架构调整时说明采用和移除的术语。
- Core 禁止 UnityEngine/UnityEditor 依赖；MonoBehaviour 只连接场景、输入与表现。保持简单，不擅自引入 DI、复杂事件总线或大型架构。
- 保留已有资源、场景、GUID。Scene、Prefab、组件及引用使用 Unity API/Editor 修改，不手工拼接 Unity YAML。资产与 `.meta` 一起移动和提交。
- 使用 Visible Meta Files、Force Text。提交 Assets、Packages（含 lock）、ProjectSettings；不提交 Library、Temp、Logs、Builds、UserSettings、Artifacts、账号和许可文件。
- 保持 LF 和路径大小写一致。按实际二进制素材配置 LFS，不能把全部 Unity 文本资源放入 LFS。VS Code 使用 Visual Studio Editor 包，禁止已弃用的 com.unity.ide.vscode。
- 执行命令只是尝试，后续操作必须基于日志、文件或当前 UI 的已观察结果。GUI 操作前确认窗口、项目和焦点；异常后重新观察，不复用旧坐标/标识。
- 同一工程的 Unity GUI 与 batchmode 不得并行；先协调保存并关闭 GUI。不得自动删 Unity 锁、强行关闭编辑器或丢弃用户改动。
- 验证：`python3 scripts/unity.py detect|setup|validate|test-editmode|test-playmode|build-macos`；仓库检查：`python3 scripts/check.py --require-generated`；脚本测试：`python3 -m unittest discover -s scripts/tests -v`。Windows 用 `py -3`。
- 明确区分未执行、失败、通过；测试通过需要非空 XML 和实际通过用例，构建需要成功报告与新产物。不能把脚本单测当作 Unity 测试。日志在 Artifacts。
- Unity 登录、许可激活、管理员授权由用户完成。未经用户明确同意，不创建远程仓库、不推送、不强制改写历史。
- 课程报告与提交材料：应弈劼，学号 2024012833。
