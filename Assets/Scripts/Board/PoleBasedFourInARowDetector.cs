using System;
using System.Collections.Generic;
using UnityEngine;

namespace ImmersiveMapInterface.Board
{
    /// <summary>
    /// Detects 4-in-a-row on pole-based 8x8x8 board.
    /// Checks all possible lines: horizontal, vertical, depth, and diagonals.
    /// </summary>
    public class PoleBasedFourInARowDetector : MonoBehaviour
    {
        [SerializeField] private PoleBasedBoardState boardState;

        private List<LineDefinition> lines = new List<LineDefinition>();

        public event Action<LineDefinition, PoleBasedBoardState.PieceColor>? OnLineFound;

        [System.Serializable]
        public struct LineDefinition
        {
            public int poleIndex1, slotIndex1;
            public int poleIndex2, slotIndex2;
            public int poleIndex3, slotIndex3;
            public int poleIndex4, slotIndex4;
            public string description; // For debugging

            public LineDefinition(int p1, int s1, int p2, int s2, int p3, int s3, int p4, int s4, string desc)
            {
                poleIndex1 = p1; slotIndex1 = s1;
                poleIndex2 = p2; slotIndex2 = s2;
                poleIndex3 = p3; slotIndex3 = s3;
                poleIndex4 = p4; slotIndex4 = s4;
                description = desc;
            }
        }

        private void Awake()
        {
            if (boardState == null) boardState = GetComponent<PoleBasedBoardState>();
            PrecomputeLines();
        }

        public void ScanAll()
        {
            if (boardState == null) return;

            foreach (var line in lines)
            {
                var color1 = boardState.GetPiece(line.poleIndex1, line.slotIndex1);
                if (color1 == PoleBasedBoardState.PieceColor.Empty) continue;

                var color2 = boardState.GetPiece(line.poleIndex2, line.slotIndex2);
                var color3 = boardState.GetPiece(line.poleIndex3, line.slotIndex3);
                var color4 = boardState.GetPiece(line.poleIndex4, line.slotIndex4);

                if (color1 == color2 && color2 == color3 && color3 == color4)
                {
                    OnLineFound?.Invoke(line, color1);
                }
            }
        }

