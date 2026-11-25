# NatureCalls controls refactor final verification

本轮完成：
- 共享设施状态改为 `ISharedFacilityState` 接口入口，Network 层不再包含 Coin、Repair、Sewer、Map2 领域名称；保留门、物品、马桶、小便池、污物列表并标注 legacy 迁移计划。
- `MovementEffectRegistry` 改为 OnEnable/OnDisable 注册，移动时不再扫描场景；新增注册源测试。
- README、ARCHITECTURE、PLAYGROUND、REUSE_GUIDE、DEVELOPMENT_STATUS 改为中文并清理旧 E 顺序/长按 E/按 E 擦拭描述；GLOSSARY 未修改。

验证：
- test-editmode：通过，36/36，artifact `20261125T011200Z-d027e2-test-editmode`。
- build-server：通过，artifact `20261125T011729Z-f4d803-build-server`。
- unfiltered test-playmode：通过，42/43，1 Relay Ignored，artifact `20261125T012415Z-9a6365-test-playmode`。
- grep 4：通过，无输出；Network 扩展 grep `Coin|Repair|Sewer|Map2`：通过，无输出。
- semantic.py：通过，`changed`，body `pr-body.md`。

未 push、未开 PR。ProjectSettings.asset 已恢复。
