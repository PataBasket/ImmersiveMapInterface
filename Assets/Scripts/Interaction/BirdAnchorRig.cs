using System.Collections.Generic;
using UnityEngine;

namespace ImmersiveMapInterface.Interaction
{
	/// <summary>
	/// Simple anchor-based Bird PoV rig for Quest: snap between pre-placed anchors and snap-turn.
	/// This is a placeholder for controller bindings; call NextAnchor/PrevAnchor/TurnLeft/TurnRight from input events.
	/// </summary>
	public class BirdAnchorRig : MonoBehaviour
	{
		[SerializeField] private Transform xrRigRoot; // e.g., OVRCameraRig/OVR Player Controller root
		[SerializeField] private BirdAnchorPlacer anchorPlacer;
		[SerializeField] private int currentIndex = 0;
		[SerializeField] private float snapTurnAngle = 30f;

		private IReadOnlyList<Transform> anchors = null;

		private void Awake()
		{
			if (xrRigRoot == null)
			{
				var xr = GameObject.Find("XR Origin") ?? GameObject.Find("XR Rig") ?? GameObject.Find("OVRCameraRig");
				if (xr != null) xrRigRoot = xr.transform;
			}
		}

		private void OnEnable()
		{
			RefreshAnchors();
		}

		public void RefreshAnchors()
		{
			if (anchorPlacer == null) anchorPlacer = GetComponentInChildren<BirdAnchorPlacer>();
			anchors = anchorPlacer != null ? anchorPlacer.GetAnchors() : null;
			currentIndex = Mathf.Clamp(currentIndex, 0, (anchors?.Count ?? 1) - 1);
			SnapToCurrent();
		}

		public void NextAnchor()
		{
			if (anchors == null || anchors.Count == 0) return;
			currentIndex = (currentIndex + 1) % anchors.Count;
			SnapToCurrent();
		}

		public void PrevAnchor()
		{
			if (anchors == null || anchors.Count == 0) return;
			currentIndex = (currentIndex - 1 + anchors.Count) % anchors.Count;
			SnapToCurrent();
		}

		public void TurnLeft()
		{
			if (xrRigRoot == null) return;
			xrRigRoot.Rotate(0f, -snapTurnAngle, 0f, Space.World);
		}

		public void TurnRight()
		{
			if (xrRigRoot == null) return;
			xrRigRoot.Rotate(0f, snapTurnAngle, 0f, Space.World);
		}

		private void SnapToCurrent()
		{
			if (xrRigRoot == null || anchors == null || anchors.Count == 0) return;
			var a = anchors[currentIndex];
			xrRigRoot.SetPositionAndRotation(a.position, a.rotation);
		}
	}
}

