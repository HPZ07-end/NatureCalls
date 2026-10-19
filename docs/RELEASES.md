# 版本与发布

游戏版本与 Unity Editor 版本是两个不同维度。Editor 固定 **6000.3.26f1**；游戏遵循 `MAJOR.MINOR.PATCH`，可带 `alpha`、`beta`、`rc` 预发布标记。Tag 格式 `v<游戏版本>`，不覆盖已有 Tag，main/dev 永久不改名。

| 状态 | 游戏版本 / Tag | Commit 与说明 |
| --- | --- | --- |
| Bootstrap（非发布） | Player Settings 原值 `1.0`，无 Tag | `e43122a22c61623e57930c34932912bbcdca4a9a`；main/dev 共用的原始完整基础设施基线 |
| 候选（待 Review/集成） | Player Settings `0.1.0-alpha.1`；拟用 `v0.1.0-alpha.1`，尚未创建 | 治理任务分支通过 Unity API 设置；精确提交见治理 PR 的版本修改 Commit |

没有给旧 main 的 `1.0` 配置打 `v0.1.0-alpha.1`，也不会把未 Review 的任务提交伪装成稳定发布。此候选表示基础设施 alpha，不表示可玩 Demo。

发布步骤：

1. 候选经其他成员 Review 和必要语义审批合入 dev，再由验收 PR 集成到 main。
2. 在拟发布 main 的确切 Commit 运行适用测试/构建，核对 Unity Player Settings `bundleVersion` 与 Tag 去掉 `v` 后完全一致；记录 Windows 等未覆盖项并由团队接受发布范围。
3. 检查 Tag 本地/远程均不存在，创建 annotated Tag（如 `git tag -a v0.1.0-alpha.1 <verified-sha>`），消息记录版本、SHA、验收与 PR 引用，正常推送该 Tag。
4. 用 `git rev-parse v0.1.0-alpha.1^{commit}` 和远程 peeled tag 核实对应关系，补入此表；后续变更走 PR。已有 Tag 不移动，修正使用新版本。

当前阻碍是治理 PR 尚需其他成员 Review、dev→main 集成及最终 main 验收。版本候选本身已与任务分支 Player Settings 同步；正式 Tag 留待满足条件后建立。
