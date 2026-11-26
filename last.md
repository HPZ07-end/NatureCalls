# 如厕重做记录（2026-11-26）

已修改：`Assets/Input/PlayerInput.cs`、`Assets/Input/ToiletingView.cs`、`Assets/Camera/CameraController.cs`、`Assets/Core/NeedsRules.cs`、`Assets/Player/PlayerNeeds.cs`、`Assets/Facility/StoolOutput.cs`、`Assets/Facility/Toilet.cs`、`Assets/Facility/ToiletFlushButton.cs`、`Assets/Editor/PlumbingIntegration.cs`、`Assets/UI/ToiletingHud.cs`。

新流程：C 在马桶或隔间外进入第一人称 `Squatting`；鼠标下压到低头阈值进入 `Aiming`，再次按 C 确认，Space 站起退出。V 在马桶或小便池进入第一人称 `Urinating`，C 确认，Esc 退出；小便池按 C 不启动。瞄准期间需求增长使用 `NeedsRules.AimingUrgencyRate = 1.5` 暂定倍率。目标圈、落点圈、如厕准星均隐藏。`NextStoolKind` 在便意从 0 开始及达到极限时预抽，`StoolOutput.Roll` 复用该值。

马桶冲水交互改为独立 `ToiletFlushButton`，由 `PlumbingIntegration` 以 Unity API 在 `Flush handle target` 创建 7 cm 碰撞体；按钮提示为“E 冲水”，交互距离仍由设施规则限制。马桶本体保留疏通/捣蛋行为，不再承担冲水。

验证：`python3 scripts/check.py` 通过；最新 EditMode 70/70 通过，结果见 `Artifacts/20261126T044139Z-83d2a6-test-editmode/results.xml`；PlayMode 已执行，97 项中 66 通过、30 个旧流程/旧镜头断言失败、1 跳过，结果见 `Artifacts/20261126T042755Z-f0fa32-test-playmode/results.xml`；最新 `build-server` 成功，见 `Artifacts/20261126T044242Z-febc3c-build-server/build.ok`。PlayMode 失败项需在后续替换为新流程断言后重跑。

未完成/疑问：联机“马桶上撒尿”仍复用现有 `RequestSeat/ReportResult`，服务器尚未增加独立尿液模式字段；自动失控如厕尚未按新状态单独验收；Playground、Map1、Map2、Map3 的现有场景按钮需运行 `plumbing`/对应集成菜单后才能确认每个按钮的位置。旧测试仍引用 `Aligning/Holding`、目标圈和自动 5 秒结算，应按新流程替换。以上规则未写入 GLOSSARY，保留待人类决定的部分。

Semantic Diff：采用 `Squatting`、`Aiming`、`Urinating` 作为输入状态；新增唯一的 `ToiletFlushButton` 组件；移除“马桶本体冲水”和“如厕目标圈/落点标记”的实现路径。旧枚举名仅保留为兼容编译符号，未作为新流程入口。
