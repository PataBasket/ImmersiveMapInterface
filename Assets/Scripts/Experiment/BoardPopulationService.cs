using System;
using System.Collections.Generic;
using UnityEngine;
using ImmersiveMapInterface.Board;

namespace ImmersiveMapInterface.Experiment
{
    public class BoardPopulationService : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private PoleBasedBoardState boardState;
        [SerializeField] private ExperimentConfig experimentConfig;

        [Header("Generation")]
        [SerializeField] private int maxValidationRetries = 200;
        [SerializeField] private int randomSeed = 12345;
        [SerializeField] private bool useTimeSeed = true;

        private List<(int p1,int s1,int p2,int s2,int p3,int s3,int p4,int s4)> cachedLines;

        private void Awake()
        {
            if (boardState == null) boardState = GetComponent<PoleBasedBoardState>();
            if (cachedLines == null) cachedLines = Lines3DUtility.GenerateAllLines();
        }

        [ContextMenu("Generate From Pattern")]
        public void GenerateFromPattern()
        {
            if (boardState == null || experimentConfig == null || experimentConfig.pattern == null)
            {
                Debug.LogError("BoardPopulationService: Missing references (boardState/experimentConfig/pattern)");
                return;
            }

            // Ensure lines cache is initialized even when called from editor context menu (Awake may not have run).
            if (cachedLines == null)
            {
                cachedLines = Lines3DUtility.GenerateAllLines();
            }

            int seed = useTimeSeed ? Environment.TickCount : randomSeed;
            var rng = new System.Random(seed);

            // Prepare target cells set (must be white)
            var targetCells = new HashSet<(int pole,int slot)>();
            foreach (var c in experimentConfig.pattern.EnumerateAllTargetCells())
            {
                targetCells.Add((c.poleIndex, c.slotIndex));
            }

            // Validate target cells count
            if (targetCells.Count != 12)
            {
                Debug.LogWarning($"Pattern yielded {targetCells.Count} unique cells; expected 12. Overlaps? Abort generation.");
                return;
            }

            int attempts = 0;
            while (attempts++ < maxValidationRetries)
            {
                boardState.Clear(PoleBasedBoardState.PieceColor.Empty);

                // Apply target whites first
                foreach (var cell in targetCells)
                {
                    boardState.SetPiece(cell.pole, cell.slot, PoleBasedBoardState.PieceColor.White);
                }

                // Fill remaining to achieve 50/50
                int total = PoleBasedBoardState.TotalPieces; // 512
                int targetWhiteTotal = total / 2; // 256
                int currentWhite = targetCells.Count; // 12
                int remainingWhite = Mathf.Max(0, targetWhiteTotal - currentWhite); // 244
                int remainingBlack = total - targetWhiteTotal; // 256

                // Build list of remaining cells
                var remaining = new List<(int pole,int slot)>(total - targetCells.Count);
                for (int pole = 0; pole < PoleBasedBoardState.PoleCount; pole++)
                {
                    for (int slot = 0; slot < PoleBasedBoardState.PiecesPerPole; slot++)
                    {
                        if (!targetCells.Contains((pole, slot))) remaining.Add((pole, slot));
                    }
                }

                // Shuffle
                for (int i = remaining.Count - 1; i > 0; i--)
                {
                    int j = rng.Next(i + 1);
                    var tmp = remaining[i];
                    remaining[i] = remaining[j];
                    remaining[j] = tmp;
                }

                // Assign additional whites then blacks
                int assigned = 0;
                for (int i = 0; i < remaining.Count && assigned < remainingWhite; i++)
                {
                    var c = remaining[i];
                    boardState.SetPiece(c.pole, c.slot, PoleBasedBoardState.PieceColor.White);
                    assigned++;
                }
                for (int i = assigned; i < remaining.Count; i++)
                {
                    var c = remaining[i];
                    boardState.SetPiece(c.pole, c.slot, PoleBasedBoardState.PieceColor.Black);
                }

                // Validate: only the three target lines exist as four-in-a-row, none others (white or black)
                if (ValidateOnlyTargets(boardState, targetCells))
                {
                    Debug.Log($"Board generated successfully in {attempts} attempt(s) [seed={seed}].");
                    return;
                }
                // else retry with new seed
                seed = rng.Next();
                rng = new System.Random(seed);
            }

            Debug.LogWarning($"BoardPopulationService: Failed to validate board within {maxValidationRetries} attempts.");
        }

        private bool ValidateOnlyTargets(PoleBasedBoardState state, HashSet<(int pole,int slot)> targetCells)
        {
            if (cachedLines == null)
            {
                cachedLines = Lines3DUtility.GenerateAllLines();
            }
            // Construct a quick lookup to check if a line equals one of the three targets
            var targetLineSets = BuildTargetLineSets(targetCells);

            foreach (var line in cachedLines)
            {
                var c1 = state.GetPiece(line.p1, line.s1);
                if (c1 == PoleBasedBoardState.PieceColor.Empty) continue;
                var c2 = state.GetPiece(line.p2, line.s2);
                var c3 = state.GetPiece(line.p3, line.s3);
                var c4 = state.GetPiece(line.p4, line.s4);
                if (c1 == c2 && c2 == c3 && c3 == c4)
                {
                    // Is this line one of the targets? Compare as unordered set of 4 cells
                    var set = new HashSet<(int,int)>(new[]{
                        (line.p1, line.s1),
                        (line.p2, line.s2),
                        (line.p3, line.s3),
                        (line.p4, line.s4),
                    });
                    bool isTarget = false;
                    foreach (var target in targetLineSets)
                    {
                        if (target.SetEquals(set)) { isTarget = true; break; }
                    }
                    if (!isTarget) return false; // extra unintended 4-in-a-row found
                }
            }
            return true;
        }

        private static List<HashSet<(int pole,int slot)>> BuildTargetLineSets(HashSet<(int pole,int slot)> targetCells)
        {
            // Split targetCells into three groups of four by reconstructing from endpoints repeatedly is complex; instead, we
            // derive from unique contiguous groups along valid directions using a simple scan.
            // Since targets are non-overlapping and length-4, we can greedily extract groups of 4 by checking neighbors.

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
                // pick one cell as origin
                var enumerator = remaining.GetEnumerator();
                if (!enumerator.MoveNext()) break;
                var cell = enumerator.Current; // (pole,slot)
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
                        // consume
                        foreach (var g in group) remaining.Remove(g);
                        result.Add(group);
                        foundGroup = true;
                        break;
                    }
                }
                if (!foundGroup)
                {
                    // If we can't form a group from this origin, remove it to avoid infinite loop
                    remaining.Remove(cell);
                }
            }
            return result;
        }
    }
}
