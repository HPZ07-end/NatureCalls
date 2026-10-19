# macOS / Windows 协作开发

两种系统使用同一份 Unity 项目和同一个 Editor 补丁版本 **6000.3.26f1**。不要另建 Windows 版或 Mac 版工程，也不要提交各自生成的 Library。

## 哪些内容共用，哪些属于本机

| 内容 | Git 中的处理 | 平台说明 |
| --- | --- | --- |
| `Assets/`、场景、C#、`.asmdef`、`.meta` | 一起提交 | 共用；资源 GUID 不能按机器重新生成 |
| `Packages/manifest.json`、`packages-lock.json` | 提交 | 共用；由 Unity 解析包，锁文件中的内置包不是 Mac 专属 |
| `ProjectSettings/` | 提交 | 共用；文件内同时含 Windows/macOS/其他平台字段是正常现象，不要删掉队友平台的配置 |
| `scripts/` | 提交 | Python 3.10+ 标准库；macOS 用 `python3`，Windows 用 `py -3` |
| `.gitattributes`、`.editorconfig`、`.gitignore` | 提交 | 共用 LF、忽略规则和格式约定 |
| `.vscode/extensions.json` | 提交 | 只推荐扩展 ID；每位开发者安装适合本机系统/架构的扩展 |
| Editor、Hub、Python、Git 的安装 | 不提交 | 各自安装；ARM64 Mac 和 x64 Windows 可使用相同版本的不同架构 Editor |
| `UNITY_EDITOR`、External Script Editor 设置 | 不提交个人值 | 各机不同；脚本自动探测各平台的标准路径，只有自定义安装才需设置环境变量 |
| `Library/`、`Temp/`、`Logs/`、`UserSettings/`、`Obj/` | 忽略 | 本机缓存、窗口布局、当前平台等状态，由 Unity 自行生成 |
| `Build/`、`Builds/`、`Artifacts/` | 忽略 | 平台构建产物、测试 XML 和日志；需要共享时单独作为 CI artifact/发布包提供 |
| `.csproj`、`.sln`、个人 `.vscode/settings.json` | 忽略 | Unity/VS Code 在本机生成，不复制 Mac 的绝对路径给 Windows |
| Unity 登录、许可、GitHub 凭证 | 不提交 | 每人独立完成登录和授权 |
| `docs/C3.pdf`、`docs/C3.md` | 提交 | 历史资料；语义以 Glossary 为准；PDF 约 162 KB，普通 Git 足够，无需 LFS |

当前 macOS 专属部分只有 Homebrew 安装示例、Mac Editor 探测路径、`build-macos` 的 ARM64 `.app` 构建，以及本次 Mac 的验收记录。Mac 架构设置只在 `BuildMac` 调用期间临时生效，结束后恢复；`UNITY_STANDALONE_OSX` 条件编译隔离该段 API。没有把 `/Users/...` 路径写入资源、项目配置或工作区设置。

Unity 会在 `.meta`/`.asset`/`.unity`/`.prefab`/`.mat` 的空值后生成空格。保留原始序列化，Git 仅对这些序列化文件放宽行尾空格检查；源码和文档仍检查空白错误。不要让编辑器自动整理 Unity YAML 造成无关差异。

## Windows 队友首次加入

1. 安装 Git for Windows、Git LFS、Python 3.10+、Unity Hub 和 VS Code。每人自行登录 Unity 并激活适用许可；GitHub CLI 只在需要命令行管理 GitHub 时安装。
2. 在 Hub 安装 **6000.3.26f1**，常见 Intel/AMD Windows 电脑选择 x64 Editor。不要因为是 Windows 就使用别的 Unity 补丁版本，也无需装 Mac IL2CPP、Xcode 或 Homebrew。
3. 用 PowerShell 克隆开发分支。建议使用较短的普通本地目录，减少路径长度和同步软件干扰：

```powershell
git clone --branch dev https://github.com/HPZ07-end/NatureCalls.git
cd NatureCalls
git lfs install --local
git lfs pull
py -3 --version
py -3 scripts/unity.py detect
```

