# Player 共享模型与接口约定

本文是当前阶段 Player 预制体、外观接口和 Git 集成的实现参考。规范语义仍以仓库根目录的 [`GLOSSARY.md`](../GLOSSARY.md) 为准；本文只约束技术结构，不确认新的玩法机制。

## 当前结构

```text
Assets/Prefabs/Characters/Player.prefab
├─ CharacterController
├─ Movement
├─ Posture
├─ PlayerInput
├─ PlayerAppearance（引用 Assets/Player/OutfitCatalog.asset）
├─ PlayerNeeds
├─ NetworkObject / NetworkTransform / Network.Avatar / AvatarAppearance / NeedsSync / FacilitySync（DressingRoomPlayer 变体中移除）
└─ Body
   └─ 全场景共享的人物模型与 Renderer

Assets/Prefabs/Characters/DressingRoomPlayer.prefab
└─ Player.prefab 的 Prefab Variant（原 ChangingRoomPlayer，GUID 保留）
   ├─ startControlled = true
   └─ PlayerOutfitController
```

`Player.prefab` 是当前阶段唯一的基础人物资产。它已经被 NGO 的网络预制体配置引用，因此保留路径、GUID 和网络组件，不在功能分支中复制或重建。DressingRoom 通过 Variant 扩展本地换装能力，模型、碰撞体、移动和姿态仍继承自同一个基础 Prefab。

若以后确实需要把网络组件从基础层拆开，应另开公共架构分支，在更新 `DefaultNetworkPrefabs.asset` 和相关测试后再引入 `PlayerBase.prefab`。当前阶段不提前进行这项高风险迁移。

## 稳定对象接口

以下名称和引用属于跨场景契约：

