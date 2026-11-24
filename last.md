# NatureCalls controls refactor review follow-up

Fix commits:
- aa4a570 complete Map2 interactables, generic RequestInteract, movement identity default, and Core rule tests
- d49bbe0 add test metadata

Implemented: Map2 vending/repair/coin interactables now expose registered E actions and use generic online RequestInteract; Map2 station actions remain validated by CanUse and server-side facilities. Movement effect aggregation now starts from identity (speed 1), preserving origin/dev movement when no effect applies. Added EditMode coverage for MovementRules, RepairRules, and vending. HandsDirty is synchronized through NeedsSync. Added pr-body.md.

Verification in required order:
1. EditMode: PASSED, 35 total, 35 passed, 0 failed, 0 ignored (20261124T151306Z-62bca2).
2. build-server: PASSED; Linux64 build plus desktop restore succeeded (20261124T151342Z-6fa495).
3. unfiltered PlayMode: FAILED, 43 total, 27 passed, 15 failed, 1 skipped (20261124T152028Z-35827a). Movement accumulation failures are resolved. Remaining failures are primarily existing tests that still assume old lobby/map setup or old interaction assertions; the prompt-hides-after-toileting test also needs its approved F-prompt expectation migrated. The known CameraBlends test is included among the failures, and Relay remains skipped.
4. generic-reference grep: PASSED, no output.
5. semantic check: PASSED with pr-body.md.

Remaining work: migrate the 15 PlayMode cases to explicit camera aiming and F semantics, add dedicated PlayMode assertions for crosshair priority, hand wiping, door-with-Unwiped, and mop hold completion, and finish provider adapters if further facility-specific prompt behavior is required. No push or PR opened.
