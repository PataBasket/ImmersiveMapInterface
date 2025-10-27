// Assets/Editor/BulkRenameWindow.cs
using UnityEditor;
using UnityEngine;

public class BulkRenameWindow : EditorWindow
{
    string baseName = "pole_";
    int startIndex = 0;

    [MenuItem("Tools/一括リネーム")]
    static void OpenWindow()
    {
        GetWindow<BulkRenameWindow>("Bulk Rename");
    }

    void OnGUI()
    {
        EditorGUILayout.LabelField("選択中のオブジェクトを一括リネーム", EditorStyles.boldLabel);
        baseName = EditorGUILayout.TextField("ベース名", baseName);
        startIndex = EditorGUILayout.IntField("開始インデックス", startIndex);

        if (GUILayout.Button("Rename Selected"))
        {
            var objs = Selection.gameObjects;
            int idx = startIndex;
            Undo.RecordObjects(objs, "Bulk Rename");
            foreach (var go in objs)
            {
                go.name = $"{baseName}{idx}";
                idx++;
            }
        }
    }
}
