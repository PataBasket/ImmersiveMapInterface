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
        public float maxDistance = 10f;
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

        [Header("Highlight")]
        public ImmersiveMapInterface.Visualization.FoundLinesHighlighter highlighter;

        [Header("Debug")]
        public bool logDebug = false;

        private void Awake()
        {
            if (boardState == null) boardState = FindObjectOfType<PoleBasedBoardState>();
        }

        public void ClearSelection()
        {
            hasFirst = false;
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
                return;
            }

            var second = (pole, slot);
            if (AreSame(firstSel, second)) { hasFirst = false; return; }

            // Validate straight length-4
            if (!TryBuildLine(firstSel, second, out var lineSet))
            {
                if (logDebug) Debug.Log("Selection: endpoints do not form a straight length-4 line.");
                OnWrongAttempt();
                hasFirst = false;
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
            hasFirst = false;
        }

        private bool RaycastToPiece(Vector3 origin, Vector3 dir, out int pole, out int slot)
        {
            pole = -1; slot = -1;
            if (Physics.Raycast(origin, dir, out var hit, maxDistance, pickMask, QueryTriggerInteraction.Ignore))
            {
                // expect piece name format contains P<pole>_S<slot>, e.g., Piece_P12_S3 or MiniPiece_P..
                var go = hit.collider.attachedRigidbody ? hit.collider.attachedRigidbody.gameObject : hit.collider.gameObject;
                if (TryParsePieceName(go.name, out pole, out slot)) return true;
            }
            return false;
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
            var target = new HashSet<(int,int)>(pattern.EnumerateAllTargetCells());
            return BoardPopulationService_BuildTargetLineSets(target);
        }

        // Duplicate of BoardPopulationService.BuildTargetLineSets to avoid coupling
        private static List<HashSet<(int pole,int slot)>> BoardPopulationService_BuildTargetLineSets(HashSet<(int pole,int slot)> targetCells)
        {
            var remaining = new HashSet<(int,int)>(targetCells);
            var result = new List<HashSet<(int,int)>>(3);
            Vector3Int[] dirs = new[]
            {
                new Vector3Int(1,0,0), new Vector3Int(0,1,0), new Vector3Int(0,0,1),
                new Vector3Int(1,1,0), new Vector3Int(1,-1,0),
                new Vector3Int(1,0,1), new Vector3Int(1,0,-1),
                new Vector3Int(0,1,1), new Vector3Int(0,1,-1),
                new Vector3Int(1,1,1), new Vector3Int(1,1,-1), new Vector3Int(1,-1,1), new Vector3Int(1,-1,-1)
            };
            while (remaining.Count > 0 && result.Count < 3)
            {
                var enumerator = remaining.GetEnumerator();
                if (!enumerator.MoveNext()) break;
                var cell = enumerator.Current;
                PoleBasedBoardState.PoleIndexToGrid(cell.Item1, out int x, out int z);
                int y = cell.Item2;
                var origin = new Vector3Int(x,y,z);
                bool foundGroup = false;
                foreach (var d in dirs)
                {
                    var group = new HashSet<(int,int)>();
                    for (int i = 0; i < 4; i++)
                    {
                        var p = origin + d * i;
                        if (!Lines3DUtility.InBounds(p)) { group.Clear(); break; }
                        int pole = PoleBasedBoardState.GridToPoleIndex(p.x, p.z);
                        var tuple = (pole, p.y);
                        if (!remaining.Contains(tuple)) { group.Clear(); break; }
                        group.Add(tuple);
                    }
                    if (group.Count == 4)
                    {
                        foreach (var g in group) remaining.Remove(g);
                        result.Add(group);
                        foundGroup = true;
                        break;
                    }
                }
                if (!foundGroup) remaining.Remove(cell);
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
            SendMessage("OnWrongAttempt", SendMessageOptions.DontRequireReceiver);
            OnWrongAttemptEvent?.Invoke();
        }

        private void OnCorrectLineFound()
        {
            SendMessage("OnCorrectLineFound", SendMessageOptions.DontRequireReceiver);
            OnCorrectLineFoundEvent?.Invoke();
        }
    }
}
