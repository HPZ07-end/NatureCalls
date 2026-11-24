# NatureCalls controls refactor fourth review

Rejected compatibility commit was reverted: `9b09c21` reverts `880edc4`.

Current fixes:
- `Movement.LookAt(Vector3)` sets yaw and clamped pitch from the player view state.
- `TestAim` uses movement view state and waits two frames.
- `IInteractable.AimPoint` uses combined collider/renderer bounds; stall doors use the door leaf collider centre.
- `InteractionRegistry.Resolve` now uses the registry only; per-frame scene scans were removed.
- Emergency paper uses explicit `Item.PromptLabel`, assigned by `Map2Interactions`.

Verification:
- EditMode: PASSED, 35/35, artifact `20261124T160910Z-6caa17`.
- Filtered `LayoutPlayerInteractSelectsNearbyDoorAndPaperRoll`: FAILED, 0/1, artifact `20261124T160946Z-9afd18`; E still returns false after the new aim helper. This is an unresolved registration/focus integration issue.
- Required final unfiltered PlayMode, server build, grep, and final semantic verification after this commit: not run because the focused test remains blocked.

No push or PR opened.
