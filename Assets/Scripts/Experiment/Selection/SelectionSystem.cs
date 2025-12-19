using System.Collections.Generic;
using UnityEngine;
using ImmersiveMapInterface.Board;

namespace ImmersiveMapInterface.Experiment.Selection
{
    public class SelectionSystem : MonoBehaviour
    {
        [Header("Refs")]
        public PoleBasedBoardState boardState;
        public ExperimentConfig config;

        [Header("Pointer")]
        public Transform pointerOrigin; // controller transform used for raycasts
        [Tooltip("Base ray length used for selection/hover (meters)")]
        public float maxDistance = 10f;
        [Tooltip("Multiplier applied to ray length in Bird condition")]
        public float birdRayMultiplier = 3f;
        [Tooltip("Multiplier applied to ray length in Internal condition")]
        public float internalRayMultiplier = 1f;
        [Tooltip("Multiplier applied to ray length in Internal+Miniature condition")]
        public float internalMiniRayMultiplier = 1f;
        public LayerMask pickMask = ~0;

        [Header("Feedback")]
        public Color hoverColor = new Color(1f, 1f, 0.1f, 0.8f);
        public Color selectColor = new Color(0.2f, 1f, 0.2f, 0.8f);
        public Material foundMaterial; // red, applied by FoundLinesHighlighter

        private bool hasFirst = false;
        private (int pole,int slot) firstSel;

        private readonly HashSet<(int pole,int slot)> foundCells = new();
        private readonly HashSet<HashSet<(int pole,int slot)>> foundLines = new();

        // Events for external systems (logger, UI)
        public System.Action OnWrongAttemptEvent;
        public System.Action OnCorrectLineFoundEvent;
        public System.Action OnSelectionCanceledEvent;

        [Header("Highlight")]
        public ImmersiveMapInterface.Visualization.FoundLinesHighlighter highlighter;

        [Header("Debug")]
        public bool logDebug = false;

        [Header("Click Handling")]
        [Tooltip("Ignore repeated clicks on the same piece within this time window (seconds).")]
        public float clickDebounceSeconds = 0.15f;
        private (int pole, int slot) lastClick = (-1, -1);
        private float lastClickTime = -999f;

        [Header("Hover")]
        public bool enableHoverHighlight = true;

        private void Awake()
        {
            if (boardState == null) boardState = FindObjectOfType<PoleBasedBoardState>();
        }

        public void ClearSelection()
        {
            ResetPendingSelection(true);
        }

        public void CancelPendingSelection()
        {
            if (!hasFirst) return;
            ResetPendingSelection(true);
            OnSelectionCanceledEvent?.Invoke();
        }

        private void Update()
        {
            if (!enableHoverHighlight || pointerOrigin == null || highlighter == null) return;
            if (RaycastToPiece(pointerOrigin.position, pointerOrigin.forward, out int hpole, out int hslot))
            {
                var cell = (hpole, hslot);
                var color = (hasFirst && AreSame(firstSel, cell)) ? selectColor : hoverColor;
                highlighter.SetHoverCell(cell, color);
            }
            else
            {
                highlighter.ClearHover();
            }
        }

        // Call from input: attempt selection at current pointer ray
        public void TrySelectAtPointer()
        {
            if (pointerOrigin == null)
            {
                if (logDebug) Debug.Log("Selection: pointer origin not assigned.");
                return;
            }
            if (RaycastToPiece(pointerOrigin.position, pointerOrigin.forward, out int pole, out int slot))
            {
                // Debounce: ignore immediate duplicate hits on the same piece
                if (pole == lastClick.pole && slot == lastClick.slot && (Time.time - lastClickTime) < clickDebounceSeconds)
                {
                    if (logDebug) Debug.Log($"Selection: debounced duplicate click P{pole} S{slot}");
                    return;
                }
                lastClick = (pole, slot);
                lastClickTime = Time.time;
                TrySelect(pole, slot);
            }
            else if (logDebug)
            {
                Debug.Log("Selection: pointer ray did not hit a piece.");
            }
        }

        public void TrySelect(int pole, int slot)
        {
            if (!hasFirst)
            {
                firstSel = (pole, slot);
                hasFirst = true;
                if (logDebug) Debug.Log($"Selection: first endpoint set P{pole} S{slot}");
                if (highlighter != null)
                {
                    highlighter.SetPreviewCell(firstSel, selectColor);
                }
                return;
            }

            var second = (pole, slot);
            if (AreSame(firstSel, second)) { if (logDebug) Debug.Log("Selection: second equals first; ignoring."); return; }

            // Validate straight length-4
            if (!TryBuildLine(firstSel, second, out var lineSet))
            {
                if (logDebug) Debug.Log("Selection: endpoints do not form a straight length-4 line.");
                OnWrongAttempt();
                ResetPendingSelection(true);
                return;
            }

            // Compare with ground truth pattern
            if (IsTargetLine(lineSet))
            {
                MarkFound(lineSet);
                OnCorrectLineFound();
                if (logDebug) Debug.Log("Selection: target line confirmed.");
            }
            else
            {
                if (logDebug) Debug.Log("Selection: not a target line (will not highlight red).");
                OnWrongAttempt();
            }
            ResetPendingSelection(true);
        }

