# 协作与语义契约

人类定义逻辑概念、性质、关系并批准语义变化；Agent 负责符合定义的实现、架构、测试、进度与技术文档。双方共同读取 [Glossary](../GLOSSARY.md)。技术文档不得另建语义权威。

## 分支与提交

- 永久分支：`main`（稳定/发布基线）、`dev`（日常集成）。版本使用 Tag，不改永久分支名称。
- 普通任务从最新 `dev` 创建，PR 合回 `dev`。`main` 只接收通过验收的集成版本或发布修复；hotfix 从 main 派生，经 PR 回 main 后再 PR 同步 dev。
- 临时分支采用小写 kebab-case：`feat/<module>-<task>`、`fix/<module>-<issue>`、`test/<target>`、`chore/<task>`、`hotfix/<issue>`、`experiment/<task>`。例如 `feat/player-movement`、`fix/facility-overflow`、`chore/repository-governance`。按任务命名，不按人员；禁止 `dev/<task>`。
- Commit 使用 `<type>(<scope>): <summary>`；type 为 feat、fix、refactor、test、docs、chore、build、ci、perf，scope 优先标准模块名小写。例：`docs(glossary): update accepted semantics`。一个提交一个明确目标。
- 禁止 force push、危险 reset、丢弃未提交工作或覆盖他人提交。临时分支合并并验证安全后删除。旧分支删除前必须确认提交可从长期分支到达、远程同步且有备份。
- 初始 main/dev 建立是一次性 Bootstrap。之后包括文档在内的日常修改都走 PR，不能以 Bootstrap 为借口继续直推。

## Semantic Diff Protocol

每个进入 dev/main 的 PR 必须有且只有一个声明。无语义变化也要检查代码是否引入新领域含义，再明确写：

```text
Semantic-Diff: none
```

新增/删除/重命名概念、改变定义、状态性质、逻辑关系、游戏规则或不变量，都属于语义变化。纯技术重构、性能优化、内部实现变化不一定属于。需要修改 Glossary 时，**先获得人类批准**；未确定内容只在 PR/讨论中作为 Proposed Change，不能写进权威树。

```text
Semantic-Diff: changed

Added:
- NatureCalls/Gameplay/Facility/<approved concept>

Changed:
- none

Removed:
- none

Reason:
- 说明为什么改变。

Human Approval:
- 人类讨论或审批的 URL / 可追溯引用。
```

路径使用以 NatureCalls 开头的规范概念路径；无条目用 `- none`，重命名列出 Removed/Added，关系与性质变化列 Changed。三个变更列表不能全为空；Reason 和 Human Approval 不接受模板占位。即使只调整 Glossary 文本也用 changed 记录影响路径并解释是否仅编辑性修改，不能标 none。

首次新建 Glossary 可以使用 `Semantic-Diff: baseline`，必须写 Reason、Human Approval；只允许基线不存在且此前历史也未建立过 Glossary 的首次引入。本次来源是用户「Repository & Semantic Governance v1」明确提供的文本。后续将该已批准基线从 dev 发布到尚无 Glossary 的 main 时，应使用 changed 及完整树路径并引用首次基线 PR，不重复使用 baseline。

实际 Glossary 文本变化必须与记录对应。自动检查不判断规则的真实含义，也不证明某个审批引用有效；Reviewer 必须核对代码、文档、路径和审批，不能让格式合格代替人类决策。语义冲突须报告，Agent 不得自行选一种解释。

## PR、Review 与集成

使用 [PR 模板](../.github/PULL_REQUEST_TEMPLATE.md) 记录目的、范围、Semantic Diff、公共接口、资产、测试、平台/LAN、风险和未完成事项。至少一名**其他成员** Review；语义变化还必须由人类负责人明确作语义审批，普通代码 Review 不自动等同于语义审批。

