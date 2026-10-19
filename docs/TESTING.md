# 测试与跨平台验证

Unity **6000.3.26f1**。Mac 使用 `python3`，Windows PowerShell 使用 `py -3`；Python 3.10+ 标准库即可。安装/路径说明见 [development](development.md)。

## 本地命令

先保存并关闭同一项目的 Unity GUI，再依次运行，不能并行启动这些 Editor 命令：

```sh
python3 scripts/unity.py detect
python3 scripts/unity.py validate
python3 scripts/unity.py test-editmode
python3 scripts/unity.py test-playmode
python3 scripts/unity.py build-macos
python3 scripts/check.py --require-generated
python3 -m unittest discover -s scripts/tests -v
```

`setup` 只用于显式初始化/修复配置，不是每人首次启动的必需步骤。`detect` 验证实际 Editor 路径与补丁版本；`validate` 导入、编译并调用 Editor 验证方法。可用 `--timeout 3600` 延长默认 1800 秒限制。

每次 Editor 命令生成独立 `Artifacts/<UTC时间-标识-命令>/`，保存 `editor.log`、`process.log`、`summary.json`；测试生成 `results.xml`。必须检查进程退出、完成回执和非空 XML，发现的测试全部 Passed 才能通过。不能将缺失结果、零测试、跳过或许可失败当通过。PlayMode/构建使用图形设备，需要可用桌面环境；不要给测试额外加 `-quit` 造成提前退出。

脚本发现 `Temp/UnityLockfile` 或 `Artifacts/unity.lock` 时拒绝运行；不自动删锁或强行退出 Editor。崩溃后由人确认进程退出与未保存内容后手工恢复。

## 当前覆盖范围

- EditMode：Force Text、Visible Meta Files、所有 Assets 内 Scene 的加载及序列化对象引用/缺失脚本检查。
- PlayMode：实际加载构建列表中的 Smoke 场景并检查 Camera。
- 仓库检查：路径大小写、JSON/asmdef、`.meta` 成对与 GUID 唯一性、生成目录忽略、包约束、相对 Markdown 文件链接；不请求外部链接，也不验证页面锚点。
- 脚本单测：并发锁、虚假测试成功、Semantic Diff 格式及 baseline 边界。
- 项目当前没有 Prefab 资产，不能声称做过真实 Prefab 行为测试；未来引入时必须添加对应验证。场景检查不证明所有运行时交互正确。
- 未实现玩法规则，因此当前测试不是 Gameplay 验收。

## Semantic Diff

本地检查实际提交（未提交的 Glossary 变化不包含在 HEAD 检查中）：

```sh
python3 scripts/semantic.py check --base origin/dev --head HEAD --body-file Artifacts/pr-body.md
```

PR 正文从 GitHub 事件 JSON 读取，CI 使用实际 base/head，防止 shell 插值正文。`none` 不能隐藏任何 Glossary 文本变化，`baseline` 只能首次进入 dev；changed 的三个路径列表、Reason、Human Approval 必须填写。无 Glossary diff 的代码也可能有语义变化，必须人工检查。

同步工具及人工阅读/确认流程见 [CONTRIBUTING](CONTRIBUTING.md)。格式检查不能验证人类审批真实性；Review 必须核查所引讨论与实际代码。

## 构建与平台记录

macOS `build-macos` 使用 ARM64 Development `.app`，检查 BuildReport、成功回执及新产物，输出到 `Builds/macOS/<时间-标识>/NatureCalls.app`。这不是签名、公证或发布验收；构建成功也不等于人工启动通过。

Windows 在 File → Build Profiles 选择 Windows x86_64，检查 Scene List，构建到 `Builds/Windows/`；需要的模块通过 Hub 安装。暂未提供自动化 Windows build 命令。Windows 不运行 `build-macos`。

每次验收记录 Commit、OS/架构、Editor、用例数、产物、日志位置、结果。Mac/Windows 尽量测试同一 SHA；Network 修改记录 Host OS/SHA、Client OS/SHA、连接和状态同步的真实结果。没运行就明确写未验证。

Actions 的 `repository` 在 Linux/macOS/Windows 运行仓库检查与 Python 单测；`semantic-diff` 检查 PR 语义记录。没有 Unity runner/许可配置，绿色 Actions 不表示 Unity 跨平台通过。实际执行结果见 [治理记录](governance-2026-10-19.md) 与 [开发状态](DEVELOPMENT_STATUS.md)。
