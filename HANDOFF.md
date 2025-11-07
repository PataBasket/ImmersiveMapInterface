# ImmersiveMapInterface – Handoff Notes

## Project Snapshot
- VR study prototype with 8×8×8 pole-based board.
- Three conditions: Bird (board grab/rotate), Internal (continuous locomotion), Internal+Miniature (internal plus chest-level miniature board).
- Core systems in place: PoleBasedBoardState, SelectionSystem (hover + debounced selection + highlighting), BoardPopulationService (3 target lines + 50/50 balance), ControllerVisualToggle, Miniature infrastructure.

## Recent Progress
- Internal locomotion now uses TrackingSpace as move target, includes vertical movement + bounds clamp.
- SelectionSystem supports hover tint, preview tint, duplicate-click suppression, and correct-line highlighting.
- Controller/hand visuals managed by `ControllerVisualToggle` (with proxy when controllers absent).
- Miniature pipeline refactored: `MiniatureRoot` + `MiniatureFollower`, `MiniaturePoleBoardGenerator` now generates pieces under `MiniaturePieces`, supports fallback colors, pole layout inheritance, (WIP) world-scaling.

## Open Issues / Tasks
1. **Miniature pieces not generating**
   - After recent world-scale changes, no mini pieces appear (only WorldInMiniature base).
   - Need to verify `piecePrefab`, `boardState`, and world scale settings; add logging/debug.
2. **Miniature world scaling**
   - Goal: true 1/40 replica using `worldBoardRoot`/`worldPivot`.
   - Implementation partly in place but not validated; pivot math likely wrong (bases shifted upward).
   - Decide whether to keep `useWorldScale` or rely on `poleLayoutRoot=WorldInMiniature`.
3. **Miniature orientation indicator (Google Maps–style “you are here”)**
   - Not implemented. Need design for direction arrow and player marker inside miniature.
4. **Controller+Hand simultaneous visuals**
   - ControllerVisualToggle sets proxies but real OVR controller visuals still hidden when hand-tracking active.
   - Requires toggling OVR hand-tracking mode or forcing OVRControllerVisual visibility.
5. **Bounds configuration**
   - `ConditionManager.overrideBounds` now available but default is false. Decide final values (e.g., `(5,5,5)` with lifted center) and document usage.

## Suggested Next Steps
1. Miniature debugging
   - Add logging around `EnsureGenerated` to confirm `piecePrefab` instantiation and layout dictionaries.
   - Test with `useWorldScale = false`, `poleLayoutRoot = WorldInMiniature`, `usePoleLayout = true`.
   - Once stable, re-enable world-scale path and compute pivot offsets accurately.
2. Orientation indicator
   - Add `MiniaturePlayerMarker` that reads XR rig pose, converts to miniature coordinates (`miniatureScale`), renders arrow/marker.
3. Controller visuals
   - In Project Settings > Meta XR > Hand Tracking Support, switch to “Controllers and Hands” or “Controllers Only”.
   - Alternatively, programmatically call `OVRManager.SetControllerVisibility(true)` or override `OVRControllerHelper`.
4. Documentation
   - Update README with instructions for configuring MiniatureRoot/Pieces and ConditionManager bounds override.
5. Testing
   - Verify selection (hover + highlight) post-miniature fixes.
   - Ensure Bird/Internal toggles still behave after recent refactors.

## Notes
- MiniatureRoot hierarchy currently expected as:
  ```
  MiniatureRoot
  ├─ WorldInMiniature (base mesh, scale 0.4,0.005,0.4)
  └─ MiniaturePieces (holds MiniaturePoleBoardGenerator output)
  ```
- `MiniatureFollower` handles placement; `miniatureGrabRotate` optional.
- Stack offset currently manual (`stackVerticalOffset`). Once world scaling works, this knob might be redundant.

Please use this doc when resuming work in a new session/tab.
