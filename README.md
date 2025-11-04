# ImmersiveMapInterface
Research prototype exploring a new UI pattern for search/selection in dense and occluded 3D spaces.

**Working Title**
- Optimizing Search and Select Actions in Dense and Occluded Spaces via a Miniature 3D-World Interface

**Goal**
- Help users find “four-in-a-row” lines inside a dense 8×8×8 field (512 pieces, two colors) in VR.
- Compare a proposed Miniature 3D-World Interface against two baselines.

**Conditions (single scene, switchable)**
- Bird PoV: User grabs the board itself to rotate it; no teleportation/movement.
- Internal PoV: User moves inside the field with continuous locomotion.
  - Left stick: planar move relative to HMD yaw
  - Right stick (Y): vertical up/down
- Internal PoV + Miniature: Same locomotion as Internal. A miniature Bird view is always in front of the user at chest height. The miniature yaw follows head yaw; pitch is ignored. The miniature is not directly manipulated.

**Task**
- Users must find three distinct four-in-a-row lines. No time limit.

**What We Measure**
- Total time to find all three lines.
- Time to find each individual line.
- Number of wrong attempts (endpoint pairs that are not a valid target line).

**Board And Targets**
- Field size: 8×8×8 (512 pieces) using the pole-based representation.
- Three “correct” lines are preselected from predefined patterns by the experimenter before a session.
- All three correct lines are white and use non-overlapping cells.
- Global color distribution is 50/50 white/black.
- Strongly enforce “only those three correct lines exist” by generation + detector validation. If extra four-in-a-row exists, regenerate and re-validate (bounded retries with warning if exceeded).

**Selection Flow**
- Endpoint selection: user selects two endpoints; the system completes the line if it is a straight line of length four.
- Feedback: if not valid, show a warning and count one wrong attempt; if valid and matches a ground-truth line, mark those four pieces as found (turn them red) and lock them.

**Controls (Meta Quest 3, controllers)**
- Bird: one-handed grab to rotate the board (yaw-focused; can extend later).
- Internal: continuous move with HMD-yaw-relative forward; left stick = planar, right stick Y = vertical.
- No teleport.
- Selection: Trigger to pick endpoints; B to clear the current selection.

**Miniature Behavior**
- Always in front of the user at chest height.
- Yaw only follows the user’s head (pitch ignored).
- Mirrors the board’s current state and orientation; miniature is view-only.

**Experiment Orchestration**
- Counterbalanced order across conditions (managed externally by the experimenter).
- Single scene with an in-editor dropdown and in-headset minimal UI to pick Condition and Pattern.
- English-only UI text.

**Data Logging**
- Will post to a Google Apps Script (GAS) Web API that writes to Google Sheets.
- Until GAS is provided, data will be saved locally (CSV) under `Application.persistentDataPath` and can be re‑posted later.
- Expected payload (subject to finalization with GAS): subjectId, condition, patternId, startTime, per-line timestamps, totalTime, wrongAttempts, device info.

**Implementation Plan (high level)**
- Condition Manager
  - Single scene toggles: Bird / Internal / Internal+Miniature
  - Minimal in-headset UI to display current condition and session controls
- Pattern System
  - ScriptableObject for predefined patterns: three lines defined by endpoint pairs (code completes the four cells)
  - Board population service: apply pattern (white), fill remaining cells to keep 50/50, enforce “only three lines” via detector validation + regeneration
- Selection System
  - Ray-based endpoint picking with visual feedback
  - Validation against straight length-4 and pattern ground truth
  - Wrong-attempt counting, correct-line locking and red highlight
- Locomotion & Manipulation
  - Bird: grab-to-rotate `Ground`/board (one hand)
  - Internal: reuse/adapt head-yaw-relative locomotion (left stick planar, right stick vertical)
  - Miniature: chest-anchored follower, yaw-only head coupling, no direct manipulation
- Logging
  - Session manager tracks start, each correct line time, total time, wrong attempts
  - Post to GAS (URL/headers from ScriptableObject). Fallback to CSV when offline
- Polish & Ops
  - Convert editor/menu strings to English and fix garbled characters
  - Simple experimenter panel: subject ID, condition selector, pattern selector, Start/Finish/Abort

**Open Items (to finalize later)**
- GAS endpoint details: URL, method, headers/auth, JSON schema
- Miniature yaw/board rotation composition specifics (adjust after first prototype)
- Trial count per session and practice trials
- Optional visibility aids (slice/X-ray/outline) — currently none

**Tech Notes**
- Uses pole-based board (`PoleBasedBoardState`, `PoleBasedBoardGenerator`, `PoleBasedFourInARowDetector`).
- XR stack: Meta OpenXR + Input System + URP.

