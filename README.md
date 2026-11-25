# NatureCalls

软件工程课程的厕所主题合作游戏。人类定义系统逻辑，Coding Agent 负责遵守语义的技术实现。共同依据是 [GLOSSARY.md](GLOSSARY.md)，人类可见的信息也是 Agent 必须读取的信息。

游戏从换装准备房（DressingRoom）启动：镜子前换装，左侧门进入联机大厅，可单人离线测试、创建或加入房间（2–4 人），开始后由房主选择地图。地图 Playground 提供第一人称移动、C 如厕（隔间内坐到该隔间马桶，隔间外原地如厕）、隔间门与卷纸交互、马桶占用、急迫值与恶心值、全体判负、全局对局计时（结束后回到准备房，联机时留在原房间），以及 NGO Host/Client 原型。仓库已关联 Unity Cloud 项目，大厅「创建房间」使用 Relay Join Code，加入时输入 IP 仍可局域网直连；Relay 真实联机尚待验收。正式玩法数值、冲水等设施规则和胜负判定尚未实现。实际验收范围见 [DEVELOPMENT_STATUS](docs/DEVELOPMENT_STATUS.md)。

## 打开工程

1. 安装 Unity Hub 和 **6000.3.26f1**，本人完成登录与许可。Mac/Windows 使用同一补丁版本。
2. 克隆仓库，切换团队的 `dev` 分支；在根目录运行 `git lfs install --local`、`git lfs pull`。
3. Hub → Add project from disk，选择包含 `Assets`、`Packages`、`ProjectSettings` 的根目录。
4. 打开 `Assets/Scenes/DressingRoom.unity`，点击 Play。镜子前 E 换装；左侧门 E 打开联机大厅，选「单人离线测试」再选地图即可进入。地图中 WASD / 鼠标操作，Space 跳跃、C 如厕（一次性，5 秒后自动站起；隔间外会留下黑色小球并加恶心值）、E 拾取卷纸或操作隔间门（坐在马桶上也能关门上锁）、R 丢掷卷纸、Esc 释放鼠标后点左上角「退出」返回准备房；顶部显示本局剩余时间，左上角显示自己的急迫值和恶心值，任何人任一项满了全队失败。
5. 双客户端：一端在大厅「创建房间」，另一端输入房主局域网地址（同机 `127.0.0.1`）「加入房间」；准备后房主开始并选择地图。两台电脑、校园网注意事项与 Relay 配置见 [Playground 指南](docs/PLAYGROUND.md)。

平台安装、VS Code 和命令行入口见 [development](docs/DEVELOPMENT.md)。

在准备房镜子前按 E 换装；可分别选择矮快、均衡、高伸三款角色模型和服装颜色。预览立即更新，确认后保存两项选择并用于离线地图和联机对局，取消会恢复上一次确认的外观。三款模型共享玩家操作与玩法能力。

地图中 E 只对准星指向的设施执行交互；视线未命中时按约 15° 窄锥选择，非第一人称镜头按距离和优先级选择。F 只作用于手持物品或自身，用于卷纸/徒手擦拭和拖把清洁；C、R、Space 不变。

## 工程概览

`Player` 管理角色及姿态，`Input` 接收本地操作，`Camera` 响应姿态，`Network` 管理 Session/角色同步，`Facility` 与 `Map` 提供测试环境与门、卷纸、镜子等交互对象，`UI` 提供地图内菜单与 DressingRoom 中的换装/大厅/选图界面。`Core` 是换装选择、房间开始条件与地图选择的纯 C# 规则，`Editor` 生成资产与构建，`Tests` 验证实际组件。目录不是玩法定义，逻辑概念树与 C# 类型不要求一一对应。详见 [ARCHITECTURE](docs/ARCHITECTURE.md)。

## 文档导航

- 共同语义：[GLOSSARY](GLOSSARY.md)；Agent 入口：[AGENTS](AGENTS.md)。
- 全部技术文档及放置规则：[docs 索引](docs/README.md)。
- 常用：[PLAYGROUND](docs/PLAYGROUND.md) · [ARCHITECTURE](docs/ARCHITECTURE.md) · [CONTRIBUTING](docs/CONTRIBUTING.md) · [TESTING](docs/TESTING.md) · [DEVELOPMENT_STATUS](docs/DEVELOPMENT_STATUS.md)。
- 历史资料：[C3 转录](docs/history/C3.md) 与 `docs/records/` 下的验收记录。历史设计不覆盖 Glossary。

