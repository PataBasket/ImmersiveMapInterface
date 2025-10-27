using System;
using System.Collections.Generic;
using UnityEngine;

namespace ImmersiveMapInterface.Board
{
    /// <summary>
    /// Manages 8x8x8 board state using 8x8 poles with 8 pieces each.
    /// Each pole has 8 slots (0-7) for pieces, creating a 3D grid.
    /// </summary>
    public class PoleBasedBoardState : MonoBehaviour
    {
        public const int PoleCount = 8 * 8; // 64 poles
        public const int PiecesPerPole = 8; // 8 pieces per pole
        public const int TotalPieces = PoleCount * PiecesPerPole; // 512 pieces

        public enum PieceColor
        {
            Empty = 0,
            White = 1,
            Black = 2
        }

        [SerializeField] private PieceColor[,] polePieces = new PieceColor[PoleCount, PiecesPerPole];

        public event Action<int, int, PieceColor>? OnPieceChanged; // poleIndex, slotIndex, color
        public event Action? OnBoardReset;

        public PieceColor GetPiece(int poleIndex, int slotIndex)
        {
            if (!IsValidPoleSlot(poleIndex, slotIndex)) return PieceColor.Empty;
            return polePieces[poleIndex, slotIndex];
        }

        public void SetPiece(int poleIndex, int slotIndex, PieceColor color)
        {
            if (!IsValidPoleSlot(poleIndex, slotIndex)) return;
            if (polePieces[poleIndex, slotIndex] == color) return;
            
            polePieces[poleIndex, slotIndex] = color;
            OnPieceChanged?.Invoke(poleIndex, slotIndex, color);
        }

        public void Clear(PieceColor fill = PieceColor.Empty)
        {
            for (int pole = 0; pole < PoleCount; pole++)
            {
                for (int slot = 0; slot < PiecesPerPole; slot++)
                {
                    polePieces[pole, slot] = fill;
                }
            }
            OnBoardReset?.Invoke();
        }

        public static bool IsValidPoleSlot(int poleIndex, int slotIndex)
        {
            return (uint)poleIndex < PoleCount && (uint)slotIndex < PiecesPerPole;
        }

        public static void PoleIndexToGrid(int poleIndex, out int x, out int z)
        {
            x = poleIndex % 8;
            z = poleIndex / 8;
        }

        public static int GridToPoleIndex(int x, int z)
        {
            return z * 8 + x;
        }

        public static bool IsValidGrid(int x, int z)
        {
            return (uint)x < 8 && (uint)z < 8;
        }

        /// <summary>
        /// Get 3D world position for a piece at given pole and slot.
        /// Assumes poles are arranged in 8x8 grid with spacing.
        /// </summary>
		public static Vector3 GetPieceWorldPosition(int poleIndex, int slotIndex, Transform poleParent, float poleSpacing = 2.0f, float pieceSpacing = 1.0f)
        {
            PoleIndexToGrid(poleIndex, out int x, out int z);
			// Center the 8x8 grid around poleParent.position
			float extent = (8 - 1) * poleSpacing * 0.5f; // half width across 7 gaps
			Vector3 polePos = poleParent.position + new Vector3(x * poleSpacing - extent, 0, z * poleSpacing - extent);
			
            // Add vertical offset for slot
            float yOffset = (slotIndex + 0.5f) * pieceSpacing; // +0.5f to center on pole
            return polePos + new Vector3(0, yOffset, 0);
        }
    }
}
