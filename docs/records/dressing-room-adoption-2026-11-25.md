# 当前工程的换装间替换

日期：2026-11-25。当前工作区 HEAD 为 `fbf02bbe1ee6aaf24cc5c68986d7f18511c07e96`，分支 `feat/art-model-import`，保留原有未提交的角色、Map2 和大厅改动。来源为从 `origin/dev` `cf537f70fe32a9452b6e32e9b3442c209d9f814b` 建立的 `feat/dressing-room-rebuild` worktree。移植前已 fetch，并在当前工作区确认相同公共语义 SHA。没有提交、合并或改写分支历史。

用户明确要求让新房间完全覆盖当前工程的旧房间，获知新旧交互接口差异后确认采用「保留当前地图和角色改动、适配必要接口」的方案。

## 实现

先在本机忽略目录 `.utmp/dressing-room-adoption-backup` 保存旧场景、场景 meta、相关脚本和文档；`before-hashes.json` 记录移植前现有工程文件 SHA-256。备份不在 Assets 内，不会进入游戏或提交。

通过 Unity API 从空场景重建 `Assets/Scenes/DressingRoom.unity`；沿用 GUID `949d1634b40f24622b3011036bc37978` 和构建首场景。旧 `DressingRoomBlockout`、`DressingRoomProps` 和原房间实例由新布局替换，不保留游戏内旧入口。保留已有角色变体、外观确认取消、联机、选图和回房路径。内净尺寸约 5×4×2.8 m，含天花板、右侧双洗手台和镜子、动态镜面反射、可搬凳子、室内灯具及大厅门。

沿用标准名称 `DressingRoom`、`DressingMirror`、`LobbyDoor`、`PlayerOutfitController` 和 `LobbyController`；移除旧布局生成实现。新增技术组件 `DressingRoomStool`、`DressingRoomHints`。凳子使用可替换几何占位外观，没有新增 Blender 素材。换装沿用已有 Reach 动作，没有新增专用换装和搬运动画。实体双坑位大厅、水面 UI、冲水提交和卷入转场仍待后续实施。

当前分支尚未集成公共分支的 `InteractionRegistry`，因此新增局部 `DressingRoomInteraction`（标记 compat），只处理本房间本地玩家的 E 准星目标和 F 放凳子；现有地图继续原接口。替代项为公共分支统一交互接口，删除阶段为该输入迁移正式集成时。没有复制公共分支的全套 Input、Map2 或网络代码。

`PlanarMirror` 动态反射以实际屏幕相机关于镜面的对称位置取景，完整 UV 平面显示，以镜面四角构建投影。原 Playground 固定镜面保留 compat 分支，待其改用完整 UV 和 ViewerCamera 后删除。镜面、材质、凳子 prefab 和场景均由 Unity API 生成；没有手改序列化 YAML。

## 验证

Windows / Unity 6000.3.26f1 / Win64 Player。当前工程场景生成与编译通过：`Artifacts/20261125T115802Z-5fca2b-dressing-room`；日志确认 1 个天花板、2 面镜子、1 张凳子和原 GUID。

- EditMode 34/34 通过：`Artifacts/20261125T115910Z-90e894-test-editmode`。
- PlayMode 47 项中 46 Passed、1 Relay Ignored：`Artifacts/20261125T120021Z-c17f20-test-playmode`。覆盖两镜三模型两高度的实际图像比较、完整 UV 色块、反射随观察位置变化、双镜外观确认取消、凳子搬放与真实跳跃落脚、已有离线／Host 地图进入和返回回归。抽查当前工程的房间全景、矮快型站凳照镜及高伸型地面照镜截图，倒影、天花板和大厅门牌均正常。有限镜框不做自动人物缩放，高个子站凳时可能超出镜框。
- Python 脚本单测 31/31 通过；仓库检查通过（1497 文件、754 个资产 GUID）；`git diff --check` 通过。工作区 Semantic Diff 相对覆盖前 Glossary 校验为 changed。
- 覆盖前 1450 个现有文件的 SHA-256 核对通过，只有允许的换装间文件和文档发生变化；场景 meta 字节相同。原有地图、角色及其他未涉及文件保持不变。

未执行双设备 LAN、真实 Relay、正式构建和真人键鼠手感验收。

## Semantic Diff

Semantic-Diff: changed

Added:
- none

Changed:
- NatureCalls/Content/Map/DressingRoom

Removed:
- none

Reason:
- 将已批准的双镜、有天花板、可搬凳子的本地换装间覆盖到当前工程，沿用启动和回房入口；只在准备房接入 E/F 操作。

Human Approval:
- 用户在本会话批准新换装间布局和交互，明确要求「我希望将新房间完全覆盖我当前旧房间」，并在获知兼容问题和保留地图角色改动的移植方案后回复「好的」。