## Blender 资产清单

清单涵盖原有三批、Foundation 风道、Map1 细化、Map2 下水道及油画风角色的 **76 件 Unity 模型**（含旧角色）。Blender 源文件位于项目旁的 `NatureCalls-Models` 目录。文件名中连字符前的部分是资产分类。模型采用米制，Foundation 风道以地图图纸标注的通行净空为准。

Unity 中的 FBX 位于 `Assets/Art/Models/<分类>/`，例如 `Assets/Art/Models/Tools/Tools-Mop.fbx`。导入器将模型材质槽映射到 `Assets/Art/Materials/` 的共享 URP 材质，两面镜子保留独立的镜面槽。模型保持米制和定位点，角色朝 Unity 的 `+Z`。业务 Prefab 的碰撞、交互组件和场景引用在接入时配置。

### 动画预览

- 三款油画风角色各有一个独立 Prefab，位于 `Assets/Art/Prefabs/Characters/`。每款使用自己的 15 骨 Generic 骨架和 `Idle`、`Walk`、`Run`、`Reach` 四段原地动画。预览时可在子物体 `Character Model` 的 Animator 将 `Speed` 改为 0、0.5 或 1，也可触发 `Reach`。这两个参数仅用于动画切换预览。

- 两款角色共享六个 Humanoid 片段：`Idle`、`Walk`、`Jump`、`SquatDown`、`SquatHold`、`StandUp`。动画在原地播放，绑定和动作源文件另存在 `NatureCalls-Models/Rigged/`。
- [动画预览场景](Assets/Art/AnimationPreviews/AnimationPreview.unity) 包含两款角色、隔间门、准备房门和水闸。在 Unity 打开场景并进入 Play Mode，选中对应实例，在 Animator 的 Parameters 中试用角色的 `Speed`、`Grounded`、`Squatting`，门的 `Open`、`ReverseSwing`（隔间门另有 `Locked`），或水闸的 `Turned`。调方向前先关门，试门闩时保持门关闭。
- 动画片段和控制器位于 `Assets/Art/Animations/`。预览场景未加入构建列表，预览参数尚未绑定游戏输入、碰撞或联机状态。卷纸和拖把沿用现有交互和刚体，不增加物件轨迹动画。

### 卫浴设施 · Facilities

- **马桶（Toilet）** · `Facilities-Toilet.blend`：放在隔间内。独立座圈、抬起的盖板和便盆内壁让俯视镜头能看清开口；可与马桶内壁污渍叠放。
- **小便池（Urinal）** · `Facilities-Urinal.blend`：沿公共厕所墙面安装。带内槽、排水口和冲水件，内槽用作第一人称瞄准区的视觉参考。
- **隔间门（Stall Door）** · `Facilities-StallDoor.blend`：安装在隔间门框内。门扇原点位于左侧铰链轴，可供 Unity 设置开关旋转；带把手和门闩。
- **洗手池（Sink）** · `Facilities-Sink.blend`：用于厕所及准备房的洗手区。带盆体、水龙头和排水口，盆沿离地约 0.865 m。
- **洗手池镜（Sink Mirror）** · `Facilities-SinkMirror.blend`：安装在洗手池上方。镜框与镜面可分别设置材质，镜面可接 Unity 反射。
- **准备房全身镜（Dressing Mirror）** · `Facilities-DressingMirror.blend`：用于换装准备房，供玩家观察全身外观；镜面独立，便于配置反射。
- **皂液器（Soap Dispenser）** · `Facilities-SoapDispenser.blend`：挂在洗手池附近作为布景，通过液位窗、按压板和喷嘴区分轮廓。
- **烘手机（Hand Dryer）** · `Facilities-HandDryer.blend`：挂在洗手区附近作为布景，带出风口、感应区和指示灯外观。
- **垃圾桶（Waste Bin）** · `Facilities-WasteBin.blend`：放在洗手区或入口墙边，带开放桶口和独立桶沿。
- **应急区占位设施（Emergency Fixture）** · `Facilities-EmergencyFixture.blend`：摆放在模块地图的应急区遮挡板之间，用低矮陶瓷盆体和排水口标示原型位置。
- **Map1 细化隔间门（Civic Stall Door）** · `Facilities-CivicStallDoor.blend`：用嵌板、踢脚、木饰封边和暖色五金增加门扇细节，以左侧铰链点与门框对接。
- **售纸机（Paper Vending Machine）** · `Facilities-PaperVendingMachine.blend`：补给角的售纸机模型。投币、存货显示和出纸位置各有独立分件或定位点。

