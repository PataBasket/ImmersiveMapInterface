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
            foreach (var c in EnumerateLineCells(line1)) yield return c;
            foreach (var c in EnumerateLineCells(line2)) yield return c;
            foreach (var c in EnumerateLineCells(line3)) yield return c;
        }

        public static IEnumerable<(int poleIndex, int slotIndex)> EnumerateLineCells(LineEndpoints line)
        {
            GridFromPole(line.a.poleIndex, out int ax, out int az);
            GridFromPole(line.b.poleIndex, out int bx, out int bz);
            int ay = line.a.slotIndex;
            int by = line.b.slotIndex;

            int dx = bx - ax;
            int dy = by - ay;
            int dz = bz - az;

            // must be straight length 4: each component abs is 0 or 3; not all zero
            if (!IsValidLength4(dx, dy, dz)) yield break;

            int stepx = SignNonZero(dx);
            int stepy = SignNonZero(dy);
            int stepz = SignNonZero(dz);

            for (int i = 0; i < 4; i++)
            {
                int x = ax + stepx * i;
                int y = ay + stepy * i;
                int z = az + stepz * i;
                if (!PoleBasedBoardState.IsValidGrid(x, z) || (uint)y >= PoleBasedBoardState.PiecesPerPole)
                    yield break;
                yield return (PoleBasedBoardState.GridToPoleIndex(x, z), y);
            }
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

