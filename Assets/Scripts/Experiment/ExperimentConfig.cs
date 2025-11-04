using UnityEngine;

namespace ImmersiveMapInterface.Experiment
{
    public enum ExperimentCondition
    {
        Bird,
        Internal,
        InternalWithMiniature
    }

    [CreateAssetMenu(fileName = "ExperimentConfig", menuName = "ImmersiveMap/Experiment Config", order = 2)]
    public class ExperimentConfig : ScriptableObject
    {
        [Header("Session")]
        public string subjectId = "";
        public ExperimentCondition condition = ExperimentCondition.Bird;
        public PatternDefinition pattern;

        [Header("Logging (GAS)")]
        public string gasUrl = ""; // to be provided later
        public string apiKey = ""; // optional header value
        public bool saveCsvFallback = true;
    }
}

