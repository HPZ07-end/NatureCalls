# 换装漂浮文字 UI

2026-11-25。用户要求参考《艾迪芬奇家族的记忆》的漂浮文字表现，去除换装面板大背景框，体型和颜色选项前使用方框，选中显示勾选。仅调整换装 UI 表现，预览、确认、取消及联机行为不变。

开始前已 fetch，当前 HEAD `fbf02bbe1ee6aaf24cc5c68986d7f18511c07e96`；公共 `origin/dev` 仍为 `cf537f70fe32a9452b6e32e9b3442c209d9f814b`，无新的 Glossary 变化，已确认。保留当前未提交改动，没有提交或合并。

草稿包括世界空间文字 Canvas、镜旁定位、逐行淡入、体型／颜色勾选行及纯文字确认取消。原 UI 与涉及文件备份于本机忽略目录 `.utmp/floating-outfit-ui-backup`。

Unity API 更新 Prefab 首轮失败：`Artifacts/20261125T121553Z-6e1f82-dressing-room-ui`，退出码 1。获取 Canvas 的通用辅助函数使用 C# 空合并，未处理 Unity 对象的特殊 null 行为，导致 MissingComponentException；已改为 Unity 对象 null 比较，但尚未重试。失败发生在保存 Prefab 前，原 UI Prefab SHA-256 与备份相同。

退出后没有检测到 Unity 进程，但 `Temp/UnityLockfile` 残留。根据项目规则不自动删除锁、不强制退出 Editor；已按用户要求停下，等待用户确认未保存内容和进程状态并手工清理。没有执行后续 UI 点击测试、截图验收或完整回归，不声明 UI 已更新。

为保持旧 UI 可用，已把本轮 LobbyController 与 PlayMode 测试草稿保存到忽略目录 `.utmp/floating-outfit-ui-draft`，并将这两个文件恢复到本轮开始时的备份。其他新 UI 组件尚未附到运行场景中。恢复工作时先核对上述草稿与当前文件，再完成生成及测试；新辅助函数修复也需要实际 Unity 验证。

Semantic-Diff: none

## 继续实现

用户回复「继续」后，已确认 Unity 进程和两种锁均不存在。核对暂停期间当前文件与备份一致后，恢复 Controller 和测试草稿。公共语义 SHA 未变化，已重新 fetch 和确认。

Unity API 更新已成功：`Artifacts/20261125T152708Z-d16543-dressing-room-ui`，编译和资源引用验证通过，原 LobbyCanvas prefab GUID 保持。此前停止段落为失败过程记录，当前已恢复实现。

换装内容使用独立世界空间 Canvas，在当前镜子旁显示朝向玩家的暖白文字，逐行淡入并轻微浮动；移除原 OutfitPanel 的 Image 背景和选项按钮底色。体型、颜色两组分别显示方框和选中勾；颜色保留小色样。选中状态由原 LobbyController 的预览索引驱动，确认与取消沿用原逻辑。新组件为 FloatingOutfitMenu、OutfitChoice；生成工具为 FloatingOutfitSetup，入口 `NatureCalls/Restyle Dressing Room UI` 或 `scripts/unity.py dressing-room-ui`。没有更改 Glossary、角色属性、大厅联机或地图流程。

已查看当前工程的 `floating-outfit-ui.png` 截图，文字、勾选、颜色样本及镜面倒影正常。新增 PlayMode 用例通过 EventSystem 实际射线命中可见选项并触发鼠标点击，检查体型／颜色各自单选、取消恢复与确认保存。

最终验证（Windows / Unity 6000.3.26f1 / Win64 Player）：

- EditMode 34/34 通过：`Artifacts/20261125T153236Z-8ae982-test-editmode`。
- PlayMode 48 项中 47 Passed、1 Relay Ignored：`Artifacts/20261125T153004Z-293dab-test-playmode`；包含新 UI 鼠标命中、确认取消，以及现有换装间、地图、Host 和回房回归。UI 截图同目录 `floating-outfit-ui.png`。
- Python 脚本单测 31/31 通过；仓库检查通过（1504 文件、757 个资产 GUID）；`git diff --check` 通过。UI 更新不改变 Glossary，Semantic Diff 为 none。
- 未执行真实跨设备 LAN／Relay 或正式构建；未提交、推送或合并。
