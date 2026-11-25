# 联机马桶实体冲水按钮

2026-11-26。用户澄清为「实体冲水按钮，沿用 E，不新增键位」，替换此前可点击的冲水字幕。当前工作 HEAD `fbf02bbe1ee6aaf24cc5c68986d7f18511c07e96`；公共 dev 仍为 `cf537f70fe32a9452b6e32e9b3442c209d9f814b`，已 fetch、检查语义未变化并确认。保留工作区已有未提交改动，涉及文件备份在忽略目录 `.utmp/physical-flush-backup`。

通过 Unity API 删除场景中的 Flush Confirm UI 对象，创建两套 LobbyFlushButton 实体底座、带 Collider 的金色按头和按压回弹表现。移除 ToiletLobby 的 Flush Button 字段及 onClick 接线，使用 FlushButtons 引用实际模型。新对象没有鼠标点击事件。

沿用 PlayerInput 现有 E InputAction 和 TryInteract：俯视时角色暂停移动，但 E 可以经当前鼠标射线命中本隔间实体按钮。输入框正在编辑时不触发；输入完用输入框原有 Enter 提交流程结束编辑，再指向实体按钮按 E。没有新增键盘绑定。镜头过渡中、按头回弹中、错误目标、输入框编辑和连接进行中均拒绝重复触发。

实际连接继续使用原 ToiletLobby.Submit → LobbyController.SubmitConnection → CreateRoom／JoinRoom；准备、领队开始、地图选择、网络传输均不变。场景路径与 GUID 保持，俯视构图扩展到实体按钮可见。没有制作吸入或新转场。

生成和编译验证通过：`Artifacts/20261125T163332Z-23226d-toilet-lobby`。新测试通过屏幕投射的真实物理射线命中实体按头，验证输入编辑防误触、无字幕按钮和原 LAN 建房行为；不以该测试冒充人工键鼠手感或真实跨设备联机验收。

Windows / Unity 6000.3.26f1 / Win64 Player：PlayMode 全量 50 项中 49 Passed、1 Relay Ignored（`Artifacts/20261125T163426Z-bff455-test-playmode`），EditMode 34/34（`Artifacts/20261125T163740Z-2c7eaf-test-editmode`）；仓库检查通过（1517 文件、762 个资产 GUID）、工作区 Semantic Diff 校验 changed、`git diff --check` 通过。已查看运行截图，实体金色按头在俯视画面右上方可见，原冲水字幕不存在。按头为可替换几何占位外观，没有新增 Blender 资源。未提交、推送或合并。

Semantic-Diff: changed

Added:
- none

Changed:
- NatureCalls/Gameplay/UI/Lobby

Removed:
- none

Reason:
- 将联机确认由可点击字幕改为实体冲水按钮和既有 E 交互，不新增键位，保持原连接接口。

Human Approval:
- 用户在本会话要求物理意义上的冲水按钮而非点击字幕，澄清时明确选择「实体冲水按钮，沿用 E，不新增键位」。
