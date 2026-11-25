# 双隔间联机方式房间

2026-11-25 至 2026-11-26，当前工程工作区。HEAD `fbf02bbe1ee6aaf24cc5c68986d7f18511c07e96`，公共 `origin/dev` `cf537f70fe32a9452b6e32e9b3442c209d9f814b`；开始前已 fetch，确认公共 Glossary 未变化并 ack。保留此前角色、地图、换装间和漂浮文字 UI 的未提交改动。本轮修改前备份位于本机忽略目录 `.utmp/toilet-lobby-backup`。没有提交、合并或改变分支历史。

## 实现

在原 DressingRoom 场景内增加有天花板的双隔间房间，原大厅门进入，返回门回到换装间。复用既有马桶模型，左为 LAN，右为 Relay。未新增正式地图或共享 Gameplay Toilet；连接选择马桶仅属于本地大厅表现。原场景路径与 GUID 保持。

近距离持续看向水面约 0.3 秒，正常视角在约 0.8 秒内平滑切到俯视，复用同一屏幕 Camera 并暂时停用 Cinemachine Brain；关闭界面反向平滑恢复。准备、房间号输入与选图使用水平放置在水面上的世界空间 Canvas。保留原 LobbyView 引用与公共接口 CreateRoom、JoinRoom、PressPrimary、ChooseMap、StartOffline；添加 SubmitConnection 只做入口类型校验后调用原连接方法，不替换网络实现。

选择「创建房间」或输入后选择「加入房间」只设置意图，点击「冲水 · 确认连接」才提交。LAN 只接受地址；Relay 沿用现有 Join Code，拒绝误填 IP，未关联 Cloud 时给出明确反馈。等待、失败消息、成员准备、人数限制、领队选图和原有地图加载沿用原流程。没有人物吸入、卷入动画或新的转场，现有选图加载行为保留。单人离线入口保留。

新增技术组件 ToiletLobby 和生成工具 ToiletLobbySetup；沿用 Lobby、LobbyController、LobbyView、DressingRoom、Session 等名称。移除本场景原大厅的虚化背景表现，替换为水面文字；旧 LobbyController 公共 API 保留，场景不带 ToiletLobby 的 legacy/compat 情形仍用旧背景，待旧场景入口全部迁移后可删除该表现分支。原房内交互适配器继续沿用，不引入新的网络架构。

生成入口 `NatureCalls/Build Toilet Lobby` 或 `scripts/unity.py toilet-lobby`。首次搭建使用 Unity API 创建场景内容与引用；再次运行只调整本工具管理的水面尺寸和 UI 底色，不覆盖房间布局。完整 DressingRoom 重建会同时搭建本房间。

## 验证

Windows / Unity 6000.3.26f1 / Win64 Player。首轮生成和编译验证通过：`Artifacts/20261125T155717Z-2fe6f4-toilet-lobby`。首轮 PlayMode 48 Passed、1 Failed、1 Relay Ignored；新增视线用例在进入保护时间刚结束时过早断言，改为有超时的实际触发等待。截图发现水面偏大、按钮底色残留，已通过 Unity API 调整水面到盆内并移除按钮和成员条背景。

调整后生成与验证通过：`Artifacts/20261125T160148Z-98aec6-toilet-lobby`。保存 Prefab 实例样式覆盖后，移除残留按钮底色；文字抬到盆沿上方避免遮挡。最终输入提示调整为暖白居中，水面缩入马桶内部；生成验证通过：`Artifacts/20261125T161509Z-4d9903-toilet-lobby`。

- 全量 PlayMode 50 项中 49 Passed、1 Relay Ignored：`Artifacts/20261125T161118Z-d95089-test-playmode`，含两模式视线、动画持续性、文字无按钮底色、EventSystem 射线点击、LAN 冲水建房、空输入拒绝和恢复返回，以及既有换装、地图和 Host 回归。
- EditMode 34/34：`Artifacts/20261125T161403Z-ad4340-test-editmode`。
- Python 脚本单测 31/31、仓库检查（1511 文件、760 个资产 GUID）、`git diff --check` 和相对覆盖前 Glossary 的 Semantic Diff 校验均通过。场景 meta 与备份字节相同。
- 最后水面与输入提示调整后的专项 PlayMode 2/2 通过：`Artifacts/20261125T161640Z-e43198-test-playmode`。已检查最终房间、LAN、Relay 和 LAN 等待界面截图；输入提示可读，文字与冲水入口不被盆沿遮挡，按钮和成员条无整块底色。回房选择模式使用原 Session.IsRelay 判断，兼容 UDP 与 WebSocket Relay；本轮不以传输协议判断联机类型。
- 未执行双设备 LAN、真实 Relay、Mac／Windows 互联或正式构建；Ignored 不计为 Relay 验收通过。

## Semantic Diff

Semantic-Diff: changed

Added:
- none

Changed:
- NatureCalls/Gameplay/UI/Lobby
- NatureCalls/Content/Map/DressingRoom

Removed:
- none

Reason:
- 将已批准的 LAN／Relay 双坑位入口落实为有隔间的房间、看向马桶的镜头动画、水面文字和冲水提交，保留原连接、准备、领队选图和地图接口。

Human Approval:
- 用户在本会话先确认「冲水提交连接，准备和选图完成后再被卷入地图」及沿用现有 Relay 房间号；随后明确要求按计划搭建两个隔间、看向马桶切换俯视、水面漂浮文字，先完成吸入与转场之前的交互，并保持此前接口。
