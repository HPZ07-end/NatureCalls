# NatureCalls

软件工程课程的厕所主题合作游戏。当前阶段只建立开发、测试、构建基础设施；尚未实现玩家、设备交互或合作玩法。

仓库：<https://github.com/HPZ07-end/NatureCalls>。日常开发分支：`dev1019`。

文档入口：[C3 初步设计案](docs/C3.md) · [原始 PDF](docs/C3.pdf) · [Windows/macOS 协作与入门](docs/development.md) · [实际验收记录](docs/setup-2026-10-19.md)。

## 统一环境

- Unity **6000.3.26f1 / Unity 6.3 LTS**，revision `de8167e7440c`。唯一版本依据为 `ProjectSettings/ProjectVersion.txt`，Mac 和 Windows 必须使用相同补丁版本。
- Apple Silicon 安装 ARM64 Editor；Windows 安装适合该机器架构的同版本 Editor。
- Git、Git LFS、Python 3.10+、VS Code。macOS 可用 Homebrew；Windows 可从各工具官网安装。
- VS Code 扩展：C# (`ms-dotnettools.csharp`)、C# Dev Kit (`ms-dotnettools.csdevkit`)、Unity (`visualstudiotoolsforunity.vstuc`)。扩展可能在首次打开 C# 工程时继续下载 .NET 组件。
- Unity Hub 登录和许可激活由本人完成。不要提交账号、令牌、许可证或机器本地路径。

macOS 安装缺少的工具（先检查已有安装，不重复安装）：

```sh
brew install git git-lfs gh python
brew install --cask unity-hub visual-studio-code
code --install-extension visualstudiotoolsforunity.vstuc
```

Unity Hub → Installs 中安装锁定版本；macOS 首次验证采用内置桌面 Mono 构建链。IL2CPP 留到确有需求时安装 Mac Build Support (IL2CPP)，不要为基础验证安装 Android/iOS 等无关模块。本次安装使用的兼容命令如下；Hub 3.18 起官方已将 Hub CLI 标为 deprecated，后续安装优先用 Hub GUI 或新版 Unity CLI。项目自动化直接调用 Editor，不依赖 Hub CLI。

```sh
"/Applications/Unity Hub.app/Contents/MacOS/Unity Hub" -- --headless install \
  --version 6000.3.26f1 --changeset de8167e7440c --architecture arm64
```

## 首次启动

1. 克隆已有仓库，进入根目录，运行 `git lfs install --local` 和 `git lfs pull`。
2. 确认 Hub 中安装、许可均完成。根目录包含 `Assets/`、`Packages/`、`ProjectSettings/`，无需在 Hub 中另建同名项目。
3. **关闭此项目的 Unity GUI**，执行下方 `detect` 和 `validate`。首次导入需要网络获取包；仓库已包含 `.meta`、`packages-lock.json`、项目设置及 Smoke 场景，应保留它们。`setup` 用于首次初始化或显式修复配置，不需要每人重新生成资产 GUID。
4. Hub → Projects → Add project from disk，选择本仓库根目录。
5. Unity → Settings/Preferences → External Tools → External Script Editor 选择 VS Code，点击 Regenerate project files。Windows 对应 Edit → Preferences。
6. 打开 `Assets/Scenes/Smoke.unity` 并 Play。这是只有默认摄像机和灯光的基础设施验证场景，不是游戏 Demo。

`setup` 用 Unity API 设置 **Force Text** 和 **Visible Meta Files**。它只在 Smoke 场景不存在时创建，不覆盖已有场景，不清空构建场景列表；已有但被禁用的 Smoke 场景需手动启用。也可在 Unity 菜单执行 `NatureCalls/Setup`，随后 `NatureCalls/Validate`。

使用 `com.unity.ide.visualstudio` **2.0.28** 集成 VS Code；不要添加已弃用的 `com.unity.ide.vscode`。Test Framework **1.6.0** 是此 Editor 系列的核心包，随 Editor 提供，不能按公共注册表显示的旧版降级。

## 可重复执行的命令

macOS 在根目录使用 `python3`；Windows PowerShell 用 `py -3` 替换 `python3`。命令实现仅依赖 Python 标准库，不依赖 Bash。

```sh
python3 scripts/unity.py detect
python3 scripts/unity.py setup
python3 scripts/unity.py validate
python3 scripts/unity.py test-editmode
python3 scripts/unity.py test-playmode
python3 scripts/unity.py build-macos
python3 scripts/check.py --require-generated
python3 -m unittest discover -s scripts/tests -v
```

默认扫描 Hub 标准路径。自定义安装位置通过 `UNITY_EDITOR` 指向 **Editor 可执行文件**，不是 `.app` 目录，也不是 Unity Hub：

```sh
export UNITY_EDITOR="/Applications/Unity/Hub/Editor/6000.3.26f1/Unity.app/Contents/MacOS/Unity"
```

```powershell
$env:UNITY_EDITOR = 'C:\Program Files\Unity\Hub\Editor\6000.3.26f1\Editor\Unity.exe'
py -3 scripts/unity.py detect
```

`detect` 运行 Editor `-version` 核对实际版本；不允许静默用别的版本打开工程。`validate` 导入/编译并调用 Editor 验证方法；`setup` 完成配置后也做验证。默认超时 1800 秒，可用 `--timeout 3600` 调整。

