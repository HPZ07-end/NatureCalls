# NatureCalls controls refactor report

Commits:
- 171af9f refactor: route E and F through facility interactions
- 59c9bfd refactor: combine movement effects through core rules
- a38b6e9 refactor: store shared facility state generically
- 839c46b docs: record E F controls and facility contracts
- ff48ae9 chore: add repair rules asset metadata
- 8a56733 fix: discard destroyed interaction registry entries

Implemented: registered facility-owned interactables and camera-centre E focus with cone/proximity fallback; F paper/hand wipe and mop hold; HandsDirty; Core MovementRules and effect sources; generic FacilityStateEntry for sewer/coin/repair state; RepairRules and vending helpers; docs and approved Glossary markers.

Verification:
- EditMode: PASSED, 31 total, 31 passed, 0 failed, 0 ignored (latest 20261124T145422Z-6daa22).
- Build server: PASSED; Linux64 build and desktop restore succeeded (20261124T145529Z-135e58).
- PlayMode unfiltered: FAILED, 43 total, 24 passed, 18 failed, 1 skipped. Failures include stale tests still assuming proximity E and one allowed camera blend test; several lifecycle/movement regressions remain and require follow-up before integration.
- Generic-reference grep: PASSED, no output for requested six files.
- Semantic check: PASSED, changed; required paths and approval sections present.

PR body draft is in /tmp/naturecalls-pr-body.md.

Unfinished/uncertain: Map2Interactions exposes the provider contract but its adapter collection is still empty; PlayMode tests were not fully migrated to aim cameras; Network NeedsSync does not yet serialize HandsDirty; dedicated door/item/toilet/mess lists remain legacy compatibility state. No PR opened or pushed.
