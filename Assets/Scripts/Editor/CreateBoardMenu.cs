using UnityEditor;
using UnityEngine;
using ImmersiveMapInterface.Board;

public static class CreateBoardMenu
{
	[MenuItem("Tools/Board/Create 8x8x8 Board Root")] 
	public static void CreateBoard()
	{
		var root = new GameObject("BoardRoot");
		var state = root.AddComponent<BoardState>();
		var generator = root.AddComponent<BoardGenerator>();
		Undo.RegisterCreatedObjectUndo(root, "Create 8x8x8 Board Root");
		Selection.activeGameObject = root;
		EditorGUIUtility.PingObject(root);
	}
}