### 场景结构 · Structure

- **地板模块（Floor Tile）** · `Structure-FloorTile.blend`：用 1 m × 1 m 单元铺设厕所、走廊和准备房。原点在模块角点，行走表面与地面基准对齐。
- **直墙模块（Wall）** · `Structure-Wall.blend`：用带踢脚线的直墙围合房间和走廊，按 1 m 宽度拼接。
- **瓷砖墙段（Tiled Wall）** · `Structure-TiledWall.blend`：用于洗手区、小便池区和维护区，可与直墙在同一网格上拼接。
- **隔间侧板（Stall Partition）** · `Structure-StallPartition.blend`：同一块模型可旋转后用作隔间左右侧板，底部留空并由支脚支撑。
- **隔间前窄板（Stall Front Panel）** · `Structure-StallFrontPanel.blend`：填补门框立柱与侧板之间的窄缝，连成完整的隔间正面。
- **隔间后墙（Stall Back Wall）** · `Structure-StallBackWall.blend`：作为马桶背后的独立墙段，与侧板围成隔间。
- **隔间支脚（Stall Foot）** · `Structure-StallFoot.blend`：安装在侧板或前窄板下方。底座贴地，上方卡座托住隔板。
- **隔间门框（Stall Door Frame）** · `Structure-StallDoorFrame.blend`：用两根立柱和横梁承接隔间门，按第一批门扇尺寸留出边缘间隙。
- **小便池挡板（Urinal Divider）** · `Structure-UrinalDivider.blend`：固定在相邻小便池之间的墙面，遮挡侧面视线。
- **准备房入口门（Dressing Room Door）** · `Structure-DressingDoor.blend`：用作准备房通往联机大厅入口的门扇外观；带铰链、双侧把手和踢板。
- **准备房门框（Dressing Room Door Frame）** · `Structure-DressingDoorFrame.blend`：与准备房入口门配套。立柱和门头围成约 0.96 m 宽、2.18 m 高的净开口。
- **应急区遮挡板（Privacy Screen）** · `Structure-PrivacyScreen.blend`：在应急区两侧或后方重复摆放，形成约 2.09 m 高的布景遮挡。
- **Map1 分色墙（Civic Wall）** · `Structure-CivicWall.blend`：米制直墙模块，两面都有釉面砖墙裙、砖缝、踢脚和木压条。
- **Map1 瓷砖墙（Civic Tiled Wall）** · `Structure-CivicTiledWall.blend`：用于厕所外墙内侧和公共墙段，顶部加木竖纹，墙裙加深绿色釉面砖和暖色嵌条。
- **Map1 隔间板（Civic Stall Partition）** · `Structure-CivicStallPartition.blend`：用于隔间侧板和正面填板，包含嵌板、撑脚、上下压条和两侧嵌线。
- **Map1 隔间门框（Civic Stall Door Frame）** · `Structure-CivicStallDoorFrame.blend`：用双柱、护脚、门楣和嵌条收口，与细化门扇共用左侧铰链基准。
- **Map1 入口挡板（Civic Privacy Screen）** · `Structure-CivicPrivacyScreen.blend`：在厕所拐角遮挡视线，下半部为实体和砖裙，顶部用竖向木格栅。
- **井盖（Manhole）** · `Structure-Manhole.blend`：A/B 井口共用的齐平井框，净开口 1.22 m，盖板使用独立铰链。
- **下水道竖井（Sewer Shaft）** · `Structure-SewerShaft.blend`：按 1 m 纵向拼接的井壁段，内侧有下滑导轨和上下定位点。
- **下水道短梯（Sewer Ladder）** · `Structure-SewerLadder.blend`：B 井返程用 2.70 m 短梯，梯身、顶部扶手及翻出定位点独立。
- **主排水渠（Sewer Channel）** · `Structure-SewerChannel.blend`：长 2.50 m 的直渠段，可按定位点拼接，独立水面用于水流表现。
- **检修栈道（Sewer Walkway）** · `Structure-SewerWalkway.blend`：包含宽 2 m 的 2.40 m 平段和 3.40 m 坡段，坡段升高 1.10 m，两个子节点按需选用。
- **检修短阶（Sewer Step）** · `Structure-SewerStep.blend`：连接分流平台和下沉槽。四级台阶，每级高 0.15 m，总高差 0.60 m。
- **渠边护栏（Sewer Guardrail）** · `Structure-SewerGuardrail.blend`：含 1.50 m 直段和短端头，可沿栈道与平台边拼接。
- **破损地砖（Broken Floor Tile）** · `Structure-BrokenFloorTile.blend`：含 1 m 裂纹砖、翘砖两个可单独选用的变体。
- **维修围挡（Repair Barrier）** · `Structure-RepairBarrier.blend`：用条纹横板和独立撑脚标记破损或施工地段。