每次 Editor 运行使用新的 `Artifacts/<UTC时间-标识-命令>/`，保存 `editor.log`、`process.log`、`summary.json`；测试额外保存 NUnit `results.xml`。测试必须实际发现至少一个用例、全部通过且 Unity 退出码成功，才报告成功。缺失 XML、零测试、跳过测试或编译/许可错误均不会被判定通过。测试命令没有 `-quit`，由 Test Framework 完成后退出。

`build-macos` 生成 `Builds/macOS/<时间-标识>/NatureCalls.app`，显式使用 **ARM64 Development Build**，检查 BuildReport 和新的 app 目录。当前只承诺在 Mac 上验证此构建命令；Windows 团队成员可运行导入、EditMode、PlayMode，并通过 Unity Build Profiles 构建 Windows。PlayMode 和构建保留图形设备；需要正常桌面会话。签名、公证和发布分发不属于本轮范围。

**同一项目不能同时由 GUI 和 batchmode 打开。** 脚本发现 `Temp/UnityLockfile` 或另一脚本的 `Artifacts/unity.lock` 时拒绝启动，不自动关 GUI、不自动删锁。崩溃后先用进程列表确认 Editor/脚本完全退出，再人工处理残留锁。不要从 Hub 同时打开正在测试的工程。

## 目录与边界

| 目录 | 职责 |
| --- | --- |
| `Assets/Core` | 普通 C# 状态、规则；`noEngineReferences` 禁止依赖 UnityEngine，目前尚无玩法类型 |
| `Assets/Player` | 玩家控制及输入与场景的连接 |
| `Assets/Environment` | 厕所、设备和场景交互 |
| `Assets/UI` | 游戏界面及表现 |
| `Assets/Editor` | 项目配置、验证、场景生成、构建；不会进入 Player |
| `Assets/Tests/EditMode` | 规则测试位置；当前测试协作所需的项目设置 |
| `Assets/Tests/PlayMode` | Unity 生命周期/场景加载测试 |
| `scripts` | Mac/Windows 通用命令和仓库检查 |

未来 Core 新增实际规则后，再让需要它的程序集引用 `NatureCalls.Core` 并加入规则测试；当前没有为了测试而编造游戏规则。MonoBehaviour 负责输入、场景对象和表现，不包办全部业务状态。不引入 DI 框架、复杂事件总线或网络框架。

## Git 协作

- 提交 `Assets`（包含 `.meta`）、`Packages/manifest.json`、Unity 生成的 `Packages/packages-lock.json` 和 `ProjectSettings`。移动资源时同时移动 `.meta`，优先在 Unity 内操作；不要重新生成已有 GUID。
- `.gitignore` 来自 [GitHub 官方 Unity 模板](https://github.com/github/gitignore/blob/main/Unity.gitignore)，末尾补充本项目报告和本地编辑器文件。忽略 `Library`、`Temp`、`Logs`、`Build`、`Builds`、`UserSettings`、`Artifacts`。
- `.gitattributes` 统一文本 LF。路径大小写必须完全一致；避免仅大小写不同的文件名。不要批量重新序列化无关资源。
- 当前没有二进制美术素材，因此没有空泛的 LFS 通配规则。引入大体积 PSD/FBX/音频等时，先按实际路径或类型执行 `git lfs track 'Assets/Art/*.psd'`，把 `.gitattributes` 与素材一起提交，并用 `git lfs ls-files` 验证。不要给全部 `.unity`、`.prefab`、`.asset`、`.meta` 开 LFS。
- 小任务建分支、PR 审核后合并。场景/Prefab 修改前约定负责人，避免两人同时编辑同一场景；冲突用 UnityYAMLMerge 或在 Unity 中解决引用，不能随意拼 YAML。
- 不提交生成的 `.csproj`/`.sln`、个人 VS Code 配置、许可证或构建成品。`gh auth status` 检查授权；失效时本人执行 `gh auth login`。
- 未经明确授权不推送、不创建远程仓库、不强制改写历史。

GitHub Actions 的 `Repository checks` 在 Linux/macOS/Windows 检查大小写、GUID、忽略规则、JSON、首次导入产物和脚本故障检测。**它不是 Unity 编译或游戏测试**，不需要 Unity 许可。Unity CI 待团队确定合法的 runner/许可方案后再接入；本轮未配置账号密钥。

## 验收与后续

本次实测状态见 [初始化记录](docs/setup-2026-10-19.md)。只有真实运行过且有结果的检查才记为通过。首次导入完成后执行上述完整命令序列，并保留测试 XML、构建摘要；不要将 Python 脚本测试结果当作 Unity EditMode 结果。

后续范围以 [C3 设计案](docs/C3.md) 为准：首版建议按 4 人考虑、单局 10 分钟，围绕个人急迫与公共清洁形成合作循环；本地同屏或联网等载体仍待团队确定。先验证一张灰盒地图上的基础循环，将规则状态放入 Core 并测试，再接 Player/Environment/UI。此前“两人本地 Demo”只是临时建议，不作为已定需求。

参考：[Unity 6000.3.26f1](https://unity.com/releases/editor/whats-new/6000.3.26f1)、[Hub CLI](https://docs.unity.com/en-us/hub/use-hub-cli)、[VS Code Unity 集成](https://code.visualstudio.com/docs/other/unity)、[Test Framework 1.6](https://docs.unity3d.com/Packages/com.unity.test-framework@1.6/manual/index.html)。
