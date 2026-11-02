# Player 共享模型与接口约定

本文是当前阶段 Player 预制体、外观接口和 Git 集成的实现参考。规范语义仍以仓库根目录的 [`GLOSSARY.md`](../../GLOSSARY.md) 为准；本文只约束技术结构，不确认新的玩法机制。

## 当前结构

```text
Assets/Prefabs/Player.prefab
├─ CharacterController
├─ Movement
├─ Posture
├─ PlayerInput
├─ PlayerAppearance
├─ NetworkObject / NetworkTransform / Network.Avatar
└─ Body
   └─ 全场景共享的人物模型与 Renderer

Assets/Prefabs/ChangingRoomPlayer.prefab
└─ Player.prefab 的 Prefab Variant
   ├─ startControlled = true
   └─ PlayerOutfitController
```

`Player.prefab` 是当前阶段唯一的基础人物资产。它已经被 NGO 的网络预制体配置引用，因此保留路径、GUID 和网络组件，不在功能分支中复制或重建。更衣室通过 Variant 扩展本地换装能力，模型、碰撞体、移动和姿态仍继承自同一个基础 Prefab。

若以后确实需要把网络组件从基础层拆开，应另开公共架构分支，在更新 `DefaultNetworkPrefabs.asset` 和相关测试后再引入 `PlayerBase.prefab`。当前阶段不提前进行这项高风险迁移。

## 稳定对象接口

以下名称和引用属于跨场景契约：

- 根对象固定为 `Player`；更衣室场景中的实例可以有名称覆盖，但不能改变 Prefab 来源。
- 模型根节点固定为 `Body`。
- `Movement.Body` 必须引用 `Body`。
- `PlayerAppearance.Body` 必须引用同一个 `Body`。
- 场景交互通过 `Movement.SetControl(bool)` 控制玩家输入，不依赖具体本地或网络控制器。
- `PlayerOutfitController` 只管理预览、确认和取消，通过 `PlayerAppearance.ApplyOutfit` 更新表现。
- `Network.Avatar` 通过 `PlayerAppearance.ApplyIdentityColor` 更新联网身份颜色，不直接遍历 Renderer。

身份颜色和服装是两个概念。当前原型可以作用于相同 Renderer，但不得把 `Avatar.ColorIndex` 重命名或解释成服装编号；联网换装同步需要单独确认需求。

## 场景使用规则

- Multiplayer Playground 使用 `Assets/Prefabs/Player.prefab`。
- Changing Room 使用 `Assets/Prefabs/ChangingRoomPlayer.prefab`。
- 场景只保存 Prefab 实例、出生位置和场景专属交互，不手工创建另一套玩家模型。
- Scene、Prefab 和组件引用必须通过 Unity API 或 Editor 工具修改，不手工编辑 Unity YAML。

统一生成工具：

```text
NatureCalls/Player/Migrate Shared Contract
NatureCalls/Changing Room/Build Scene
```

自动化入口：

```text
NatureCalls.Editor.PlayerContractMigration.Run
NatureCalls.Editor.PlayerContractMigration.BuildChangingRoom
```

## 文件所有权

公共 Player 契约负责人维护：

```text
Assets/Prefabs/Player.prefab
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

更衣室模块维护：

```text
Assets/Prefabs/ChangingRoomPlayer.prefab
Assets/Player/PlayerOutfitController.cs
Assets/Facility/ChangingRoom/
Assets/Facility/ChangingRoomPlatform.cs
Assets/Scenes/ChangingRoom.unity
```

Lobby 模块维护：

```text
Assets/UI/Lobby/
Assets/UI/LobbyUI.unity
```

修改公共 Player 契约前应通知其他功能负责人，并单独提交，不与大量 Scene 或 UI 修改混在同一提交中。

## 标准术语与兼容迁移

采用：

- `Player`：共享玩家控制与实体技术模块。
- `Movement`：统一移动和控制开关。
- `PlayerAppearance`：统一人物外观表现入口。
- `PlayerOutfitController`：更衣室换装流程连接组件。
- `Facility`：厕所、设备及更衣室交互所在的规范模块名称。

移除：

- `SimplePlayerController`：由 `Movement` 替代。
- `NatureCalls.Environment` 和 `Assets/Environment`：由规范术语 `Facility` 替代。
- 更衣室场景中手工创建的 Capsule 玩家：由 `ChangingRoomPlayer.prefab` 替代。
- Player、Facility、UI 的功能级 `.asmdef`：当前 Stage 1 继续使用 `NatureCalls.Runtime`，避免提前拆分程序集。

## Git 工作流

功能开发使用以下顺序：

```text
main（稳定版本）
  └─ dev（团队已集成并验证的开发版本）
       ├─ feat/multiplayer-playground
       ├─ feat/ui-lobby-flow
       └─ feat/Dressing-Room
```

1. 功能分支开始和集成前先 fetch。
2. 公共基础功能先合入 `dev`。
3. 已推送的功能分支使用 `git merge origin/dev` 同步，不强制 rebase 或改写历史。
4. 解决 `.asmdef`、Packages 和 Build Settings 冲突时保留双方真实依赖。
5. Unity GUI 保存并关闭后再运行 batchmode 验证。
6. 功能验证通过后推送原功能分支并通过 PR 合入 `dev`。
7. `dev` 达到阶段目标并完成团队验证后再合入 `main`。

禁止直接覆盖其他人的 `feat/*` 分支，也禁止为了消除冲突而重新生成已有资产 GUID。

## 验证指标

每次修改 Player 公共接口至少确认：

- `Player.prefab` 和 `ChangingRoomPlayer.prefab` 无 Missing Script 或丢失引用。
- Changing Room 场景中的 Player 是共享 Prefab 的实例。
- `Movement.Body` 与 `PlayerAppearance.Body` 指向同一个模型根节点。
- 更衣室进入和退出换装时分别调用 `Movement.SetControl(false/true)`。
- Network Avatar 仍只为 Owner 启用输入，并通过 `PlayerAppearance` 应用身份颜色。
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

## Semantic Diff

`none`。本次只统一 Player 技术接口、资产复用和模块术语，不新增或确认换装、联网换装、角色数量等玩法语义。
