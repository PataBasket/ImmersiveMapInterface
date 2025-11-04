using UnityEditor;
using UnityEngine;
using ImmersiveMapInterface.Experiment;

public class ExperimentControlWindow : EditorWindow
{
    private ExperimentConfig config;
    private ExperimentController controller;
    private BoardPopulationService pop;
    private ConditionManager cond;

    [MenuItem("Tools/Experiment/Control Panel")] 
    public static void Open()
    {
        GetWindow<ExperimentControlWindow>("Experiment Control");
    }

    private void OnEnable()
    {
        FindSceneRefs();
    }

    private void OnHierarchyChange()
    {
        FindSceneRefs();
        Repaint();
    }

    private void OnProjectChange()
    {
        Repaint();
    }

    private void FindSceneRefs()
    {
        controller = FindObjectOfType<ExperimentController>();
        pop = FindObjectOfType<BoardPopulationService>();
        cond = FindObjectOfType<ConditionManager>();
        if (controller != null && config == null) config = controller.config;
        if (config == null && controller != null) config = controller.config;
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField("Experiment Control", EditorStyles.boldLabel);
        EditorGUILayout.Space(4);

        config = (ExperimentConfig)EditorGUILayout.ObjectField("Config", config, typeof(ExperimentConfig), false);
        if (controller != null) controller.config = config;
        if (cond != null && cond.config == null && config != null) cond.config = config;
        if (pop != null && config != null)
        {
            var so = new SerializedObject(pop);
            so.Update();
            so.FindProperty("experimentConfig").objectReferenceValue = config;
            so.ApplyModifiedProperties();
        }

        if (config != null)
        {
            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField("Session", EditorStyles.boldLabel);
            config.subjectId = EditorGUILayout.TextField("Subject ID", config.subjectId);
            config.pattern = (PatternDefinition)EditorGUILayout.ObjectField("Pattern", config.pattern, typeof(PatternDefinition), false);
            config.condition = (ExperimentCondition)EditorGUILayout.EnumPopup("Condition", config.condition);

            EditorGUILayout.Space(4);
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Apply Condition"))
            {
                if (controller != null) controller.ApplyCondition();
                else if (cond != null) cond.ApplyCondition();
            }
            if (GUILayout.Button("Generate Board"))
            {
                if (controller != null) controller.GenerateBoard();
                else if (pop != null) pop.GenerateFromPattern();
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(4);
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Start Session"))
            {
                if (controller != null) controller.StartSession();
            }
            if (GUILayout.Button("Finish Session"))
            {
                if (controller != null) controller.FinishSession();
            }
            EditorGUILayout.EndHorizontal();
        }
        else
        {
            EditorGUILayout.HelpBox("Assign an ExperimentConfig asset.", MessageType.Info);
        }
    }
}