4. Hub → Projects → Add project from disk，选择包含 `Assets`、`Packages`、`ProjectSettings` 的仓库根目录。等待首次导入结束。不要选择 `Assets` 子目录，也不要使用另一个模板重新创建工程。
5. 在 VS Code 安装项目推荐扩展；Unity → Edit → Preferences → External Tools，选择 Visual Studio Code，然后 Regenerate project files。
6. M1 合并前先 `git switch feat/multiplayer-playground`（需要先 fetch）；打开 `Assets/Scenes/Playground.unity`。菜单 `NatureCalls/Validate` 检查配置；Window → General → Test Runner 中分别运行 EditMode 和 PlayMode。Playground 是首个构建场景，Smoke/Sandbox 保留。操作及两端联机见 [PLAYGROUND](PLAYGROUND.md)。
7. 保存并关闭这个项目的 Unity GUI 后，可在 PowerShell 自动化验证：

```powershell
py -3 scripts/unity.py validate
py -3 scripts/unity.py test-editmode
py -3 scripts/unity.py test-playmode
py -3 scripts/check.py --require-generated
py -3 -m unittest discover -s scripts/tests -v
```

如果 `detect` 找不到自定义目录中的 Editor，设置当前终端的环境变量，指向 `Unity.exe`：

```powershell
$env:UNITY_EDITOR = 'D:\Unity\6000.3.26f1\Editor\Unity.exe'
py -3 scripts/unity.py detect
```

不要把这个示例路径直接提交到共享配置；以自己实际安装位置为准。环境变量指向的 Editor 也必须通过版本校验。

## 构建与验证边界

- macOS：`python3 scripts/unity.py build-macos` 构建 ARM64 Development `.app`，适合本机 Apple Silicon 验证；不是 Windows 可执行文件。
- Windows：在 Unity File → Build Profiles 中选择/添加 Windows 平台，选择 x86_64，确认 Scene List 首项为 `Assets/Scenes/Playground.unity`，构建到 `Builds/Windows/`。缺少平台支持时通过 Hub 添加。当前没有自动化 Windows 构建命令，不要求 Windows 队友运行 `build-macos`。
- 现有 EditMode/PlayMode 覆盖设置、资源、角色、镜头及 Host 生命周期；双进程验证与平台验收边界见 [TESTING](TESTING.md)。不能把本机直连成功写成 Relay 或 Windows 已通过。
- GitHub Actions 在 Linux、macOS、Windows 上只运行仓库检查与 Python 脚本测试。绿色 CI 不等于三个系统上的 Unity 都已通过；Windows 的 Unity 导入、运行测试和 `.exe` 构建仍需 Windows 队友实测并记录。
- 同一份工程不能同时由 Unity GUI 和 batchmode 打开。脚本会拒绝已占用的项目；不要为了运行脚本删锁或强行结束队友/自己的编辑器。

## 日常协作

统一遵循 [CONTRIBUTING](CONTRIBUTING.md)；实现约束见 [IMPLEMENTATION](IMPLEMENTATION.md)，完整验证与日志说明见 [TESTING](TESTING.md)。

语义以 [GLOSSARY](../GLOSSARY.md) 为唯一权威。[C3](C3.md) 保留为历史设计转录，不把其中未纳入基线的机制视为开发承诺。

## Mac 安装补充

先检查已有软件，缺失时可用 Homebrew 安装 `git git-lfs gh python`，应用使用 `unity-hub visual-studio-code` cask。不要在 Windows 安装 Homebrew。VS Code 推荐 C#、C# Dev Kit、Unity 三个扩展，ID 见 `.vscode/extensions.json`；每人本机安装并生成工程文件。

Hub 中安装锁定的 Editor，优先 GUI；旧 Hub headless CLI 已弃用，工程自动化直接调用 Editor。基础 Mac Mono 构建不要求先装所有平台模块，IL2CPP/Xcode 在确需该构建链时安装。许可、账号和管理员授权由本人完成。
