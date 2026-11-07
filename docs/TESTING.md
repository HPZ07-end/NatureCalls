# 测试与跨平台验证

Unity **6000.3.26f1**。Mac 使用 `python3`，Windows PowerShell 使用 `py -3`；Python 3.10+ 标准库即可。安装/路径说明见 [development](DEVELOPMENT.md)。

## 本地命令

先保存并关闭同一项目的 Unity GUI，再依次运行，不能并行启动这些 Editor 命令：

```sh
python3 scripts/unity.py detect
python3 scripts/unity.py validate
python3 scripts/unity.py test-editmode
python3 scripts/unity.py test-playmode
python3 scripts/unity.py build-macos
python3 scripts/check.py --require-generated
python3 -m unittest discover -s scripts/tests -v
```

`camera` 仅通过 Editor API 将已有 Playground 的镜头设为当前居中近景参数，不生成其他资产；参数和使用时机见 [PLAYGROUND](PLAYGROUND.md)。

`setup` 只用于显式初始化/修复配置，不是每人首次启动的必需步骤。`detect` 验证实际 Editor 路径与补丁版本；`validate` 导入、编译并调用 Editor 验证方法。可用 `--timeout 3600` 延长默认 1800 秒限制。

每次 Editor 命令生成独立 `Artifacts/<UTC时间-标识-命令>/`，保存 `editor.log`、`process.log`、`summary.json`；测试生成 `results.xml`。必须检查进程退出、完成回执和非空 XML，发现的测试全部 Passed 才能通过。不能将缺失结果、零测试、跳过或许可失败当通过。PlayMode/构建使用图形设备，需要可用桌面环境；不要给测试额外加 `-quit` 造成提前退出。

脚本发现 `Temp/UnityLockfile` 或 `Artifacts/unity.lock` 时拒绝运行；不自动删锁或强行退出 Editor。崩溃后由人确认进程退出与未保存内容后手工恢复。

## 当前覆盖范围

以下按测试方法计数（2026-11-03 实际运行，见 [开发记录](records/status-log.md)）。

- EditMode（13 项）：Force Text、Visible Meta Files、`Assets/Scenes` 下项目场景及所有 Prefab 的序列化引用/缺失脚本检查（第三方演示场景不在范围内）、DressingRoomPlayer 使用共享 Player 契约且不带网络组件、Lobby 资源接线（LobbyRoom 已注册、Network Prefab、启动场景与地图均在构建列表）；`OutfitSelection` 循环切换与取消恢复；`LobbyRules` 2–4 人与准备条件、`MapSelection` 随机地图；局域网房间号跳过 `169.254.x.x` 自分配地址；`RoundClock` 倒计时与显示格式；`NeedsRules` 急迫值 1 分钟涨满、如厕中不涨、隔间外 +10 恶心值与判负阈值。
- PlayMode（17 项，其中 1 项需要设置环境变量才运行）：DressingRoom 镜子换装改变外观并记住；门打开虚化 Lobby、单人离线测试选地图后加载 Playground 并穿着外观；Host 建房后单人不能开始、Lobby 中不生成角色、选图后加载地图并同步外观，全局计时在 HUD 显示，房主驱动玩家急迫值，任一玩家急迫值满后全队失败、所有人回到准备房并留在原房间、房间显示失败原因；单人离线对局到点后回到准备房；Smoke 加载；Playground 四个出生点都站在房间内空地上（脚下有地板、站立胶囊不碰撞、不在隔间内、与 Spawn 1 同一房间）；墙体碰撞/高帧率位移/姿态停步；站立模型脚底接触地图地面；Cinemachine 多帧 Blend、近距离正后方居中与朝向、马桶内部无遮挡、角色完整入镜与返回朝向；Host 所有权/唯一镜头/退出清理；Host 开门、拾取和丢掷卷纸后发布共享状态；空 Join Code 与未关联 Cloud 的失败反馈及离线练习保留。Playground 测试从场景读取墙体位置与隔间马桶，只统计渲染到屏幕的摄像机（镜面摄像机渲染到贴图）；马桶测试先从门外开门再进入隔间，验证：隔间外 C 原地如厕、再按 C 无效、5 秒后自动站起，E 不再坐下；每个隔间从门内到座位、左右两侧的位置都判定为本隔间，C 坐到本隔间的马桶并占用；坐着时 E 关门、上锁、开门；5 秒后自动站起并释放马桶。急迫值按 1 分钟涨满的速度上涨、如厕中不涨、结束后清零，隔间外如厕 +10 恶心值并留下一个黑色小球，隔间内不加。另有占用中的马桶拒绝第二名玩家，以及联机 Host 请求坐下后由房主授予并共享占用、5 秒后站起并释放。坐在马桶上时验证俯视镜头位于角色上方、看得到马桶开口且不被角色网格遮挡；站起后回到坐下前的位置并能走出隔间。Host 建房用例通过 `LobbyController.PreferLan` 固定走本机局域网房间，不依赖 Relay 和外网；Relay 建房的自动用例 `RelayHostConnectsOverWebSockets` 只在设置 `NATURECALLS_RELAY_TESTS=1` 时运行，用真实 Unity Cloud 以 WebSocket 建房并取得 Join Code，否则标记为 Ignored；两台机器经 Relay 联机仍需人工验收。2026-11-04 batchmode 全部通过。
- 仓库检查：路径大小写、JSON/asmdef、`.meta` 成对与 GUID 唯一性、生成目录忽略、包约束、相对 Markdown 文件链接；不请求外部链接，也不验证页面锚点。
- 脚本单测：并发锁、虚假测试成功、Semantic Diff 格式及 baseline 边界。
- 双 Player 验证：Development 构建通过 `scripts/multiplayer.py` 启动两个独立进程，在 localhost UnityTransport 上检查位置/朝向/姿态、所有权、独立镜头、可见远端角色、出生点与退出清理。开发探针只在显式 `-m1-probe` 参数下运行；普通游戏不自动操作角色，Release 不包含探针。
- 自动运动调用实际 Movement/Posture 能力，不等于人工键鼠手感验收。镜头截图只覆盖固定马桶 Anchor，未穷举任意墙角和 Inspector 配置。
- 未实现完整玩法规则，因此当前测试不是完整 Gameplay 验收。

