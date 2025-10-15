using System;
using System.Collections.Generic;
using UnityEngine;

namespace ImmersiveMapInterface.Board
{
	/// <summary>
	/// Detects 4-in-a-row on an 8×8×8 grid by iterating precomputed line index sets.
	/// </summary>
	public class FourInARowDetector : MonoBehaviour
	{
		[SerializeField] private BoardState boardState;

		private List<int[]> lines = new List<int[]>(
			capacity: 8 * 8 * (8 - 3) * 7 // rough upper bound
		);

		public event Action<int[], BoardState.Cell>? OnLineFound;

		private void Awake()
		{
			if (boardState == null) boardState = GetComponent<BoardState>();
			PrecomputeLines();
		}

		public void ScanAll()
		{
			if (boardState == null) return;
			foreach (var line in lines)
			{
				BoardState.Cell c0 = boardState.GetCellFromIndex(line[0]);
				if (c0 == BoardState.Cell.Empty) continue;
				bool allSame = true;
				for (int i = 1; i < 4; i++)
				{
					if (boardState.GetCellFromIndex(line[i]) != c0)
					{
						allSame = false;
						break;
					}
				}
				if (allSame)
				{
					OnLineFound?.Invoke(line, c0);
				}
			}
		}

		private void PrecomputeLines()
		{
			lines.Clear();
			// Directions to check: 13 unique directions in 3D for straight/diagonals
			Vector3Int[] dirs = new[]
			{
				new Vector3Int(1,0,0), new Vector3Int(0,1,0), new Vector3Int(0,0,1),
				new Vector3Int(1,1,0), new Vector3Int(1,-1,0),
				new Vector3Int(1,0,1), new Vector3Int(1,0,-1),
				new Vector3Int(0,1,1), new Vector3Int(0,1,-1),
				new Vector3Int(1,1,1), new Vector3Int(1,1,-1), new Vector3Int(1,-1,1), new Vector3Int(1,-1,-1)
			};

			for (int z = 0; z < BoardState.Size; z++)
			for (int y = 0; y < BoardState.Size; y++)
			for (int x = 0; x < BoardState.Size; x++)
			{
				var origin = new Vector3Int(x,y,z);
				foreach (var d in dirs)
				{
					// ensure 3 more steps fit in bounds
					var end = origin + d * 3;
					if (!InBounds(end)) continue;
					int[] segment = new int[4];
					for (int i = 0; i < 4; i++)
					{
						var p = origin + d * i;
						segment[i] = BoardState.ToIndex(p.x, p.y, p.z);
					}
					lines.Add(segment);
				}
			}
		}

		private static bool InBounds(Vector3Int p)
		{
			return (uint)p.x < BoardState.Size && (uint)p.y < BoardState.Size && (uint)p.z < BoardState.Size;
		}
	}
}