        private void PrecomputeLines()
        {
            lines.Clear();

            // Horizontal lines (same z, same y, different x)
            for (int z = 0; z < 8; z++)
            {
                for (int y = 0; y < 8; y++)
                {
                    for (int x = 0; x <= 4; x++) // 4 possible horizontal lines per row
                    {
                        int p1 = PoleBasedBoardState.GridToPoleIndex(x, z);
                        int p2 = PoleBasedBoardState.GridToPoleIndex(x + 1, z);
                        int p3 = PoleBasedBoardState.GridToPoleIndex(x + 2, z);
                        int p4 = PoleBasedBoardState.GridToPoleIndex(x + 3, z);
                        
                        lines.Add(new LineDefinition(p1, y, p2, y, p3, y, p4, y, $"H({x},{y},{z})"));
                    }
                }
            }

            // Vertical lines (same x, same z, different y)
            for (int x = 0; x < 8; x++)
            {
                for (int z = 0; z < 8; z++)
                {
                    for (int y = 0; y <= 4; y++) // 4 possible vertical lines per pole
                    {
                        int pole = PoleBasedBoardState.GridToPoleIndex(x, z);
                        lines.Add(new LineDefinition(pole, y, pole, y + 1, pole, y + 2, pole, y + 3, $"V({x},{y},{z})"));
                    }
                }
            }

            // Depth lines (same x, same y, different z)
            for (int x = 0; x < 8; x++)
            {
                for (int y = 0; y < 8; y++)
                {
                    for (int z = 0; z <= 4; z++) // 4 possible depth lines per column
                    {
                        int p1 = PoleBasedBoardState.GridToPoleIndex(x, z);
                        int p2 = PoleBasedBoardState.GridToPoleIndex(x, z + 1);
                        int p3 = PoleBasedBoardState.GridToPoleIndex(x, z + 2);
                        int p4 = PoleBasedBoardState.GridToPoleIndex(x, z + 3);
                        
                        lines.Add(new LineDefinition(p1, y, p2, y, p3, y, p4, y, $"D({x},{y},{z})"));
                    }
                }
            }

            // 2D diagonals in XY plane (same z)
            for (int z = 0; z < 8; z++)
            {
                // Diagonal \ (top-left to bottom-right)
                for (int startX = 0; startX <= 4; startX++)
                {
                    for (int startY = 0; startY <= 4; startY++)
                    {
                        int p1 = PoleBasedBoardState.GridToPoleIndex(startX, z);
                        int p2 = PoleBasedBoardState.GridToPoleIndex(startX + 1, z);
                        int p3 = PoleBasedBoardState.GridToPoleIndex(startX + 2, z);
                        int p4 = PoleBasedBoardState.GridToPoleIndex(startX + 3, z);
                        
                        lines.Add(new LineDefinition(p1, startY, p2, startY + 1, p3, startY + 2, p4, startY + 3, $"XY\\{startX},{startY},{z}"));
                    }
                }

                // Diagonal / (top-right to bottom-left)
                for (int startX = 3; startX < 8; startX++)
                {
                    for (int startY = 0; startY <= 4; startY++)
                    {
                        int p1 = PoleBasedBoardState.GridToPoleIndex(startX, z);
                        int p2 = PoleBasedBoardState.GridToPoleIndex(startX - 1, z);
                        int p3 = PoleBasedBoardState.GridToPoleIndex(startX - 2, z);
                        int p4 = PoleBasedBoardState.GridToPoleIndex(startX - 3, z);
                        
                        lines.Add(new LineDefinition(p1, startY, p2, startY + 1, p3, startY + 2, p4, startY + 3, $"XY/{startX},{startY},{z}"));
                    }
                }
            }

            // 2D diagonals in XZ plane (same y)
            for (int y = 0; y < 8; y++)
            {
                // Diagonal \ (front-left to back-right)
                for (int startX = 0; startX <= 4; startX++)
                {
                    for (int startZ = 0; startZ <= 4; startZ++)
                    {
                        int p1 = PoleBasedBoardState.GridToPoleIndex(startX, startZ);
                        int p2 = PoleBasedBoardState.GridToPoleIndex(startX + 1, startZ + 1);
                        int p3 = PoleBasedBoardState.GridToPoleIndex(startX + 2, startZ + 2);
                        int p4 = PoleBasedBoardState.GridToPoleIndex(startX + 3, startZ + 3);
                        
                        lines.Add(new LineDefinition(p1, y, p2, y, p3, y, p4, y, $"XZ\\{startX},{y},{startZ}"));
                    }
                }

                // Diagonal / (front-right to back-left)
                for (int startX = 3; startX < 8; startX++)
                {
                    for (int startZ = 0; startZ <= 4; startZ++)
                    {
                        int p1 = PoleBasedBoardState.GridToPoleIndex(startX, startZ);
                        int p2 = PoleBasedBoardState.GridToPoleIndex(startX - 1, startZ + 1);
                        int p3 = PoleBasedBoardState.GridToPoleIndex(startX - 2, startZ + 2);
                        int p4 = PoleBasedBoardState.GridToPoleIndex(startX - 3, startZ + 3);
                        
                        lines.Add(new LineDefinition(p1, y, p2, y, p3, y, p4, y, $"XZ/{startX},{y},{startZ}"));
                    }
                }
            }

            // 2D diagonals in YZ plane (same x)
            for (int x = 0; x < 8; x++)
            {
                // Diagonal \ (front-bottom to back-top)
                for (int startY = 0; startY <= 4; startY++)
                {
                    for (int startZ = 0; startZ <= 4; startZ++)
                    {
                        int p1 = PoleBasedBoardState.GridToPoleIndex(x, startZ);
                        int p2 = PoleBasedBoardState.GridToPoleIndex(x, startZ + 1);
                        int p3 = PoleBasedBoardState.GridToPoleIndex(x, startZ + 2);
                        int p4 = PoleBasedBoardState.GridToPoleIndex(x, startZ + 3);
                        
                        lines.Add(new LineDefinition(p1, startY, p2, startY + 1, p3, startY + 2, p4, startY + 3, $"YZ\\{x},{startY},{startZ}"));
                    }
                }

                // Diagonal / (front-top to back-bottom)
                for (int startY = 3; startY < 8; startY++)
                {
                    for (int startZ = 0; startZ <= 4; startZ++)
                    {
                        int p1 = PoleBasedBoardState.GridToPoleIndex(x, startZ);
                        int p2 = PoleBasedBoardState.GridToPoleIndex(x, startZ + 1);
                        int p3 = PoleBasedBoardState.GridToPoleIndex(x, startZ + 2);
                        int p4 = PoleBasedBoardState.GridToPoleIndex(x, startZ + 3);
                        
                        lines.Add(new LineDefinition(p1, startY, p2, startY - 1, p3, startY - 2, p4, startY - 3, $"YZ/{x},{startY},{startZ}"));
                    }
                }
            }

            // 3D space diagonals
            // Main diagonal (0,0,0) to (7,7,7) direction
            for (int startX = 0; startX <= 4; startX++)
            {
                for (int startY = 0; startY <= 4; startY++)
                {
                    for (int startZ = 0; startZ <= 4; startZ++)
                    {
                        int p1 = PoleBasedBoardState.GridToPoleIndex(startX, startZ);
                        int p2 = PoleBasedBoardState.GridToPoleIndex(startX + 1, startZ + 1);
                        int p3 = PoleBasedBoardState.GridToPoleIndex(startX + 2, startZ + 2);
                        int p4 = PoleBasedBoardState.GridToPoleIndex(startX + 3, startZ + 3);
                        
                        lines.Add(new LineDefinition(p1, startY, p2, startY + 1, p3, startY + 2, p4, startY + 3, $"3D\\{startX},{startY},{startZ}"));
                    }
                }
            }

            // Anti-diagonal (7,0,0) to (0,7,7) direction
            for (int startX = 3; startX < 8; startX++)
            {
                for (int startY = 0; startY <= 4; startY++)
                {
                    for (int startZ = 0; startZ <= 4; startZ++)
                    {
                        int p1 = PoleBasedBoardState.GridToPoleIndex(startX, startZ);
                        int p2 = PoleBasedBoardState.GridToPoleIndex(startX - 1, startZ + 1);
                        int p3 = PoleBasedBoardState.GridToPoleIndex(startX - 2, startZ + 2);
                        int p4 = PoleBasedBoardState.GridToPoleIndex(startX - 3, startZ + 3);
                        
                        lines.Add(new LineDefinition(p1, startY, p2, startY + 1, p3, startY + 2, p4, startY + 3, $"3D/{startX},{startY},{startZ}"));
                    }
                }
            }

            Debug.Log($"Precomputed {lines.Count} possible 4-in-a-row lines");
        }

        [ContextMenu("Test Detection")]
        private void TestDetection()
        {
            ScanAll();
        }
    }
}
