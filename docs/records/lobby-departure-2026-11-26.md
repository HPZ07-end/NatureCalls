# 准备房空间、退出交互与马桶吸入转场

工作 HEAD `fbf02bbe1ee6aaf24cc5c68986d7f18511c07e96`；公共 `origin/dev` 为 `cf537f70fe32a9452b6e32e9b3442c209d9f814b`。开始任务已 fetch，公共 Glossary 相对前次确认点无变化；已核对并 ack 同一 SHA。保留当前分支及全部既有改动，修改前备份位于忽略目录 `.utmp/lobby-departure-backup`。未提交、推送或合并。

## 实现

- 双隔间联机准备房纵深由 4.4 m 扩至 6.4 m，入口向后移动 2 m，保留两个隔间及吊牌，延长墙、地板与天花板并补入口照明。原换装区域布局、DressingRoom 路径和 GUID 保持。
- 关闭水面界面或 Esc 可以中断打开动画、退出连接后的房间界面并恢复行走与探索镜头；连接保留，离开联机房间仍使用原「离开房间」。关闭后须先移开视线再看向同一个马桶才重开，避免连续自动俯视造成卡住。返回探索时恢复鼠标捕获。
- 通过 Blender 后台只读检查 `Facilities-Toilet.blend`，确认原模型包含 `Flush Button` 网格。移除后来生成的 `Physical Flush Button` 及其金色按头；在原网格上接入射线碰撞和按压回弹，没有新增可见按钮模型。仍使用既有 E，输入编辑时拒绝误触。
- 最终选图后按 E 确认，先隐藏水面菜单，播放约 2.5 秒的水流旋涡、人物缩小旋转卷入、镜头下潜和黑场；地图加载并绑定本地角色后约 0.45 秒淡入，恢复操作。首次冲水仍只提交连接。
- `ToiletDeparture` 为本地程序动画；`DepartureFade` 跨场景保留黑场并在镜头绑定后清理。没有新增 Blender 动画片段。动画使用玩家已确认的角色与颜色。
- `LobbyRoom` 追加 `Departing` 阶段，保留原枚举值。领队确认仍先通过原人数和准备校验，各客户端完成本地动画后经 RPC 报告，服务器等当前成员全部完成再调用原加载接口。纯服务器不播放动画；转场超时返回准备房，连接保留。既有 CreateRoom、JoinRoom、PressPrimary、ChooseMap、ConfirmMapSelection 与 Match.Begin 名称保持，无新增领域名称或 legacy 替代项。

场景、碰撞组件及引用通过 Unity API 更新，原 FBX 与 Blender 源未修改；旧金色按钮材质资产保留但不再用于该场景。

## 验证

Unity 6000.3.26f1、Windows x86_64 Player 目标。场景重建与编译通过：`Artifacts/20261125T174832Z-7ee53b-toilet-lobby`。

完整 PlayMode：53 项，52 Passed、1 Relay Ignored，日志与 XML 位于 `Artifacts/20261125T175056Z-90ed12-test-playmode`。新增用例覆盖入口距离、复用原模型按钮、打开动画中断、退出后不会连续重新俯视及恢复行走；已连接 LAN 房间关闭界面后连接保留。离线四地图检查先播放吸入、角色缩小，再加载并恢复本地操作；本机 Host 地图及回房流程通过。

EditMode 34/34 通过，位于 `Artifacts/20261125T175444Z-c7059e-test-editmode`。仓库检查通过（1542 文件、773 资产 GUID），`git diff --check` 通过。未执行真实双客户端 LAN、Relay、跨设备与跨平台验收，不将模拟成员的单进程 Host 用例算作多人网络通过。

## 视觉检查发现问题，按要求暂停

已查看入口、原生按钮、水面页面及人物吸入截图。吸入截图可见完整角色、马桶与旋涡；但 `toilet-lobby-lan.png` 的 1100 × 760 取景中，最下方状态文字被画面边缘裁切。新俯视镜头为了包含水箱原生按钮调整了取景，目前未同时完整容纳页面底部；自动回归没有检查这项视觉边界。按用户「遇到问题停下来告诉我」暂停，不声明四项已完整验收，也未继续改镜头或排版。

建议下一步调整俯视取景范围／中心并复验按钮射线与完整水面页面，然后再宣布完成。当前代码和生成场景已保留，README 记载的是已接入行为；本记录与开发状态明确保留视觉问题。

## Semantic Diff

Semantic-Diff: changed

Added:
- none

Changed:
- NatureCalls/Gameplay/UI/Lobby
- NatureCalls/Content/Map/MapSelection
- NatureCalls/Content/Map/DressingRoom

Removed:
- none

Reason:
- 按用户明确要求扩大入口空间、允许退出联网方式界面、复用原马桶按钮，并实现最终确认后的吸水、人物卷入与地图转场。

Human Approval:
- 本会话用户 2026-11-26「修改意见」四项指令，原文包括「制作确定后的吸水动画与人物被吸入的转场」，同时要求复用马桶上自带按钮并修复退出。