### 用品 · Supplies

- **卷纸（Paper Roll）** · `Supplies-PaperRoll.blend`：用于可拾取卷纸，也可摆在纸架、卷纸柜上作陈列；纸芯为贯通孔。
- **壁挂卷纸架（Paper Roll Holder）** · `Supplies-PaperRollHolder.blend`：固定在隔间内墙，转轴上可放置同一卷纸模型；带轴心定位点。
- **清洁剂瓶（Cleaner Bottle）** · `Supplies-CleanerBottle.blend`：摆在工具架或清洁区台面上，作为清洁用品布景。
- **金币（Coin）** · `Supplies-Coin.blend`：售纸资源的视觉模型，带压印标志及滚花边缘。

### 工具 · Tools

- **拖把（Mop）** · `Tools-Mop.blend`：用于取放、携带和清洁的视觉模型。高 1.36 m，杆、握把和拖把头分开，并留有握把定位点。
- **工具箱（Toolbox）** · `Tools-Toolbox.blend`：作为拖把取放点的外观。箱体带提手和锁扣，背后卡槽可作为摆放拖把的视觉参考。
- **水桶（Bucket）** · `Tools-Bucket.blend`：摆在清洁区或工具架底层，与拖把配套陈列；带敞口桶身和提手。
- **扳手（Wrench）** · `Tools-Wrench.blend`：挂在工具架上或放在维护台面，作为维修区布景；有可见的开口钳头。
- **手摇疏通器（Drain Auger）** · `Tools-DrainAuger.blend`：在检修口使用的工具外观，卷筒、摇柄和伸缩软轴分件。

### 收纳 · Storage

- **置物架（Shelf）** · `Storage-Shelf.blend`：在厕所公共区域陈列卷纸或其他道具，亦可作为普通开放货架复用。
- **卷纸柜（Paper Cabinet）** · `Storage-PaperCabinet.blend`：用于模块地图的纸品补给区。开放层板带陈列定位点，可重复摆放卷纸。
- **放置台（Drop Counter）** · `Storage-DropCounter.blend`：用在模块地图的放置点或转角。约 0.92 m 高的台面带物件摆放定位点。
- **工具架（Tool Rack）** · `Storage-ToolRack.blend`：用于工具及清洁区，提供层板和挂点，可分别摆放清洁剂瓶、水桶和扳手。

### 维护布景 · Systems

- **直水管（Straight Pipe）** · `Systems-StraightPipe.blend`：沿维护区墙面铺设管路。管段长 0.60 m，外径 60 mm，两端有对位点。
- **水管弯头（Pipe Elbow）** · `Systems-PipeElbow.blend`：让同规格管道转向 90°，以两端定位点对接横管和竖管。
- **水管接头（Pipe Coupler）** · `Systems-PipeCoupler.blend`：套在两段 60 mm 管道的接缝处，表现管路连接结构。
- **水闸（Water Valve）** · `Systems-WaterValve.blend`：安装于同规格管路中，表现维护区的阀门位置；手轮带独立旋转轴。
- **通风格栅（Vent Unit）** · `Systems-VentUnit.blend`：安装在墙面，使用外框和斜百叶表现通风口。
- **可通行风口框（Vent Access Frame）** · `Systems-VentAccessFrame.blend`：用于地上 A、B 两处风道入口。净开口 1.20 m × 1.05 m，原点在门槛底面中心，格栅有独立铰链且默认打开。
- **斜风道（Vent Slope）** · `Systems-VentSlope.blend`：连接两个不同标高的风道口。单段水平投影 2.85 m，下降 1.30 m；净宽 1.20 m、垂直净高 1.05 m。底板、侧壁和顶板独立。
- **风道会车段（Vent Passing Bay）** · `Systems-VentPassingBay.blend`：设于两段斜风道之间，提供 1.80 m × 1.80 m 的水平空间，两端收至居中的 1.20 m 开口。
- **地下出口收口（Vent Exit Transition）** · `Systems-VentExitTransition.blend`：连接风道与地下维修层，底面与地下地坪齐平。出口外预留 2 m × 2 m 的站立空间，可用现有地板模块铺设。
- **维修设备外壳（Maintenance Housing）** · `Systems-MaintenanceHousing.blend`：放在地下环路中央，遮挡跨区视线。检修面朝向阀门工位，两处管件定位点用于对接现有管路。
- **管道检修口（Pipe Inspection Port）** · `Systems-PipeInspectionPort.blend`：含可打开的检修盖、铰链、连通的管腔和软轴入口定位点。
- **管道观察窗（Pipe Sight Glass）** · `Systems-PipeSightGlass.blend`：用于上游和下游状态观察，透明窗、内水面和水位定位点独立。与检修口使用同一 0.45 m 管身外径。

