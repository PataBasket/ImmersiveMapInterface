using UnityEditor;
using UnityEngine;
using ImmersiveMapInterface.Board;
using ImmersiveMapInterface.Interaction;

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

	[MenuItem("Tools/Board/Create Pole-Based Board System")] 
	public static void CreatePoleBasedBoard()
	{
		// Find or create Ground object
		var ground = GameObject.Find("Ground");
		if (ground == null)
		{
			ground = new GameObject("Ground");
			Undo.RegisterCreatedObjectUndo(ground, "Create Ground");
		}

		// Create board state
		var boardState = ground.GetComponent<PoleBasedBoardState>();
		if (boardState == null)
		{
			boardState = ground.AddComponent<PoleBasedBoardState>();
			Undo.RegisterCreatedObjectUndo(boardState, "Add PoleBasedBoardState");
		}

		// Create board generator
		var generator = ground.GetComponent<PoleBasedBoardGenerator>();
		if (generator == null)
		{
			generator = ground.AddComponent<PoleBasedBoardGenerator>();
			Undo.RegisterCreatedObjectUndo(generator, "Add PoleBasedBoardGenerator");
		}

		// Create detector
		var detector = ground.GetComponent<PoleBasedFourInARowDetector>();
		if (detector == null)
		{
			detector = ground.AddComponent<PoleBasedFourInARowDetector>();
			Undo.RegisterCreatedObjectUndo(detector, "Add PoleBasedFourInARowDetector");
		}

		Selection.activeGameObject = ground;
		EditorGUIUtility.PingObject(ground);
		
		Debug.Log("Pole-based board system created! Assign Sphere.fbx prefab and materials to PoleBasedBoardGenerator.");
	}

	[MenuItem("Tools/Board/Create Bird PoV Camera")]
	public static void CreateBirdCamera()
	{
		var camGo = new GameObject("BirdCamera");
		var cam = camGo.AddComponent<Camera>();
		cam.clearFlags = CameraClearFlags.Skybox;
		cam.fieldOfView = 60f;
		camGo.AddComponent<BirdOrbitCamera>();
		Undo.RegisterCreatedObjectUndo(camGo, "Create Bird PoV Camera");
		Selection.activeGameObject = camGo;
		EditorGUIUtility.PingObject(camGo);
	}

	[MenuItem("Tools/Board/Create Bird PoV Teleport Rig")]
	public static void CreateBirdTeleportRig()
	{
		var rig = new GameObject("BirdTeleportRig");
		var placer = rig.AddComponent<BirdAnchorPlacer>();
		placer.GenerateAnchors();
		var anchorRig = rig.AddComponent<BirdAnchorRig>();
		Undo.RegisterCreatedObjectUndo(rig, "Create Bird PoV Teleport Rig");
		Selection.activeGameObject = rig;
		EditorGUIUtility.PingObject(rig);
		Debug.Log("Bird PoV Teleport Rig created. Wire input to BirdAnchorRig.NextAnchor/PrevAnchor/TurnLeft/TurnRight.");
	}

	[MenuItem("Tools/Board/Attach Bird Head Locomotion")]
	public static void AttachBirdHeadLocomotion()
	{
		var go = Selection.activeGameObject;
		if (go == null)
		{
			Debug.LogWarning("Select XR rig root (e.g., XR Origin/OVRCameraRig) before attaching BirdHeadLocomotion.");
			return;
		}
		var locomotion = go.GetComponent<BirdHeadLocomotion>();
		if (locomotion == null)
		{
			locomotion = go.AddComponent<BirdHeadLocomotion>();
			Undo.RegisterCreatedObjectUndo(locomotion, "Attach BirdHeadLocomotion");
		}
		Debug.Log("BirdHeadLocomotion attached. Left stick moves in HMD yaw direction.");
	}
}


