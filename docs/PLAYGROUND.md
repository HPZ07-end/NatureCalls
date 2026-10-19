# M1 Multiplayer Playground

本次语义授权：2026-10-19 用户「Multiplayer Playground Initialization」，见根 [GLOSSARY](../GLOSSARY.md)。本文件描述技术实现与验证方式，不重新定义游戏规则。工程仍用 Unity **6000.3.26f1**，游戏版本不提升（`0.1.0-alpha.1`）。

## 环境与依赖

| Package | 固定版本 |
| --- | --- |
| Universal RP | 17.3.0（随当前 Editor 提供） |
| Input System | 1.20.1 |
| Netcode for GameObjects | 2.13.3 |
| Multiplayer Services | 2.4.1 |
| Cinemachine | 3.1.7 |
| Multiplayer Play Mode | 2.0.2 |

完整传递依赖以 `Packages/packages-lock.json` 为准。选择依据为 Unity Registry 的稳定版本、最低 Editor 版本和本机实际包解析；NGO 3.x 要求更新的 Editor，因此没有升级。全部使用官方包，不实现替代传输或摄像机框架。

## 打开和重建

打开 `Assets/Scenes/Playground.unity`，Play。点击 UI 右侧游戏画面锁定鼠标，WASD 移动、鼠标观察、C 切换姿态、靠近测试马桶后 E 对齐、Esc 释放鼠标。已有 Smoke/Sandbox 场景保留。

`python3 scripts/unity.py playground`（Windows：`py -3 scripts/unity.py playground`）通过 Editor API **重建原型 Playground、Player/Toilet Prefab 和原型材质**，只在明确需要恢复生成场景时运行；不要用它覆盖人工修改的原型。普通队友使用已提交场景即可，不需要补接组件。脚本保存原 GUID、不改旧场景，构建列表将 Playground 放在第一位。

## 组件边界

- `Player/Posture`：可供交互系统调用的姿态切换与通知；不依赖摄像机。
- `Player/Movement`：CharacterController、角色朝向、俯仰、姿态下的身体与碰撞体调整。
- `Input/PlayerInput`：Input System 将本地键鼠转化为移动、观察、姿态与交互调用。
- `Camera/CameraController`：唯一实际 Camera + CinemachineBrain，两个 CinemachineCamera 的优先级和 Blend；仅绑定本地角色。
- `Facility/Toilet`：距离检测、姿态 Anchor 与内部可见目标；没有正式设施占用或冲水状态。
- `Map/Playground`：原型资源引用、4 个出生位置与离线练习。
- `Editor/Playground`：用 Unity API 创建场景、材质、URP 设置和 Prefab。

保留原逻辑目录，不建立同义的 Networking/Environment 体系。新增一个 `NatureCalls.Runtime` 总程序集是为了让 Editor 和测试能引用实际组件；没有逐模块拆程序集或提前提取 Core。

## 镜头

第一人称使用眼部目标、Hard Lock To Target 和 Rotate With Follow Target。第三人称使用独立肩部目标与 Cinemachine Third Person Follow，内置障碍物处理只检测世界层。原型玩家在 Ignore Raycast 层，墙壁/设备仍有物理碰撞。

两台 Cinemachine Camera 只是虚拟镜头，每个进程只有一个实际 Camera/AudioListener。切换使用 EaseInOut Blend；离开的目标保持原姿势，避免 E 对齐或站起时瞬间反转。第一人称本地 Body Renderer 隐藏，第三人称移出身体后恢复；不修改共享资产、物理墙体或其他玩家渲染状态。

Inspector 参数位于场景 `Local camera`：Eye Height、Third Person Distance/Height/Pitch、Shoulder Offset、Transition Duration、Camera Collision Layers/Radius。当前没有墙体全局隐藏。C 可在任意位置测试姿态；要验证马桶内部构图，请走到马桶附近按 E。

## 验证状态

尚在实现中。最终实测日志、截图、联机覆盖和人工配置步骤将在本任务验收后补充。单机编译、自动测试、本地直连、Relay 与真实 Mac/Windows 互联必须分别记录，不能互相替代。

## 官方依据

- [Sessions + NGO](https://docs.unity.com/en-us/mps-sdk/tutorials/build-your-first-session)
- [Casual co-op quickstart](https://docs.unity.com/en-us/multiplayer/quickstarts/casual-co-op-quickstart)
- [Relay + NGO](https://docs.unity.com/en-us/mps-sdk/tutorials/relay-and-ngo)
- [Create session](https://docs.unity.com/en-us/mps-sdk/create-session)
- [Cinemachine Third Person Follow](https://docs.unity.cn/Packages/com.unity.cinemachine%403.1/manual/CinemachineThirdPersonFollow.html)

实现 API 还直接核对了本次安装包的源代码，避免使用旧版 Cinemachine 2 或 NGO 旧继承式权限示例。
