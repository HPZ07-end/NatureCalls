# 联机房四项修复与 Blender 吊牌

工作基线 HEAD `fbf02bbe1ee6aaf24cc5c68986d7f18511c07e96`，公共 dev `cf537f70fe32a9452b6e32e9b3442c209d9f814b`，已 fetch、核对 Glossary 未变化并 ack。保留当前未提交改动，备份在 `.utmp/lobby-four-fixes-backup`。未提交或合并。

## Blender 资源

实际检查原 `Signs-OutOfService.blend`：只有 Plaque、Inset Face、警示三角与感叹号，没有绳子。按用户新增授权，通过 Blender 4.5.0 新建 `Signs-ConnectionHanging.blend`（旁边的 NatureCalls-Models 源目录），不是覆盖旧资源。包含木牌、圆角、两根弯曲细绳和正面 UV。

导入 Unity 的文件：`Assets/Art/Models/Signs/Signs-ConnectionHanging.fbx`，以及 `Assets/Art/Textures/Signs/Signs-Connection-LAN.png`、`Signs-Connection-Relay.png`。同一模型用两套材质变体；文字为「本地局域网连接」及「远程房间号连接」。贴图由本项目脚本使用中文字体栅格化，未分发字体文件，无新增第三方美术素材。Pillow 仅为美术生成工具依赖，不进入 Unity 运行时。

可复现脚本：`scripts/art/hanging_sign_textures.py`（可通过 --font 指定字体），`hanging_sign_blender.py`（Blender 后台运行，保护已存在源与导出文件不被覆盖）。README 的模型清单已同步至 77 件。原 .blend、现有门及材质均保留。

## 运行时

- 移除联机房原悬空墙面 LAN／Relay 字，保留文字仅在进入本房后启用、返回换装间后隐藏。新吊牌使用有深度遮挡的普通网格／材质，无通过墙面显示的字体平面。
- 使用已有 Facilities-StallDoor 模型和 StallDoor 交互组件，补齐门叶、碰撞铰链、正面侧板，支持既有 E 开、关及内侧上锁。吊牌随门叶转动。关闭门时，隔间外看向马桶不会触发穿门俯视。
- 水面文字放大、疏排；建房和加入分列，输入集中于水面中部；等待区成员改为两列，移除重复身体／颜色文字但保留网络外观状态；选图待确认只显示所选地图与冲水提示。增加轻微上下漂浮。
- ChooseMap 保留公共名称，行为改为保存待确认地图。网络新增同步 SelectedMap 和 LobbyPhase.ConfirmingMap，保持旧阶段枚举值；ConfirmMap／ConfirmMapSelection 为明确的确认入口。只有领队和符合原人数／准备规则的房间可提交；人数或成员变化取消待确认。随机在选中时确定，冲水不重复抽取。
- 离线同样先保存选择，再经实体按钮 E 调用确认后才 Match.Begin。原 CreateRoom、JoinRoom、PressPrimary、连接与加载接口保持；ServerProbe 已适配两阶段调用，但本轮未运行专用服务器多进程验收。

场景、门、材质、引用均通过 Unity API 修改。原 DressingRoom 场景路径与 GUID 保持。首轮生成、导入和编译验证通过：`Artifacts/20261125T170112Z-d6bb80-toilet-lobby`。

## 首轮暂停记录（后续已恢复）

首轮完整 PlayMode：51 项，45 Passed、5 Failed、1 Relay Ignored（`Artifacts/20261125T170310Z-ca8c92-test-playmode`）。按用户「遇到问题停下来」要求暂停，没有继续修补后重跑，不声明四项均已完成。

已定位的测试前置条件冲突：4 个 HostMap 用例仍通过直接写 Phase 绕过两人要求，新 ConfirmMapRpc 会再次校验原人数和准备规则，故拒绝加载；Map2SuppliesAndShaftAreReachableFromSpawn 在点击选图后没有执行新确认，就访问 Map2Interactions，因仍在准备房而空引用。应适配测试流程和准备数据，不能放松服务器校验。尚未进行该适配和复验。

新门与吊牌资源引用、门可开启、房外文字关闭、普通离线选择停留后实体按钮确认等已有首轮覆盖，但仍需补闭门正面截图，以确认中文贴图朝向；原截图拍在开门后。已查看水面新排版截图，字号和布局确已变化。地图待确认页面还需要检查并去掉底层大厅标题／状态的重复显示。未执行最终 EditMode、完整仓库检查、双客户端或真实 Relay 验收。

Blender 源已重新打开核对，模型包含 Hanging Board、Sign Lettering、Left Cord、Right Cord；两张变体文字贴图均已打包到源中。没有覆盖旧故障牌。`git diff --check` 通过。当前无 Unity 进程或残留锁，未提交／合并。

## 继续实现与最终验证

继续记录：用户再次指示继续后，适配了上述 5 项旧测试。HostMap 使用准备完成的第二成员数据，通过正常 RequestStart 和确认校验后再移除没有传输端的测试成员；不修改服务器门槛，也不把单进程测试当真实双客户端验收。Map2 补给测试增加显式确认。新增准备条件被撤销后服务器拒绝确认的用例。

第二轮 PlayMode 为 50 Passed、1 Failed、1 Relay Ignored（`Artifacts/20261125T171150Z-1b4639-test-playmode`）；原 5 项加载前置问题均已消除，剩余 Map2 Host 的旧急迫值断言在地图操作后仍按初始上限判断。该断言已按实际回合经过时间和现有增长速率计算允许上限，未改变游戏规则。闭门截图已核对两块中文牌子朝向正确，另通过 Unity API 加入门前照明提高可读性（`Artifacts/20261125T171725Z-02e71d-toilet-lobby`）。选图页面隐藏底层大厅标题和状态，待确认截图覆盖各地图。

最终完整 PlayMode：52 项中 51 Passed、1 Relay Ignored（`Artifacts/20261125T171846Z-a237e9-test-playmode`）。最后地图短名称与确认排版调整后，模块厕所专项 1/1 通过（`Artifacts/20261125T172158Z-eefca8-test-playmode`）；实际显示「模块厕所」，不显示 MapLayoutPrototype 等程序后缀，确认提示分行留白。EditMode 34/34 通过（`Artifacts/20261125T172330Z-141165-test-editmode`）。仓库检查通过（1538 文件、771 个资产 GUID），工作区 Semantic Diff 校验 changed、`git diff --check` 通过；场景 meta 与原备份字节相同。

已查看最终闭门牌子、房外不显示文字、水面菜单、四地图待确认截图，以及模块厕所最终排版截图。确认两块中文贴图朝向正确、门前照明可读、底层大厅文字不与选图页叠加；选图不会加载，实体按钮再次确认后才进入地图。原始暂停与失败结果保留在上方作为过程记录，当前本轮实现和上述本机验收已完成。

没有执行实际双客户端、跨设备 LAN／Relay、专用服务器部署或正式构建；未提交、推送或合并。模拟第二名成员的 Host 用例仅验证服务器状态与本机加载，不能算多人网络验收。

## Semantic Diff

Semantic-Diff: changed

Added:
- none

Changed:
- NatureCalls/Gameplay/UI/Lobby
- NatureCalls/Content/Map/MapSelection

Removed:
- none

Reason:
- 按人类要求补隔间门、吊牌和水面排版，地图选择增加显式实体冲水确认，维持原人数、准备和领队权威校验。

Human Approval:
- 用户在本会话明确列出四项修改，随后授权既有 Blender 源不存在合适模型时新建带小绳、牌板、文字的模型，更新文档并导入 Unity 继续实施。
