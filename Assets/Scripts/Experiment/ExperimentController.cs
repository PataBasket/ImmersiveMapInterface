using UnityEngine;
using ImmersiveMapInterface.Experiment.Logging;
using ImmersiveMapInterface.Experiment.Selection;

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
        }

        private void OnEnable()
        {
            if (selection != null)
            {
                selection.OnWrongAttemptEvent += HandleWrongAttempt;
                selection.OnCorrectLineFoundEvent += HandleCorrectLineFound;
            }
        }

        private void OnDisable()
        {
            if (selection != null)
            {
                selection.OnWrongAttemptEvent -= HandleWrongAttempt;
                selection.OnCorrectLineFoundEvent -= HandleCorrectLineFound;
            }
        }

        public void ApplyCondition()
        {
            if (conditionManager != null)
            {
                conditionManager.ApplyCondition();
            }
        }

        public void GenerateBoard()
        {
            if (populationService != null)
            {
                populationService.GenerateFromPattern();
            }
        }

        public void StartSession()
        {
            if (logger != null) logger.StartSession();
        }

        public void FinishSession()
        {
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
    }
}

