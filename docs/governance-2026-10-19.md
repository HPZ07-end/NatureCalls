# Repository & Semantic Governance v1 执行记录

## Git 调查与保全

开始时工作区干净，无 Tag、无 PR。实际旧分支是 `dev1019`（不存在 `dev_1019`）。它与 origin/dev1019 同为 `e43122a22c61623e57930c34932912bbcdca4a9a`；本地 main 为祖先 `0b42e67b9ab70f4e8a23a82fe3c05d4b9c106d83`，远程原来没有 main/dev。

已保存完整 `Artifacts/governance/before.bundle` 并通过 `git bundle verify`。main 快进到原始完整基线，dev 从相同 Commit 建立，两个远程分支已核实 SHA 相等。无 force push、无历史改写。任务分支 `chore/repository-governance` 从 dev 派生，变更经目标为 dev 的 [PR #1](https://github.com/HPZ07-end/NatureCalls/pull/1) 交付。治理实现提交为 `f6dbd8ea5614a567f6309111aa73de39948b4abc`；后续提交补充验收记录，不改变已验证的 Unity 代码和资产。

账号权限为 WRITE；修改默认分支的 API 返回 HTTP 404，默认仍是 dev1019。它仍是 GitHub 默认分支，因此本次暂不删除本地/远程旧分支。其全部历史已被 main/dev 和本地 bundle 保留。管理员切换默认分支后，再 fetch 核实旧分支没有新提交、无未合并工作，并确认旧 SHA 可从远程 main/dev 到达，才可正常删除 origin/dev1019 和本地 dev1019（使用 `git branch -d`，不强删）。

## 语义与文件

`Semantic-Diff: baseline`。人类批准来源：2026-10-19 用户「NatureCalls — Repository & Semantic Governance v1」Part 2.3 的完整语义树。此请求提供初始语义授权，不冒充另一名团队成员的 PR Review。

根概念 NatureCalls，下设 Gameplay、Infrastructure、Content、Development；各子树完整保留用户给定定义，详见 [唯一规范树](../GLOSSARY.md)。不将 Core 强塞入树，不补完人数、阈值等未定机制。

采用 Facility 标准名称，空目录 Environment 通过 `AssetDatabase.MoveAsset` 改名；GUID `ae4baf54d6ca246548d6ad882c66b5c8` 保留。Smoke/Sandbox 路径及内容保留。一次性 Editor 工具执行后已删除，不引入长期迁移层。Player Settings 通过 Unity API 改为 `0.1.0-alpha.1`；Editor 版本不变。

C3 的 Markdown/PDF 保留为历史资料；Markdown 仅补充非规范提示，原转录不重写。原 PDF 不变。旧文档中的 Environment 名称、C3 语义权威和 dev1019 日常流程已在现行入口替换；历史验收记录保留当时事实。LAN 与 PaperRoll 等现在以用户新基线为准，C3 未纳入树的机制不自动继承。

## 验证结果

本轮在 macOS Apple Silicon，实际 Editor 路径为 `/Applications/Unity/Hub/Editor/6000.3.26f1/Unity.app/Contents/MacOS/Unity`。以下均为本轮真实结果，测试时的 Assets/Packages/ProjectSettings/scripts 快照与 `f6dbd8ea5614a567f6309111aa73de39948b4abc` 一致；日志为本机忽略产物，未提交 Git。

| 项目 | 结果 / 证据 |
| --- | --- |
| 导入、编译、配置 | 通过；`Artifacts/20261019T105027Z-916849-validate/` |
| EditMode | **3/3**；含两个 Scene 的缺失脚本/引用检查；`Artifacts/20261019T105029Z-624ae1-test-editmode/results.xml` |
| PlayMode | **1/1**；Smoke 加载及 Camera；`Artifacts/20261019T105031Z-d5e03d-test-playmode/results.xml` |
| Mac 构建 | Succeeded，errors=0，169380238 bytes；`Artifacts/20261019T105034Z-e08989-build-macos/` |
| 新产物 | `Builds/macOS/20261019T105034Z-e08989/NatureCalls.app`；`file` 核实 arm64，Info.plist 游戏版本 `0.1.0-alpha.1` |
| 仓库检查 | 通过：82 个文件、18 个 GUID；Markdown 相对文件链接、忽略规则、路径大小写与包约束均通过，`git diff --check` 通过 |
| 脚本单测 | 17/17（其中 Semantic Diff 10 项）；与 Unity 测试独立 |
| 资源保全 | Smoke/Sandbox 与基线字节相同；18 个既有资产 GUID 保留，Facility 目录继承旧 GUID；Prefab 数量为 0，不声称 Prefab 行为验收 |
| PDF | 与基线字节相同，SHA256 `f186bf564e9afa4a05c94234f72d323424126c8136788a9ab8822a33b47bb5a5` |
| Git/LFS | `git fsck --full` 无损坏（有未引用 blob，未清理）；LFS fsck OK，无 LFS 资产；最大文件 PDF 165983 bytes，无需大文件迁移 |
| 语义同步 | 完成 fetch、读取首次缺失公共 Glossary 状态，明确确认公共 SHA `e43122a22c61623e57930c34932912bbcdca4a9a`；本任务读取完整新树 |

Windows Unity、LAN、Mac app 人工启动及发布签名未执行。[Semantic contract](https://github.com/HPZ07-end/NatureCalls/actions/runs/39240893119) 已在 GitHub 真实成功；[Repository checks](https://github.com/HPZ07-end/NatureCalls/actions/runs/39240892922) 的 Linux、macOS、Windows 三项均成功，验证提交为 `f6dbd8ea5614a567f6309111aa73de39948b4abc`。后续 PR head 仍必须通过相同检查。

本地 CLI 对真实 `origin/dev` / HEAD 与 PR 正文的 Semantic Diff 检查通过（baseline）。这些检查不代表 Windows Unity/LAN 验收，也不代表已获得人类 Review。

## GitHub 实际强制状态与管理员操作

已观察：仓库公开，当前账号 WRITE；rulesets 返回空列表；默认分支修改请求 HTTP 404。在上述四项 CI 成功后，分别对 main/dev 提交保护配置（PR、1 approval、撤销过期批准、管理员不绕过、禁止 force/delete、实际成功的 required checks），两个 PUT 请求均返回 **HTTP 404**。随后读取 branches，main/dev 的 `protected` 均为 **false**。所以服务端保护、required checks 和 required review **均未生效**；文档约定和已运行 Actions 不能替代它们。

用户确认 TMM 是显示名称，准确 GitHub 登录名待补充。当前不写未经核实的 CODEOWNERS 规则，未向误匹配账号发出 Review 请求，也不冒充已完成 Review。获得准确登录名后先核实 Write 权限，再经 PR 配置并请求人类审批。

管理员后续操作（只使用本仓库真实成功的检查名）：

1. Settings → General → Default branch 改为 main，并验证 API 返回 main。随后按前述安全条件清理旧分支。
2. Settings → Branches 添加 main、dev 保护（或等价 active ruleset），要求 PR、至少 1 名其他成员批准、dismiss stale approvals；禁止 force push 和删除，禁止绕过（包括管理员）。日常不允许直推。
3. CI 真正成功后才选 `semantic-diff` 和 `repository (ubuntu-latest)`、`repository (macos-latest)`、`repository (windows-latest)` 为 required checks，并要求更新到最新基线。不要添加不存在的 Unity CI。
4. 团队指定真实语义负责人账号后，PR 更新 `.github/CODEOWNERS`；再启用 Require review from Code Owners。语义审批仍需明确文字引用，不能仅凭普通批准勾选认定。
5. 核实 main/dev API `protected: true`、保护字段与 required checks 均匹配；保存响应作为证据。若套餐/权限不支持，记录未生效并执行人工 PR 门禁，不能声称服务器强制执行。

合并前仍需一名其他成员 Review。本次不自行合并，Tag 仍为候选，见 [RELEASES](RELEASES.md)。

## 最终文档与目录职责

```text
NatureCalls/
├── README.md                 人类入口与导航
├── GLOSSARY.md               唯一规范性语义树
├── AGENTS.md                 Agent 强制规则与导航
├── .github/
│   ├── CODEOWNERS            负责人权限待配置
│   ├── PULL_REQUEST_TEMPLATE.md
│   └── workflows/{check,semantic}.yml
├── Assets/
│   ├── Core/                 既有空程序集
│   ├── Player/               占位
│   ├── Facility/             占位，保留原 Environment GUID
│   ├── UI/                   占位
│   ├── Scenes/{Smoke,Sandbox}.unity
│   ├── Editor/               Unity 自动化
│   └── Tests/{EditMode,PlayMode}/
├── Packages/                 清单及锁文件
├── ProjectSettings/          Unity 版本与共享设置
├── scripts/{unity,check,semantic}.py
├── scripts/tests/            脚本测试
└── docs/
    ├── CONTRIBUTING.md       Git、Review、语义记录与同步
    ├── IMPLEMENTATION.md     实现约束
    ├── TESTING.md            测试命令及验收边界
    ├── ARCHITECTURE.md       技术职责和依赖
    ├── ROADMAP.md            Stage 1/2/3 及交付路线
    ├── DEVELOPMENT_STATUS.md 实际进度
    ├── RELEASES.md           版本、Commit、发布流程
    ├── development.md        Mac/Windows 安装和入门
    ├── C3.md / C3.pdf        历史设计（非规范）
    ├── setup-2026-10-19.md   历史环境验收
    └── governance-2026-10-19.md
```

目录树省略 `.meta` 与其他根配置；它们仍完整追踪。Artifacts、Builds、Library 等本机生成目录不提交。
