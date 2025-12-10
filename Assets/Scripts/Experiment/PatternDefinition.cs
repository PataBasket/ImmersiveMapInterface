using System;
using System.Collections.Generic;
using UnityEngine;
using ImmersiveMapInterface.Board;

namespace ImmersiveMapInterface.Experiment
{
    [CreateAssetMenu(fileName = "PatternDefinition", menuName = "ImmersiveMap/Pattern Definition", order = 1)]
    public class PatternDefinition : ScriptableObject
    {
        [Serializable]
        public struct Endpoint
        {
            public int poleIndex; // 0..63 (x + 8*z)
            public int slotIndex; // 0..7 (y)
        }

        [Serializable]
        public struct LineEndpoints
        {
            public Endpoint a;
            public Endpoint b;
        }

        [Header("Meta")]
        public string patternId = Guid.NewGuid().ToString();
        public string patternName = "Pattern";

        [Header("Target white lines (length-4, non-overlapping)")]
        [Tooltip("Define all white lines that should exist on the board. Each entry is 2 endpoints and will generate 4 contiguous cells.")]
        [SerializeField] private List<LineEndpoints> lines = new List<LineEndpoints>(16);

        public IReadOnlyList<LineEndpoints> Lines => lines;
        public int LineCount => lines != null ? lines.Count : 0;

        public IEnumerable<(int poleIndex, int slotIndex)> EnumerateAllTargetCells()
        {
            if (lines == null) yield break;
            for (int i = 0; i < lines.Count; i++)
            {
                if (!TryGetLineCells(lines[i], out var cells, out string error))
                {
                    Debug.LogWarning($"PatternDefinition '{patternName}': line {i} invalid. {error}", this);
                    continue;
                }
                foreach (var c in cells) yield return c;
            }
        }

        public IEnumerable<List<(int poleIndex, int slotIndex)>> EnumerateLineCellSets()
        {
            if (lines == null) yield break;
            for (int i = 0; i < lines.Count; i++)
            {
                if (!TryGetLineCells(lines[i], out var cells, out string error))
                {
                    Debug.LogWarning($"PatternDefinition '{patternName}': line {i} invalid. {error}", this);
                    continue;
                }
                yield return cells;
            }
        }

        public static bool TryGetLineCells(LineEndpoints line, out List<(int poleIndex, int slotIndex)> cells, out string error)
        {
            cells = new List<(int poleIndex, int slotIndex)>(4);
            error = string.Empty;

            GridFromPole(line.a.poleIndex, out int ax, out int az);
            GridFromPole(line.b.poleIndex, out int bx, out int bz);
            int ay = line.a.slotIndex;
            int by = line.b.slotIndex;

            int dx = bx - ax;
            int dy = by - ay;
            int dz = bz - az;

            if (!IsValidLength4(dx, dy, dz))
            {
                error = $"Endpoints must define a straight length-4 line. d=({dx},{dy},{dz}).";
                return false;
            }

            int stepx = SignNonZero(dx);
            int stepy = SignNonZero(dy);
            int stepz = SignNonZero(dz);

            for (int i = 0; i < 4; i++)
            {
                int x = ax + stepx * i;
                int y = ay + stepy * i;
                int z = az + stepz * i;
                if (!PoleBasedBoardState.IsValidGrid(x, z) || (uint)y >= PoleBasedBoardState.PiecesPerPole)
                {
                    error = $"Computed cell ({x},{y},{z}) is out of bounds.";
                    cells.Clear();
                    return false;
                }
                cells.Add((PoleBasedBoardState.GridToPoleIndex(x, z), y));
            }
            return true;
        }

        private static void GridFromPole(int poleIndex, out int x, out int z)
        {
            PoleBasedBoardState.PoleIndexToGrid(poleIndex, out x, out z);
        }

        private static int SignNonZero(int v)
        {
            if (v == 0) return 0;
            return v > 0 ? 1 : -1;
        }

        private static bool IsValidLength4(int dx, int dy, int dz)
        {
            int adx = Math.Abs(dx);
            int ady = Math.Abs(dy);
            int adz = Math.Abs(dz);
            if (!((adx == 0 || adx == 3) && (ady == 0 || ady == 3) && (adz == 0 || adz == 3))) return false;
            // at least one axis must be 3
            return (adx + ady + adz) > 0 && (adx == 3 || ady == 3 || adz == 3);
        }
    }
}
