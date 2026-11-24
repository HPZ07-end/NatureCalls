# NatureCalls controls refactor third review

Reverted rejected commit 880edc4 with 9b09c21. Forbidden test-detection, name-based prompt selection, and widened distances are absent.

New commits:
- d3f831b migrated interaction tests toward explicit aiming/F and added TestAim helper.

Implemented in this pass:
- Item has explicit PromptLabel; Map2 assigns the emergency-paper label in Awake.
- Added TestAim helper and migrated MapLayout, Map1, and Accuracy interaction calls toward aimed E/F semantics.
- TryAction is public for direct F assertions.

Verification:
- EditMode: 35/35 passed, artifact 20261124T155734Z-6852e1.
- Filtered LayoutPlayerInteractSelectsNearbyDoorAndPaperRoll: failed, artifact 20261124T155904Z-ea8d02; E still returned false after helper aiming. This is an unresolved focus/camera integration bug, so the required unfiltered PlayMode pass has not been reached.
- Final unfiltered PlayMode, build-server, grep, and semantic rerun after this last commit: not run / pending because the focused test remains blocked.

No push or PR opened.
