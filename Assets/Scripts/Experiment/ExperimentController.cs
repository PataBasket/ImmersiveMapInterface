using System.Collections.Generic;
using UnityEngine;
using ImmersiveMapInterface.Experiment.Logging;
using ImmersiveMapInterface.Experiment.Selection;
using ImmersiveMapInterface.Board;
using ImmersiveMapInterface.Visualization;

namespace ImmersiveMapInterface.Experiment
{
    public class ExperimentController : MonoBehaviour
    {
        [Header("Config & Systems")]
        public ExperimentConfig config;
        public ConditionManager conditionManager;
        public BoardPopulationService populationService;
        public ExperimentLogger logger;
        public SelectionSystem selection;

        [Header("Visual Generators")]
        [Tooltip("Ground board generators that should rebuild their pieces whenever GenerateBoard is pressed.")]
        public PoleBasedBoardGenerator[] boardGenerators;
        [Tooltip("Miniature board generators that should refresh after GenerateBoard.")]
        public MiniaturePoleBoardGenerator[] miniatureGenerators;
        [Tooltip("Automatically call GeneratePieces on the board generators after GenerateBoard().")]
        public bool regenerateBoardPieces = true;
        [Tooltip("Automatically call EnsureGenerated on miniature generators after GenerateBoard().")]
        public bool regenerateMiniatures = true;

        private void Reset()
        {
            if (config == null)
            {
                // Try find a ScriptableObject in Resources (optional) – otherwise leave null
            }
            if (conditionManager == null) conditionManager = FindObjectOfType<ConditionManager>();
            if (populationService == null) populationService = FindObjectOfType<BoardPopulationService>();
            if (logger == null) logger = FindObjectOfType<ExperimentLogger>();
            if (selection == null) selection = FindObjectOfType<SelectionSystem>();
            if (boardGenerators == null || boardGenerators.Length == 0)
            {
                boardGenerators = FindObjectsOfType<PoleBasedBoardGenerator>();
            }
            if (miniatureGenerators == null || miniatureGenerators.Length == 0)
            {
                miniatureGenerators = FindObjectsOfType<MiniaturePoleBoardGenerator>();
            }
        }

        private void OnEnable()
        {
            if (selection != null)
            {
                selection.OnWrongAttemptEvent += HandleWrongAttempt;
                selection.OnCorrectLineFoundEvent += HandleCorrectLineFound;
                selection.OnSelectionCanceledEvent += HandleSelectionCanceled;
            }
        }

        private void OnDisable()
        {
            if (selection != null)
            {
                selection.OnWrongAttemptEvent -= HandleWrongAttempt;
                selection.OnCorrectLineFoundEvent -= HandleCorrectLineFound;
                selection.OnSelectionCanceledEvent -= HandleSelectionCanceled;
            }
        }

        public void ApplyCondition()
        {
            EnsureCoreReferences();
            if (conditionManager != null)
            {
                conditionManager.ApplyCondition();
            }
        }

        public void GenerateBoard()
        {
            EnsureCoreReferences();
            if (populationService != null)
            {
                populationService.GenerateFromPattern();
                EnsureGeneratorReferences();
                if (regenerateBoardPieces)
                {
                    RegenerateBoardPieces();
                }
                if (regenerateMiniatures)
                {
                    RegenerateMiniatures();
                }
            }
        }

        public void StartSession()
        {
            if (logger != null) logger.StartSession();
        }

        public void FinishSession()
        {
            EnsureCoreReferences();
            if (logger != null) logger.FinishSession();
        }

        private void HandleWrongAttempt()
        {
            if (logger != null) logger.OnWrongAttempt();
        }

        private void HandleCorrectLineFound()
        {
            if (logger != null) logger.OnCorrectLineFound();
        }

        private void HandleSelectionCanceled()
        {
            if (logger != null) logger.OnSelectionCanceled();
        }

        private void EnsureCoreReferences()
        {
            if (config == null)
            {
                var configs = Resources.FindObjectsOfTypeAll<ExperimentConfig>();
                if (configs != null && configs.Length > 0) config = configs[0];
            }
            if (conditionManager == null) conditionManager = FindObjectOfType<ConditionManager>();
            if (populationService == null) populationService = FindObjectOfType<BoardPopulationService>();
            if (logger == null) logger = FindObjectOfType<ExperimentLogger>();
            if (selection == null) selection = FindObjectOfType<SelectionSystem>();
        }

        private void EnsureGeneratorReferences()
        {
            boardGenerators = RefreshGeneratorArray(boardGenerators);
            miniatureGenerators = RefreshGeneratorArray(miniatureGenerators);
        }

        private T[] RefreshGeneratorArray<T>(T[] current) where T : Object
        {
            var list = new List<T>();
            if (current != null)
            {
                foreach (var item in current)
                {
                    if (item != null) list.Add(item);
                }
            }

            if (list.Count == 0)
            {
                list.AddRange(FindObjectsOfType<T>());
            }

            return list.ToArray();
        }

        private void RegenerateBoardPieces()
        {
            if (boardGenerators == null) return;
            foreach (var generator in boardGenerators)
            {
                if (generator == null) continue;
                generator.GeneratePieces();
            }
        }

        private void RegenerateMiniatures()
        {
            if (miniatureGenerators == null) return;
            foreach (var miniature in miniatureGenerators)
            {
                if (miniature == null) continue;
                miniature.EnsureGenerated();
            }
        }
    }
}