Foundation 每条风道按风口框、斜段、会车段、斜段、地下出口的顺序拼装。两段斜坡加会车段的水平投影共 7.50 m，累计落差 2.60 m。A、B 可共用同一套模型，B 的整体朝向旋转 180°。导入 Unity 后，斜段和会车段沿局部 `+X` 拼接，上下对应局部 `Y`，以 `Inlet`、`Outlet` 定位点对齐。

### 灯具与导视 · Fixtures

- **壁灯（Wall Light）** · `Fixtures-WallLight.blend`：安装在镜子上方或墙面，带独立灯罩材质；场景照明可在 Unity 中配置。
- **厕所标识牌（Restroom Sign）** · `Fixtures-RestroomSign.blend`：用于入口或公共区域导视，用几何图案表示厕所。

### 警示标记 · Signs

- **隔间故障牌（Out of Service Sign）** · `Signs-OutOfService.blend`：挂在故障隔间门上或墙边，用三角感叹号标示布景状态。
- **警示条纹（Warning Stripe）** · `Signs-WarningStripe.blend`：贴在故障隔间门、隔板或维护区墙面，可重复摆放。

### 污物与地面脏污 · Mess

- **黑色小球（Dropping）** · `Mess-Dropping.blend`：用于现有黑色小球占位表现，放在地面以便玩家识别清洁目标。
- **积水（Puddle）** · `Mess-Puddle.blend`：贴地放置的不规则薄片，用于现有积水表现。
- **马桶内壁污渍（Dirty Toilet Stain）** · `Mess-DirtyToilet.blend`：与马桶共用原点，叠放后贴合便盆内壁，用于显示脏马桶状态。
- **地面脏污（Floor Grime）** · `Mess-FloorGrime.blend`：贴在地砖表面，为脏污隔间和维护区增加静态磨痕；此处的 `Mess` 是文件分类。

### 角色 · Characters

旧男性和女性源模型及绑定版本位于 `NatureCalls-Models/Archive/PreviousCharacters/`，Unity 清单继续记录现有对局所引用的对应模型。

- **矮快型（Swift）** · `Characters-Swift.blend`：设计身高 1.45 m，芥末黄短夹克和短促的步频。
- **均衡型（Balanced）** · `Characters-Balanced.blend`：设计身高 1.65 m，茄紫工装和中等步频。
- **高伸型（Tall）** · `Characters-Tall.blend`：设计身高 1.88 m，橄榄绿长外套和较长的步态周期。

- **男性角色（Male Character）** · `Characters-Male.blend`：高 1.80 m 的 A 字站姿模型，可作为对局及准备房的男性玩家外观；上衣保留独立换色材质。
- **女性角色（Female Character）** · `Characters-Female.blend`：高 1.74 m 的 A 字站姿模型，可作为对局及准备房的女性玩家外观；上衣保留独立换色材质。
## 联机入口

大厅提供「创建房间」（Unity Relay 或局域网直连）和「创建服务器房间」。服务器房间由 broker 按需分配随机三位房间号（100–999），加入时输入房间号即可；也可继续输入 `IP:端口` 直连或输入 Relay Join Code。端口用途固定为：UDP 8000 常驻房间、UDP 8001–8099 按需房间、TCP 8100 房间 broker、UDP 47777 局域网发现信标。动态房间空闲 300 秒后退出。
