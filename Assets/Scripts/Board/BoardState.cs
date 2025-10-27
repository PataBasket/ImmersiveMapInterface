using System;
using System.Collections.Generic;
using UnityEngine;

namespace ImmersiveMapInterface.Board
{
	/// <summary>
	/// Holds authoritative state for an 8×8×8 two-color board.
	/// Indexing convention: (x,y,z) each in [0,7], linear index = x + 8*(y + 8*z).
	/// </summary>
	public class BoardState : MonoBehaviour
	{
		public const int Size = 8;
		public const int CellCount = Size * Size * Size; // 512

		public enum Cell
		{
			Empty = 0,
			ColorA = 1,
			ColorB = 2
		}

		[SerializeField]
		private Cell[] cells = new Cell[CellCount];

		public event Action<int, Cell>? OnCellChanged;
		public event Action? OnBoardReset;

		public Cell GetCell(int x, int y, int z)
		{
			if (!IsInBounds(x, y, z)) return Cell.Empty;
			return cells[ToIndex(x, y, z)];
		}

		public void SetCell(int x, int y, int z, Cell value)
		{
			if (!IsInBounds(x, y, z)) return;
			int idx = ToIndex(x, y, z);
			if (cells[idx] == value) return;
			cells[idx] = value;
			OnCellChanged?.Invoke(idx, value);
		}

		public void Clear(BoardState.Cell fill = Cell.Empty)
		{
			for (int i = 0; i < cells.Length; i++)
			{
				cells[i] = fill;
			}
			OnBoardReset?.Invoke();
		}

		public Cell GetCellFromIndex(int index)
		{
			if ((uint)index >= cells.Length) return Cell.Empty;
			return cells[index];
		}

		public static bool IsInBounds(int x, int y, int z)
		{
			return (uint)x < Size && (uint)y < Size && (uint)z < Size;
		}

		public static int ToIndex(int x, int y, int z)
		{
			return x + Size * (y + Size * z);
		}

		public static void FromIndex(int index, out int x, out int y, out int z)
		{
			z = index / (Size * Size);
			int rem = index - z * Size * Size;
			y = rem / Size;
			x = rem - y * Size;
		}

		/// <summary>
		/// Returns localPosition for a given cell index using unit spacing and origin at (0,0,0).
		/// Consumers can parent this under a root and apply centering/scale externally.
		/// </summary>
		public static Vector3 IndexToLocalPosition(int index, float spacing)
		{
			FromIndex(index, out int x, out int y, out int z);
			return new Vector3(x * spacing, y * spacing, z * spacing);
		}
	}
}

