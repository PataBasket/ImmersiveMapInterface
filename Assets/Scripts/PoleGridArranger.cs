// Assets/Editor/PoleGridArranger.cs
using UnityEngine;
using UnityEditor;
using System.Linq;

public static class PoleGridArranger
{
    [MenuItem("GameObject/Arrange Poles 8×8 Centered %#&g", false, 0)]
    static void ArrangePoles8x8Centered()
    {
        var ground = GameObject.Find("Ground");
        if (ground == null)
        {
            Debug.LogError("Ground が見つかりません。名前を「Ground」にしてください。");
            return;
        }

        const int cols = 8;
        const int rows = 8;
        const float spacing = 2.0f;  // ポール間隔：必要に応じて調整

        // グリッドの「原点」を中央に持ってくるオフセット
        Vector3 centerOffset = new Vector3(
            (cols - 1) * spacing * 0.5f,
            0f,
            (rows - 1) * spacing * 0.5f
        );

        // Groundを基準に子階層の pole_* を番号順に取得
        var poles = ground.transform
            .Cast<Transform>()
            .Where(t => t.name.StartsWith("pole_"))
            .Select(t => t.gameObject)
            .OrderBy(go =>
            {
                var p = go.name.Split('_');
                return (p.Length == 2 && int.TryParse(p[1], out var idx)) ? idx : int.MaxValue;
            })
            .ToArray();

        if (poles.Length < cols * rows)
            Debug.LogWarning($"pole が {poles.Length} 個しかありません。64個用意してください。");

        for (int i = 0; i < Mathf.Min(poles.Length, cols * rows); i++)
        {
            int x = i / cols;
            int z = i % cols;
            var go = poles[i];

            Undo.RecordObject(go.transform, "Arrange Poles 8×8 Centered");

            // グリッド上の位置を計算し、centerOffset を引く
            Vector3 localPos = new Vector3(x * spacing, 0f, z * spacing) - centerOffset;
            go.transform.position = ground.transform.position + localPos;
            go.name = $"pole_{i}";
        }

        Debug.Log("ポールを 8×8 グリッド（中心揃え）に配置しました。");
    }
}