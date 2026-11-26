# Map3 接入修复记录（2026-11-26）

## 做了什么

- `Map3Integration` 的结构网格改用非凸 `MeshCollider`，重复运行先替换旧碰撞体；为走廊/房间增加世界轴对齐的薄地板支撑碰撞，出生点从射线命中的地板高度加角色原点偏移校正。
- 天花板保持独立渲染器；`Portal` 支持标记本地判定轴，偏移由 `Room_R0.position - Room_R2.position` 计算，实际为 `(-40, 0, -8.8)`，反向门取反。
- 可走段只处理 `Segment_N`/`Segment_M`：42 个卷纸（含 `_Floor`）、42 个卷纸架、4 个工具箱、4 个拖把；按挂点复用并清理重复实例，工具箱与拖把关联。
- 门叶改为后代节点查找；镜像段开门方向反向，Ajar/PipeChase/Janitor/Empty 初始半开或打开，Engaged/OutOfOrder 锁死。马桶/Anchor/Flush target/Pipe、镜面最近选择和 Light_Flicker 实时点光接线。
- 扩展 Map3 PlayMode 覆盖道具数量、天花板/镜子/灯、禁用隔间厕所和四个出生点跑帧接地；增加 batchmode 截图入口。

## 根因与修法

- 原按 bounds 生成 BoxCollider 把“地板+墙”网格变成实心盒，玩家在盒内并穿透；改为网格碰撞并补地板支撑。
- 原出生点射线从高处首先命中天花板；改为排除天花板并取地板面，放置 CharacterController 原点。
- 原传送偏移把 Blender Z-up 的 Y 值写入 Unity Y；改为场景房间中心世界坐标差。
- 原纸卷跳过 `_Floor` 且每次运行都实例化；现全部可走段挂点处理并幂等。

## 验证

- `python3 scripts/unity.py map3-integration`：连续运行两次通过（最近两次日志目录：`Artifacts/20261126T093857Z-caba84-map3-integration/`、`Artifacts/20261126T094546Z-e4e8af-map3-integration/`）；第二次日志 `MAP3_SOCKET_COUNTS|paper=42|holder=42|toolbox=4|mop=4`、`MAP3_PORTAL_LAYOUT|r0=(0.00, 0.00, 0.00)|r2=(40.00, 0.00, 8.80)|offset=(-40.00, 0.00, -8.80)`。
- Map3 专项 PlayMode：7/7 Passed。
- 全量 EditMode：70/70 Passed。
- 全量 PlayMode：97 Passed，1 Ignored（既有 Relay 云测试）。
- `python3 scripts/check.py --require-generated`：通过（1722 files，867 asset GUIDs）。
- `python3 -m unittest discover -s scripts/tests -v`：41/41 Passed。
- `python3 scripts/unity.py validate`：通过。

## 截图

PNG 已写入 `Artifacts/Map3Check/`：`spawn_level.png`、`ceiling.png`、`stall.png`、`mirror.png`、`portal_before.png`、`portal_after.png`。截图入口为 `NatureCalls.Editor.Automation.Map3Screenshots`，batchmode 日志含 `MAP3_SCREENSHOTS_OK`。

## Semantic Diff

`none`。没有修改 GLOSSARY，也没有引入新的领域语义；`Portal`、`Engaged`、`OutOfOrder` 等沿用已批准术语。

## 文档与遗留

已更新 `docs/DEVELOPMENT_STATUS.md`。仓库将 `Artifacts/` 整体 gitignore；本地 `Artifacts/BackroomsRestroom/README.md` 已同步标注已解决 TODO，但它不属于 Git 提交内容。未执行真实双设备/Relay 联机、人工键鼠手感和跨平台 Windows 验收；这些不由 batchmode 专项测试替代。
