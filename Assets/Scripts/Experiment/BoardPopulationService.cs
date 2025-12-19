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

        [Header("Options")]
        [SerializeField] private bool logDetails = false;
        [SerializeField] private bool autoGenerateOnPlay = true;
        [SerializeField] private bool skipAutoGenerationIfBoardHasPieces = true;

        private List<(int p1, int s1, int p2, int s2, int p3, int s3, int p4, int s4)> cachedLines;

        private void Awake()
        {
            if (boardState == null) boardState = GetComponent<PoleBasedBoardState>();
            EnsureLinesCache();
        }

        private void Start()
        {
            if (!Application.isPlaying || !autoGenerateOnPlay) return;
            if (skipAutoGenerationIfBoardHasPieces && boardState != null && boardState.HasAnyColoredPieces())
            {
                if (logDetails)
                {
                    Debug.Log("BoardPopulationService: auto generation skipped because board already has pieces.", this);
                }
                return;
            }
            GenerateFromPattern();
        }

        private void EnsureLinesCache()
        {
            if (cachedLines == null)
            {
                cachedLines = Lines3DUtility.GenerateAllLines();
            }
        }

        [ContextMenu("Generate From Pattern")]
        public void GenerateFromPattern()
        {
            if (boardState == null || experimentConfig == null || experimentConfig.pattern == null)
            {
                Debug.LogError("BoardPopulationService: Missing references (boardState/experimentConfig/pattern).");
                return;
            }

            EnsureLinesCache();

            if (!TryBuildTargetLineData(experimentConfig.pattern, out var targetLineSets, out var targetCells))
            {
                return;
            }

            var colors = new PoleBasedBoardState.PieceColor[PoleBasedBoardState.PoleCount, PoleBasedBoardState.PiecesPerPole];
            FillParityPattern(colors);
            ApplyTargetLines(colors, targetLineSets);

            int desiredWhite = PoleBasedBoardState.TotalPieces / 2; // 256

            BalanceWhiteCount(colors, targetLineSets, targetCells, desiredWhite);

            if (!EliminateExtraWhiteLines(colors, targetLineSets, targetCells))
            {
                Debug.LogWarning("BoardPopulationService: Unable to eliminate extra white lines without touching target cells. Adjust the pattern or algorithm.");
                return;
            }

            // After elimination, we may have fewer whites than desired. Try to restore towards 50/50 safely.
            AttemptRestoreWhiteCount(colors, targetLineSets, targetCells, desiredWhite);

            if (!ValidateColors(colors, targetLineSets))
            {
                Debug.LogWarning("BoardPopulationService: Deterministic population failed validation (extra four-in-a-row detected).");
                return;
            }

            ApplyToBoardState(colors);
            Debug.Log("BoardPopulationService: Generated board using parity-based deterministic population.");
        }

        private bool TryBuildTargetLineData(PatternDefinition pattern, out List<HashSet<(int pole, int slot)>> targetLineSets, out HashSet<(int pole, int slot)> targetCells)
        {
            targetLineSets = new List<HashSet<(int pole, int slot)>>();
            targetCells = new HashSet<(int pole, int slot)>();

            var lineDefs = pattern.Lines;
            if (lineDefs == null || lineDefs.Count == 0)
            {
                Debug.LogWarning("BoardPopulationService: Pattern has no target lines defined.");
                return false;
            }

            for (int i = 0; i < lineDefs.Count; i++)
            {
                if (!PatternDefinition.TryGetLineCells(lineDefs[i], out var cells, out string error))
                {
                    Debug.LogWarning($"BoardPopulationService: Pattern line {i + 1} invalid. {error}");
                    targetLineSets.Clear();
                    targetCells.Clear();
                    return false;
                }

                var set = new HashSet<(int pole, int slot)>();
                foreach (var cell in cells)
                {
                    var tuple = (pole: cell.poleIndex, slot: cell.slotIndex);
                    if (!targetCells.Add(tuple))
                    {
                        Debug.LogWarning($"BoardPopulationService: Pattern lines overlap at pole {tuple.pole}, slot {tuple.slot}.");
                        targetLineSets.Clear();
                        targetCells.Clear();
                        return false;
                    }
                    set.Add(tuple);
                }
                targetLineSets.Add(set);
            }

            if (logDetails)
            {
                Debug.Log($"BoardPopulationService: Pattern validated successfully with {targetLineSets.Count} lines.");
            }

            return true;
        }

        private static void FillParityPattern(PoleBasedBoardState.PieceColor[,] colors)
        {
            for (int pole = 0; pole < PoleBasedBoardState.PoleCount; pole++)
            {
                PoleBasedBoardState.PoleIndexToGrid(pole, out int x, out int z);
                for (int slot = 0; slot < PoleBasedBoardState.PiecesPerPole; slot++)
                {
                    int parity = (x + z + slot) & 1;
                    colors[pole, slot] = parity == 0 ? PoleBasedBoardState.PieceColor.White : PoleBasedBoardState.PieceColor.Black;
                }
            }
        }

        private static void ApplyTargetLines(PoleBasedBoardState.PieceColor[,] colors, List<HashSet<(int pole, int slot)>> targetLineSets)
        {
            foreach (var line in targetLineSets)
            {
                foreach (var cell in line)
                {
                    colors[cell.pole, cell.slot] = PoleBasedBoardState.PieceColor.White;
                }
            }
        }

        private bool BalanceWhiteCount(PoleBasedBoardState.PieceColor[,] colors, List<HashSet<(int pole, int slot)>> targetLineSets, HashSet<(int pole, int slot)> targetCells, int desiredWhite)
        {
            int whiteCount = CountColor(colors, PoleBasedBoardState.PieceColor.White);
            if (whiteCount <= desiredWhite)
            {
                if (whiteCount < desiredWhite)
                {
                    Debug.LogWarning($"BoardPopulationService: White count {whiteCount} below desired {desiredWhite}. Using current distribution.");
                }
                return true;
            }

            int excessWhite = whiteCount - desiredWhite;
            var candidates = CollectWhiteCandidates(colors, targetCells);
            Shuffle(candidates, new System.Random());
            int flips = 0;

            foreach (var candidate in candidates)
            {
                colors[candidate.pole, candidate.slot] = PoleBasedBoardState.PieceColor.Black;
                flips++;
                excessWhite--;
                if (excessWhite == 0) break;
            }

            if (excessWhite > 0)
            {
                int finalWhite = whiteCount - flips;
                Debug.LogWarning($"BoardPopulationService: Could not reach 50/50. Remaining excess whites: {excessWhite}. Final white count={finalWhite} (desired {desiredWhite}).");
            }

            return true;
        }

        private bool EliminateExtraWhiteLines(PoleBasedBoardState.PieceColor[,] colors, List<HashSet<(int pole, int slot)>> allowedWhiteLines, HashSet<(int pole, int slot)> targetCells)
        {
            EnsureLinesCache();
            int safety = 0;
            int limit = cachedLines.Count * 2;
            while (safety++ < limit)
            {
                if (!TryFindExtraWhiteLine(colors, allowedWhiteLines, out var lineCells))
                {
                    return true; // no extra lines remain
                }

                bool flipped = false;
                foreach (var cell in lineCells)
                {
                    if (targetCells.Contains(cell)) continue;
                    colors[cell.pole, cell.slot] = PoleBasedBoardState.PieceColor.Black;
                    flipped = true;
                    break;
                }

                if (!flipped)
                {
                    // All cells are target cells; cannot break this line without affecting targets
                    return false;
                }
            }

            Debug.LogWarning("BoardPopulationService: EliminateExtraWhiteLines reached iteration cap. Result may still contain extra white lines.");
            return false;
        }

        private bool TryFindExtraWhiteLine(PoleBasedBoardState.PieceColor[,] colors, List<HashSet<(int pole, int slot)>> allowedWhiteLines, out (int pole, int slot)[] lineCells)
        {
            EnsureLinesCache();
            foreach (var line in cachedLines)
            {
                var c1 = colors[line.p1, line.s1];
                if (c1 != PoleBasedBoardState.PieceColor.White) continue;
                var c2 = colors[line.p2, line.s2];
                var c3 = colors[line.p3, line.s3];
                var c4 = colors[line.p4, line.s4];
                if (!(c2 == PoleBasedBoardState.PieceColor.White && c3 == PoleBasedBoardState.PieceColor.White && c4 == PoleBasedBoardState.PieceColor.White))
                    continue;
                if (IsAllowedLine(line, allowedWhiteLines)) continue;

                lineCells = new[]
                {
                    (line.p1, line.s1),
                    (line.p2, line.s2),
                    (line.p3, line.s3),
                    (line.p4, line.s4)
                };
                return true;
            }

            lineCells = Array.Empty<(int pole, int slot)>();
            return false;
        }

        private bool ValidateColors(PoleBasedBoardState.PieceColor[,] colors, List<HashSet<(int pole, int slot)>> allowedWhiteLines)
        {
            EnsureLinesCache();

            foreach (var line in cachedLines)
            {
                var c1 = colors[line.p1, line.s1];
                if (c1 == PoleBasedBoardState.PieceColor.Empty) continue;
                var c2 = colors[line.p2, line.s2];
                var c3 = colors[line.p3, line.s3];
                var c4 = colors[line.p4, line.s4];

                if (c1 == c2 && c2 == c3 && c3 == c4)
                {
                    if (c1 == PoleBasedBoardState.PieceColor.White)
                    {
                        if (!IsAllowedLine(line, allowedWhiteLines)) return false;
                    }
                }
            }

            return true;
        }

        private void AttemptRestoreWhiteCount(PoleBasedBoardState.PieceColor[,] colors, List<HashSet<(int pole, int slot)>> allowedWhiteLines, HashSet<(int pole, int slot)> targetCells, int desiredWhite)
        {
            int whiteCount = CountColor(colors, PoleBasedBoardState.PieceColor.White);
            if (whiteCount >= desiredWhite) return;

            // Collect candidate blacks that are not part of target cells
            var candidates = new List<(int pole, int slot)>();
            for (int pole = 0; pole < PoleBasedBoardState.PoleCount; pole++)
            {
                for (int slot = 0; slot < PoleBasedBoardState.PiecesPerPole; slot++)
                {
                    var tuple = (pole: pole, slot: slot);
                    if (targetCells.Contains(tuple)) continue;
                    if (colors[pole, slot] == PoleBasedBoardState.PieceColor.Black)
                    {
                        candidates.Add(tuple);
                    }
                }
            }

            Shuffle(candidates, new System.Random());
            foreach (var c in candidates)
            {
                if (whiteCount >= desiredWhite) break;
                // Tentatively flip to white; ensure it doesn't create extra white lines
                colors[c.pole, c.slot] = PoleBasedBoardState.PieceColor.White;
                if (ValidateColors(colors, allowedWhiteLines))
                {
                    whiteCount++;
                }
                else
                {
                    colors[c.pole, c.slot] = PoleBasedBoardState.PieceColor.Black; // revert if invalid
                }
            }

            if (whiteCount < desiredWhite)
            {
                Debug.LogWarning($"BoardPopulationService: Could not restore to 50/50. Final white count={whiteCount} (desired {desiredWhite}).");
            }
        }

        private static bool IsAllowedLine((int p1, int s1, int p2, int s2, int p3, int s3, int p4, int s4) line, List<HashSet<(int pole, int slot)>> allowedWhiteLines)
        {
            foreach (var target in allowedWhiteLines)
            {
                if (target.Contains((line.p1, line.s1)) &&
                    target.Contains((line.p2, line.s2)) &&
                    target.Contains((line.p3, line.s3)) &&
                    target.Contains((line.p4, line.s4)))
                {
                    return true;
                }
            }
            return false;
        }

        private void ApplyToBoardState(PoleBasedBoardState.PieceColor[,] colors)
        {
            boardState.Clear(PoleBasedBoardState.PieceColor.Empty);
            for (int pole = 0; pole < PoleBasedBoardState.PoleCount; pole++)
            {
                for (int slot = 0; slot < PoleBasedBoardState.PiecesPerPole; slot++)
                {
                    var color = colors[pole, slot];
                    if (color != PoleBasedBoardState.PieceColor.Empty)
                    {
                        boardState.SetPiece(pole, slot, color);
                    }
                }
            }
        }

        private static int CountColor(PoleBasedBoardState.PieceColor[,] colors, PoleBasedBoardState.PieceColor target)
        {
            int count = 0;
            for (int pole = 0; pole < PoleBasedBoardState.PoleCount; pole++)
            {
                for (int slot = 0; slot < PoleBasedBoardState.PiecesPerPole; slot++)
                {
                    if (colors[pole, slot] == target) count++;
                }
            }
            return count;
        }

        private static List<(int pole, int slot)> CollectWhiteCandidates(PoleBasedBoardState.PieceColor[,] colors, HashSet<(int pole, int slot)> targetCells)
        {
            var list = new List<(int pole, int slot)>();
            for (int pole = 0; pole < PoleBasedBoardState.PoleCount; pole++)
            {
                for (int slot = 0; slot < PoleBasedBoardState.PiecesPerPole; slot++)
                {
                    var tuple = (pole: pole, slot: slot);
                    if (!targetCells.Contains(tuple) && colors[pole, slot] == PoleBasedBoardState.PieceColor.White)
                    {
                        list.Add(tuple);
                    }
                }
            }
            return list;
        }

        private static void Shuffle<T>(IList<T> list, System.Random rng)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
        }
    }
}