## 双客户端与手工回归

构建后运行（路径以当次输出为准）：

```sh
python3 scripts/multiplayer.py Builds/macOS/<build-id>/NatureCalls.app
```

Windows 使用 `py -3 scripts/multiplayer.py Builds/Windows/NatureCalls.exe`，必须勾选 Development Build；Release 没有探针，不能用于此命令。脚本为两个自身创建的进程收集 JSON、PNG 和 Player 日志，超时失败会结束这两个进程，不操作 Editor。每端断言和退出码必须同时通过。测试使用短暂分配的本机 UDP 端口，不配置 Cloud。

双进程探针还依次由 Host、Client 发起门开/关/锁/解锁，以及卷纸拾取、丢掷；两端各自检查实际场景的门状态、卷纸携带者、Renderer/Collider 显隐及落点（位置差小于 0.15m）。同一运行目录内的标记文件只协调测试步骤，不写入游戏设施状态；实际交互走 Avatar 请求/RPC。测试会临时设置自己角色的位置，不代表人工键鼠路径或大厅/Relay 验收。

MPPM GUI、2–4 人、真实 LAN、Relay、Mac/Windows 互联分别人工记录。按 [PLAYGROUND](PLAYGROUND.md) 启动，逐端验证 WASD/鼠标/C/E/Esc，观察远端移动和蹲下，确认 A 的镜头变化不影响 B；大厅中验证准备/开始按键、人数不足时不能开始、选图与随机地图、对局中双方外观一致；Client 点左上角「退出」后 Host 只剩自己的角色，Host 退出后 Client 回到准备房并看到原因。再测试重新加入、游戏开始后加入被拒、无效地址或 Join Code、房间满员与端口不可达。未执行项不能因脚本通过而勾选。

## Semantic Diff

本地检查实际提交（未提交的 Glossary 变化不包含在 HEAD 检查中）：

```sh
python3 scripts/semantic.py check --base origin/dev --head HEAD --body-file Artifacts/pr-body.md
```

PR 正文从 GitHub 事件 JSON 读取，CI 使用实际 base/head，防止 shell 插值正文。`none` 不能隐藏任何 Glossary 文本变化，`baseline` 只能首次进入 dev；changed 的三个路径列表、Reason、Human Approval 必须填写。无 Glossary diff 的代码也可能有语义变化，必须人工检查。

同步工具及人工阅读/确认流程见 [CONTRIBUTING](CONTRIBUTING.md)。格式检查不能验证人类审批真实性；Review 必须核查所引讨论与实际代码。

## 构建与平台记录

macOS `build-macos` 使用 ARM64 Development `.app`，检查 BuildReport、成功回执及新产物，输出到 `Builds/macOS/<时间-标识>/NatureCalls.app`。这不是签名、公证或发布验收；构建成功也不等于人工启动通过。

Windows 可以在关闭同工程 GUI 后运行 `py -3 scripts/unity.py build-windows`，生成 Windows x86_64 Development Player，路径为 `Builds/Windows/<build-id>/NatureCalls.exe`，检查 BuildReport、完成回执及 Player 产物。也可在 File → Build Profiles 手动构建；需要的模块通过 Hub 安装。Windows 不运行 `build-macos`。

每次验收记录 Commit、OS/架构、Editor、用例数、产物、日志位置、结果。Mac/Windows 尽量测试同一 SHA；Network 修改记录 Host OS/SHA、Client OS/SHA、连接和状态同步的真实结果。没运行就明确写未验证。

Actions 的 `repository` 在 Linux/macOS/Windows 运行仓库检查与 Python 单测；`semantic-diff` 检查 PR 语义记录。没有 Unity runner/许可配置，绿色 Actions 不表示 Unity 跨平台通过。实际执行结果见 [治理记录](records/governance-2026-10-19.md) 与 [开发状态](DEVELOPMENT_STATUS.md)。

## 专用服务器改造回归（2026-11-07）

`python3 scripts/unity.py facilities` 通过 Unity API 生成/注册服务器拥有的 `Facilities.prefab`，并接线 Network Prefab 与 Playground。设施共享状态断言改读 `Facilities.Current`；玩家交互仍经 `FacilitySync`。新增 PlayMode 用例检查纯服务器不占房间成员名额、按进入顺序选领队、非领队服务器不能开始，以及继任者的准备状态不计入其他成员人数。此单进程用例不替代服务器与两个真实客户端的联机验收。

可用 `--test-filter` 向 Unity 传入明确测试方法名（多个名字以分号分隔）；实际参数记入 summary.json。不要用能匹配父套件的正则排除单个测试。仅允许本地网络的运行应明确选择本地方法，单列 Relay 未执行；未筛选运行出现 Ignored 时仍由严格检查器判为失败。