- 根对象固定为 `Player`；DressingRoom 中的实例可以有名称覆盖，但不能改变 Prefab 来源。
- 模型根节点固定为 `Body`。
- `Movement.Body` 必须引用 `Body`。
- `PlayerAppearance.Body` 必须引用同一个 `Body`。
- `Movement.Enter(Transform)` 保留玩家对位的位置；Anchor 识别设施，站起恢复进入前的位置。`Aiming` 供镜头读取，`Immobile` 暂停小便期间的位移和转向；瞄准/HUD 只读接口见 [PLAYGROUND](PLAYGROUND.md#表现层读取接口)。
- 场景交互通过 `Movement.SetControl(bool)` 控制玩家输入，不依赖具体本地或网络控制器。
- 外观以 `OutfitCatalog` 中的编号表示；`PlayerAppearance.ApplyOutfit(int)` 是唯一的外观表现入口。
- `PlayerOutfitController` 只管理预览、确认和取消；确认后写入 `LocalProfile.OutfitIndex`，供 Lobby 房间与之后的地图使用。
- `Network.AvatarAppearance.OutfitIndex`（Server-write）在联机对局中同步外观；为 -1 时（直接打开地图联机测试、没有 Lobby 房间）改用 `ColorIndex` 身份颜色。
- `Network.AvatarAppearance` 不直接遍历 Renderer，只调用 `PlayerAppearance`。

身份颜色和外观是两个概念：`ColorIndex` 是出生槽位颜色，`OutfitIndex` 是玩家选择的外观，不得互相改名或复用。当前两者作用于同一组 Renderer，有外观时显示外观。

替换美术：在 `OutfitCatalog.asset` 中增删条目或调整名称/颜色即可，换装面板按目录动态生成按钮；以后外观需要换材质或模型时，扩展 `OutfitCatalog.Outfit` 字段并只修改 `PlayerAppearance.ApplyOutfit`。

## 场景使用规则

- Multiplayer Playground 使用 `Assets/Prefabs/Characters/Player.prefab`。
- DressingRoom 的本地练习玩家使用 `Assets/Prefabs/Characters/DressingRoomPlayer.prefab`。
- 场景只保存 Prefab 实例、出生位置和场景专属交互，不手工创建另一套玩家模型。
- Scene、Prefab 和组件引用必须通过 Unity API 或 Editor 工具修改，不手工编辑 Unity YAML。

统一生成工具：菜单 `NatureCalls/Build Lobby Flow`，或关闭 GUI 后运行 `python3 scripts/unity.py lobby-flow`（入口 `NatureCalls.Editor.Automation.LobbyFlow`）。它为 `Player.prefab` 指定外观目录、维护 `DressingRoomPlayer` Variant，并重建 Lobby 相关资源与 DressingRoom 接线；已存在的外观/地图目录不会被覆盖。

## 文件所有权

公共 Player 契约负责人维护：

```text
Assets/Prefabs/Characters/Player.prefab
Assets/Player/Movement.cs
Assets/Player/Posture.cs
Assets/Player/PlayerAppearance.cs
```

多人模块维护：

```text
Assets/Network/
Assets/Input/
Assets/DefaultNetworkPrefabs.asset
```

DressingRoom 与换装维护：

```text
Assets/Prefabs/Characters/DressingRoomPlayer.prefab
Assets/Player/PlayerOutfitController.cs
Assets/Player/OutfitCatalog.cs / OutfitCatalog.asset
Assets/Player/LocalProfile.cs
Assets/Scenes/DressingRoom.unity
```

Lobby 模块维护：

```text
Assets/UI/Lobby/
Assets/Network/LobbyRoom.cs
Assets/Prefabs/Network/
Assets/Map/MapCatalog.cs / MapCatalog.asset、Assets/Map/Match.cs
Assets/Editor/LobbyFlowBuilder.cs
```

修改公共 Player 契约前应通知其他功能负责人，并单独提交，不与大量 Scene 或 UI 修改混在同一提交中。

## 标准术语与兼容迁移

采用：

- `Player`：共享玩家控制与实体技术模块。
- `Movement`：统一移动和控制开关。
- `PlayerAppearance`：统一人物外观表现入口。
- `PlayerOutfitController`：DressingRoom 换装流程连接组件。
- `OutfitCatalog`：可选外观；`LocalProfile`：本地已确认外观。
- `Facility`：厕所设备交互所在的规范模块名称。

移除：

- `SimplePlayerController`：由 `Movement` 替代。
- `NatureCalls.Environment` 和 `Assets/Environment`：由规范术语 `Facility` 替代。
- 更衣室场景中手工创建的 Capsule 玩家：由共享 Player 的 Variant 替代。
- ChangingRoom 场景、`ChangingRoomPlatform`、`ChangingRoomHud`、`ThirdPersonCamera`：由 DressingRoom 镜子换装与 `CameraController` 替代（2026-11-02）。
- `ChangingRoomPlayer.prefab`：更名为 `DressingRoomPlayer.prefab`（2026-11-02，GUID 保留）。
- Player、Facility、UI 的功能级 `.asmdef`：当前 Stage 1 继续使用 `NatureCalls.Runtime`，避免提前拆分程序集。

## Git 工作流

分支命名、同步与合并流程统一遵循 [CONTRIBUTING](CONTRIBUTING.md)。Player 相关的额外约定：

- 解决 `.asmdef`、Packages 和 Build Settings 冲突时保留双方真实依赖。
- 禁止直接覆盖其他人的 `feat/*` 分支，也禁止为了消除冲突而重新生成已有资产 GUID。

## 验证指标

每次修改 Player 公共接口至少确认：

- `Player.prefab` 和 `DressingRoomPlayer.prefab` 无 Missing Script 或丢失引用，`PlayerAppearance` 已引用外观目录。
- DressingRoom 中的练习玩家来自共享 Prefab 的 Variant。
- `Movement.Body` 与 `PlayerAppearance.Body` 指向同一个模型根节点。
- 打开和关闭换装面板、Lobby 时分别调用 `Movement.SetControl(false/true)`。
- Network Avatar 仍只为 Owner 启用输入；外观或身份颜色由 AvatarAppearance 通过 `PlayerAppearance` 应用。
- 仓库检查、脚本单测、Unity validate、EditMode 和 PlayMode 测试分别记录真实结果。

Windows 验证命令：

```powershell
py -3 scripts/check.py --require-generated
py -3 -m unittest discover -s scripts/tests -v
py -3 scripts/unity.py validate
py -3 scripts/unity.py test-editmode
py -3 scripts/unity.py test-playmode
```

若 `py -3` 未登记已安装解释器，可以使用明确的 Python 可执行文件运行相同脚本；报告中必须记录实际使用的解释器。Unity 版本固定为 `6000.3.26f1`。