        private bool RaycastToPiece(Vector3 origin, Vector3 dir, out int pole, out int slot)
        {
            pole = -1; slot = -1;
            float dist = GetEffectiveMaxDistance();
            if (Physics.Raycast(origin, dir, out var hit, dist, pickMask, QueryTriggerInteraction.Ignore))
            {
                // Resolve piece root by walking up parents (collider may sit on a child)
                var go = hit.collider.attachedRigidbody ? hit.collider.attachedRigidbody.gameObject : hit.collider.gameObject;
                Transform t = go.transform; int hops = 0;
                while (t != null && hops < 6)
                {
                    if (TryParsePieceName(t.name, out pole, out slot)) return true;
                    t = t.parent; hops++;
                }
            }
            return false;
        }

        public float CurrentPointerLength => GetEffectiveMaxDistance();
        public LayerMask PointerLayerMask => pickMask;

        private float GetEffectiveMaxDistance()
        {
            if (config == null)
            {
                return maxDistance;
            }
            switch (config.condition)
            {
                case ExperimentCondition.Bird:
                    return maxDistance * Mathf.Max(0f, birdRayMultiplier);
                case ExperimentCondition.Internal:
                    return maxDistance * Mathf.Max(0f, internalRayMultiplier);
                case ExperimentCondition.InternalWithMiniature:
                    return maxDistance * Mathf.Max(0f, internalMiniRayMultiplier);
                default:
                    return maxDistance;
            }
        }

        private static bool TryParsePieceName(string name, out int pole, out int slot)
        {
            pole = -1; slot = -1;
            int pIdx = name.IndexOf("_P");
            int sIdx = name.IndexOf("_S");
            if (pIdx < 0 || sIdx < 0) { pIdx = name.IndexOf("P"); sIdx = name.IndexOf("S"); }
            if (pIdx < 0 || sIdx < 0) return false;
            int pStart = pIdx + 1; if (name[pIdx] == '_') pStart++;
            int sStart = sIdx + 1; if (name[sIdx] == '_') sStart++;
            string pStr = ExtractDigits(name, pStart);
            string sStr = ExtractDigits(name, sStart);
            if (int.TryParse(pStr, out pole) && int.TryParse(sStr, out slot)) return true;
            return false;
        }

        private static string ExtractDigits(string s, int start)
        {
            int i = start;
            var ch = new System.Text.StringBuilder();
            while (i < s.Length && char.IsDigit(s[i])) { ch.Append(s[i]); i++; }
            return ch.ToString();
        }

        private static bool AreSame((int pole,int slot) a, (int pole,int slot) b)
        {
            return a.pole == b.pole && a.slot == b.slot;
        }

        private bool TryBuildLine((int pole,int slot) a, (int pole,int slot) b, out HashSet<(int pole,int slot)> set)
        {
            set = new HashSet<(int,int)>();
            PoleBasedBoardState.PoleIndexToGrid(a.pole, out int ax, out int az);
            PoleBasedBoardState.PoleIndexToGrid(b.pole, out int bx, out int bz);
            int ay = a.slot; int by = b.slot;
            int dx = bx - ax; int dy = by - ay; int dz = bz - az;
            // must be straight length 4
            int adx = Mathf.Abs(dx); int ady = Mathf.Abs(dy); int adz = Mathf.Abs(dz);
            if (!((adx == 0 || adx == 3) && (ady == 0 || ady == 3) && (adz == 0 || adz == 3))) return false;
            if (!(adx == 3 || ady == 3 || adz == 3)) return false;
            int sx = dx == 0 ? 0 : (dx > 0 ? 1 : -1);
            int sy = dy == 0 ? 0 : (dy > 0 ? 1 : -1);
            int sz = dz == 0 ? 0 : (dz > 0 ? 1 : -1);
            for (int i = 0; i < 4; i++)
            {
                int x = ax + sx * i;
                int y = ay + sy * i;
                int z = az + sz * i;
                if (!(PoleBasedBoardState.IsValidGrid(x, z) && (uint)y < 8)) return false;
                set.Add((PoleBasedBoardState.GridToPoleIndex(x, z), y));
            }
            return true;
        }

        private bool IsTargetLine(HashSet<(int pole,int slot)> set)
        {
            if (config == null || config.pattern == null) return false;
            var targets = BoardPopulationService_BuildTargetLineSets(config.pattern);
            foreach (var t in targets)
            {
                if (t.SetEquals(set)) return true;
            }
            return false;
        }

        private static List<HashSet<(int pole,int slot)>> BoardPopulationService_BuildTargetLineSets(PatternDefinition pattern)
        {
            var result = new List<HashSet<(int pole, int slot)>>();
            if (pattern == null) return result;
            foreach (var line in pattern.EnumerateLineCellSets())
            {
                result.Add(new HashSet<(int pole, int slot)>(line));
            }
            return result;
        }

        private void MarkFound(HashSet<(int pole,int slot)> set)
        {
            foreach (var c in set) foundCells.Add(c);
            foundLines.Add(set);
            if (highlighter != null) highlighter.HighlightCells(set);
        }

        private void OnWrongAttempt()
        {
                if (highlighter != null) { highlighter.ClearPreview(); highlighter.ClearHover(); }
                OnWrongAttemptEvent?.Invoke();
        }

        private void OnCorrectLineFound()
        {
            if (highlighter != null) { highlighter.ClearPreview(); highlighter.ClearHover(); }
            OnCorrectLineFoundEvent?.Invoke();
        }

        private void ResetPendingSelection(bool clearVisuals)
        {
            hasFirst = false;
            lastClick = (-1, -1);
            if (clearVisuals && highlighter != null)
            {
                highlighter.ClearPreview();
                highlighter.ClearHover();
            }
        }
    }
}
