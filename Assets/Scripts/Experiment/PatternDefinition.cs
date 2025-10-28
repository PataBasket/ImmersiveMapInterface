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

        [Header("Three target white lines (non-overlapping)")]
        [Tooltip("Each line is specified by two endpoints; code will complete the 4 cells.")]
        public LineEndpoints line1;
        public LineEndpoints line2;
        public LineEndpoints line3;

        public IEnumerable<(int poleIndex, int slotIndex)> EnumerateAllTargetCells()
        {
            if (TryGetLineCells(line1, out var cells1, out _))
            {
                foreach (var c in cells1) yield return c;
            }
            if (TryGetLineCells(line2, out var cells2, out _))
            {
                foreach (var c in cells2) yield return c;
            }
            if (TryGetLineCells(line3, out var cells3, out _))
            {
                foreach (var c in cells3) yield return c;
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