负责人尚未有效配置，见 [CODEOWNERS](../.github/CODEOWNERS) 和 [权限记录](governance-2026-10-19.md)。团队确认真实账号并核实具有 Write 权限后再写有效规则；不能虚构团队账号，也不能将作者自己当作其他 Reviewer。

合并前 fetch 最新基线，复查语义、冲突和当前 PR head 的 CI。发生新提交时重新确认 Review 是否仍有效。采用 merge commit 或 squash，合并 Commit 正文必须保留：

```text
Semantic-Diff: none|changed|baseline
PR: https://github.com/HPZ07-end/NatureCalls/pull/<number>
```

有变化时还保留对应概念路径及审批引用摘要；完整表在 PR 历史中。不要依赖 GitHub 默认标题自动携带语义。合并者编辑正文并在合并后用 `git show` 核实 PR 引用与 Semantic-Diff；不能用普通 rebase 合并丢失记录。只有授权集成且人类 Review 满足时才能合并；禁止绕过检查。任务期间的临时提交不必每条附完整语义表。

## Agent 同步

开始任务及准备集成时都执行：

1. 确认工作区状态，`git fetch origin --prune`，记录任务 HEAD 与公共分支 Commit。
2. `python3 scripts/semantic.py sync --branch dev`：读取本地上次确认点，显示它到最新 origin/dev 的 Glossary Diff；main 集成时改为 main。命令自己也 fetch。
3. 有变化则重读受影响子树，检查实现是否依赖变化定义；无可靠记录（包括历史不连续）则重读完整 Glossary。工具输出不能代替语义理解。
4. 有冲突立即报告并请求人类决策；不得静默覆盖其他 Agent 已确认语义。
5. 阅读并检查后，以命令打印的完整 SHA 执行 `python3 scripts/semantic.py sync --branch dev --ack <sha>`。只确认仍是该 SHA 的远程版本；若远程已变化，拒绝确认并重新读取。

记录保存在 Git 的本地工作区元数据中，不提交、不共享。多个 Agent 必须使用各自 worktree；不共用确认记录。无工具时同样可以手动记录 Commit 并用 `git diff <old> <new> -- GLOSSARY.md`。Git 是语义传播底座，不引入后台服务。

## Unity 与跨平台

统一 Editor 补丁版本；Assets 及 `.meta`、Packages 及 lock、ProjectSettings 一起提交。Force Text、Visible Meta Files 必须开启。场景/Prefab 共享修改先协调，资产移动用 Unity API 保留 GUID，不能手拼 YAML。

忽略 Library、Temp、Logs、UserSettings、Build/Builds、Artifacts 等生成目录。构建通过 CI artifact/发布附件分发，不直接提交。大型二进制按实际素材选 LFS，不把所有 Unity 文本文件放 LFS；禁止未经批准重写大文件历史。第三方资源记录许可证，不提交密钥或本机授权。

Mac/Windows 尽可能验证同一 Commit，记录平台与 SHA。Network 修改必须标明 Host/Client 组合和验证状态；未执行写未执行，不能把脚本单测或 LAN 规划写成 Unity/LAN 通过。详见 [TESTING](TESTING.md)。

## 强制执行的边界

| 层次 | 能保证什么 |
| --- | --- |
| 文档约定 | 人类/Agent 协作纪律；自身不能阻止 Git 操作 |
| 本地 `scripts/semantic.py check` | 格式、Glossary 差异、初始 baseline 限制；可被本地跳过 |
| Actions `semantic-diff` | 对 PR 的真实 base/head 和最新正文执行同一检查；不验证人类批准真实性 |
| GitHub 服务端保护 | 管理员启用后才阻止直推/无 Review 合并/失败检查等 |

当前实际启用状态及管理员设置步骤见 [治理记录](governance-2026-10-19.md)。仅在 CI 已真实成功后，才选择其精确 job 名为 required check；不要设置尚不存在的 Unity CI。Review 还应审查 workflow/检查脚本本身的修改，不能让 PR 修改检查器来规避规范。
