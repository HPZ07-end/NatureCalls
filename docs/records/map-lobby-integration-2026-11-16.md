# 已有模块地图接入大厅

基线：`origin/dev` / `8d3800759478f461b887912093b989d3e262dabf`；Unity 6000.3.26f1；Windows。

## 范围与授权

复用现有 `MapLayoutPrototype`，通过已有 `Playground`、`MapCatalog`、`Match`、`LobbyRoom`、`Avatar`、`Facilities` 接入离线及联机流程。保留原 Playground，不新增包、运行时框架或玩法规则。场景修改由 Unity Editor API 完成，不修改模块库 Prefab；现有镜面 RenderTexture 的本地改动不纳入任务。

用户在本次会话确认实施上述方案，原文「实现这个，遇到问题停下来告诉我哦」；因 Unity 锁暂停后，用户关闭 Editor 并回复「关闭了，继续，有问题告诉我」。

交付授权：用户随后要求将源码及可运行游戏包「两者都上传」。源码提交到本功能分支；Windows Development 游戏包作为 GitHub 预览版 Release 附件交付，不提交 Builds/Artifacts，不改变正式游戏版本或合并公共分支。预览标签为 `preview/map-lobby-20261116`，绑定本功能分支提交；压缩包包含运行依赖，排除 `NatureCalls_BurstDebugInformation_DoNotShip` 调试资料。

## Semantic Diff

Semantic-Diff: changed

Added:
- none

Changed:
- NatureCalls/Content/Map/MapSelection

Removed:
- none

Reason:
- 将现有模块布局作为第二张可选地图，复用原有单人及联机进入流程；原 Playground 保留。

Human Approval:
- 本次会话用户确认上述地图接入方案（2026-11-16），原文及继续执行指示见本文「范围与授权」。

## 验证

- 已通过 Unity API 接线、编译和资源验证：`Artifacts/20261116T140736Z-9af20a-map-integration/summary.json`；退出码 0，`validate.ok` 存在。
- PlayMode：`Artifacts/20261116T141013Z-ac73e7-test-playmode/results.xml`，33 项，31 Passed、1 Failed、1 Relay Ignored，进程退出码 2。新地图的大厅离线/外观/第一人称、Host 网络玩家及设施、出生点及交互三项新增用例均 Passed。
- 首次失败：`DoorOpensBlurredLobbyAndOfflineTestLoadsTheChosenMapWithTheOutfit`，当时 `DressingRoomTests.cs:172`；新增 `Camera.allCamerasCount == 1` 断言实际得到 3。原 Playground 有镜面渲染相机，断言未区分渲染目标。当时已按用户要求停下；这不是三个玩家视角的证据。
- 用户随后明确授权修正方案（「实现这个方案，遇到问题停下来告诉我」）。大厅离线/Host 测试与 ServerProbe 客户端断言现在只统计 `isActiveAndEnabled && targetTexture == null` 的相机，要求仅一台且等于地图的 Output、跟随本地玩家；保留镜面相机。专用服务器仍要求没有任何启用的渲染相机。
- 修正后完整 PlayMode 通过：`Artifacts/20261116T141624Z-5be58c-test-playmode/results.xml`，33 项中 32 Passed、1 Relay Ignored，退出码 0。
- EditMode 30/30 通过：`Artifacts/20261116T141807Z-f6d3e2-test-editmode/results.xml`，退出码 0。
- Python 脚本单测 31/31 通过；最终仓库检查通过（1003 files / 502 asset GUIDs）。工作树 Glossary 与本文件 Semantic Diff 格式校验通过（changed；不是已提交 HEAD 的 PR 检查）。
- Windows x86_64 Development 构建通过：`Artifacts/20261116T141849Z-7b7871-build-windows/summary.json`，退出码 0、`build.ok` 存在，产物 `Builds/Windows/20261116T141849Z-7b7871/NatureCalls.exe` 与数据目录存在。
- 新地图三进程本机 UDP 通过：`py -3 scripts/server.py Builds/Windows/20261116T141849Z-7b7871/NatureCalls.exe --map-index 1`；`Artifacts/20261116T142051Z-e32b27-server/summary.json`。同一工作树构建的 Windows 服务器/领队/成员三端均退出码 0，报告 passed；验证准备→选图→玩家/设施生成、两端独立屏幕相机及本地绑定、门、卷纸拾取/丢掷、马桶占用/释放、污物及拖把共享、领队离开后接任、计时结束返回保留房间、最后成员离开后服务器空房继续等待。
- 镜面相机保持原状；这次断言修正本身没有新增游戏语义。全任务 Semantic Diff 为 changed，原因是已批准的第二张可选地图。
- 公网、Relay、跨设备和人工手感未执行；服务器与客户端需重建同一版本后才能使用新地图，未部署公网服务器。
