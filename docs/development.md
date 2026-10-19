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
| `docs/C3.pdf`、`docs/C3.md` | 提交 | 设计依据；PDF 约 162 KB，普通 Git 足够，无需 LFS |

当前 macOS 专属部分只有 Homebrew 安装示例、Mac Editor 探测路径、`build-macos` 的 ARM64 `.app` 构建，以及本次 Mac 的验收记录。Mac 架构设置只在 `BuildMac` 调用期间临时生效，结束后恢复；`UNITY_STANDALONE_OSX` 条件编译隔离该段 API。没有把 `/Users/...` 路径写入资源、项目配置或工作区设置。

Unity 会在 `.meta`/`.asset` 的空值后生成空格。保留原始序列化，Git 仅对这两类文件放宽行尾空格检查；源码和文档仍检查空白错误。不要让编辑器自动整理 Unity YAML 造成无关差异。

## Windows 队友首次加入

1. 安装 Git for Windows、Git LFS、Python 3.10+、Unity Hub 和 VS Code。每人自行登录 Unity 并激活适用许可；GitHub CLI 只在需要命令行管理 GitHub 时安装。
2. 在 Hub 安装 **6000.3.26f1**，常见 Intel/AMD Windows 电脑选择 x64 Editor。不要因为是 Windows 就使用别的 Unity 补丁版本，也无需装 Mac IL2CPP、Xcode 或 Homebrew。
3. 用 PowerShell 克隆开发分支。建议使用较短的普通本地目录，减少路径长度和同步软件干扰：

```powershell
git clone --branch dev1019 https://github.com/HPZ07-end/NatureCalls.git
cd NatureCalls
git lfs install --local
git lfs pull
py -3 --version
py -3 scripts/unity.py detect
```

4. Hub → Projects → Add project from disk，选择包含 `Assets`、`Packages`、`ProjectSettings` 的仓库根目录。等待首次导入结束。不要选择 `Assets` 子目录，也不要使用另一个模板重新创建工程。
5. 在 VS Code 安装项目推荐扩展；Unity → Edit → Preferences → External Tools，选择 Visual Studio Code，然后 Regenerate project files。
6. 双击 `Assets/Scenes/Smoke.unity`。菜单 `NatureCalls/Validate` 检查配置；Window → General → Test Runner 中分别运行 EditMode 和 PlayMode。`Smoke` 是基础设施场景，`Sandbox` 是保留的编辑器工作场景，当前只有 Smoke 被列入构建。
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
- Windows：在 Unity File → Build Profiles 中选择/添加 Windows 平台，选择 x86_64，确认 Scene List 包含 `Assets/Scenes/Smoke.unity`，构建到 `Builds/Windows/`。缺少平台支持时通过 Hub 添加。当前没有自动化 Windows 构建命令，不要求 Windows 队友运行 `build-macos`。
- 现有 EditMode 测试检查 Force Text、Visible Meta Files；PlayMode 测试检查 Smoke 场景加载和摄像机。它们不代表游戏规则、联网或真实玩法已经实现。
- GitHub Actions 在 Linux、macOS、Windows 上只运行仓库检查与 Python 脚本测试。绿色 CI 不等于三个系统上的 Unity 都已通过；Windows 的 Unity 导入、运行测试和 `.exe` 构建仍需 Windows 队友实测并记录。
- 同一份工程不能同时由 Unity GUI 和 batchmode 打开。脚本会拒绝已占用的项目；不要为了运行脚本删锁或强行结束队友/自己的编辑器。

## 日常协作

开始工作前先确认所在分支和工作区状态，保存改动后再拉取。一个任务一个分支，经 PR 审核合并；不要直接推送自己未理解的 Unity 自动修改。

资源重命名和移动优先在 Unity 内操作，确保 `.meta` 一起变化。Windows 通常不区分文件名大小写，因此资源路径必须精确匹配；需要只改大小写时使用中间名分两次移动并检查 Git 差异。场景与 Prefab 约定负责人，避免同时修改同一资源。

将来的 PSD、FBX、音频等大文件，按实际素材规模选择 LFS；当前不为文本场景、Prefab、`.meta` 或小型设计 PDF 配置 LFS。新增 LFS 模式必须一起提交 `.gitattributes` 并用 `git lfs ls-files` 核实。

玩法讨论以 [C3 设计案](C3.md) 为依据：4 人是首版建议，人数、本地同屏/联网、部分资源用途仍待确定。文档转换不把待定方案变成开发承诺。
