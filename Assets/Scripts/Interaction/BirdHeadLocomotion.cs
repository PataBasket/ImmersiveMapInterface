using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR;

namespace ImmersiveMapInterface.Interaction
{
	/// <summary>
	/// Head-oriented locomotion for Bird PoV on Quest.
	/// - Movement: left thumbstick (primary2DAxis) relative to HMD yaw
	/// - Rotation: HMD (no artificial yaw applied)
	/// Attach to any GameObject and set xrRigRoot to the XR rig root transform.
	/// </summary>
	public class BirdHeadLocomotion : MonoBehaviour
	{
		[SerializeField] private Transform xrRigRoot; // XR Origin / OVRCameraRig root
		[SerializeField] private Transform headTransform; // HMD camera transform
		[SerializeField] private Transform moveTarget;   // Transform to actually move (e.g., OVRCameraRig/TrackingSpace)
		[SerializeField] private float moveSpeed = 1.5f; // meters per second at full deflection
		[SerializeField] private float strafeSpeed = 1.5f;
		[SerializeField] private bool constrainToGroundPlane = false;
		[SerializeField] private bool maintainConstantHeight = true;
		[SerializeField] private float targetHeight = 2.0f; // meters
		[SerializeField] private bool allowVerticalAdjust = true;
		[SerializeField] private float verticalSpeed = 1.0f; // meters per second via right stick Y

		[Header("Bounds (optional)")]
		[SerializeField] private bool constrainToBounds = false;
		[SerializeField] private Transform boundsCenter; // e.g., BoardRoot
		[SerializeField] private Vector3 boundsHalfSize = new Vector3(8f, 8f, 8f);

		private InputDevice leftHandDevice;
		private InputDevice rightHandDevice;
		private float initialY;

        public bool IsMoving { get; private set; }

        private void Reset()
		{
			AutoAssignReferences();
		}

		private void Awake()
		{
			AutoAssignReferences();
			initialY = xrRigRoot != null ? xrRigRoot.position.y : 0f;
			TryCacheDevices();
		}

		private void OnEnable()
		{
			AutoAssignReferences();
			TryCacheDevices();
		}

		private void AutoAssignReferences()
		{
			if (xrRigRoot == null)
			{
				var xr = GameObject.Find("XR Origin") ?? GameObject.Find("XR Rig") ?? GameObject.Find("OVRCameraRig");
				if (xr != null) xrRigRoot = xr.transform;
			}
			if (headTransform == null)
			{
				var cam = Camera.main;
				if (cam != null) headTransform = cam.transform;
			}
			if (moveTarget == null && xrRigRoot != null)
			{
				// Prefer OVRCameraRig/TrackingSpace if present, otherwise move the root
				var tspace = xrRigRoot.Find("TrackingSpace");
				moveTarget = tspace != null ? tspace : xrRigRoot;
			}
		}

		private void Update()
		{
			if (xrRigRoot == null || headTransform == null) return;

			// Read thumbsticks via cached devices (refresh if needed)
			if (!leftHandDevice.isValid || !rightHandDevice.isValid)
			{
				TryCacheDevices();
			}
            Vector2 move = ReadAxis(leftHandDevice, CommonUsages.primary2DAxis);
            IsMoving = move.sqrMagnitude > 0.0001f;
            Vector2 look2 = allowVerticalAdjust ? ReadAxisWithFallback(rightHandDevice) : Vector2.zero;
			if (!leftHandDevice.isValid && Application.isPlaying)
			{
				// Simple one-shot hint if controllers are not detected
				Debug.LogWarning("BirdHeadLocomotion: Left controller not detected. Check OpenXR controller profile and that hand-tracking isn't disabling controllers.");
			}

			if (move.sqrMagnitude > 0.0001f)
			{
				// Head yaw-only forward
				Vector3 fwd = headTransform.forward;
				fwd.y = 0f;
				fwd.Normalize();
				Vector3 right = new Vector3(fwd.z, 0f, -fwd.x); // perpendicular on XZ plane

				Vector3 delta = fwd * (move.y * moveSpeed) + right * (move.x * strafeSpeed);
				delta *= Time.deltaTime;

				if (constrainToGroundPlane) delta.y = 0f;
				(moveTarget != null ? moveTarget : xrRigRoot).position += delta;
			}

			// Optional vertical adjust via right stick Y
			if (allowVerticalAdjust && Mathf.Abs(look2.y) > 0.0001f)
			{
				var p = (moveTarget != null ? moveTarget : xrRigRoot).position;
				p.y += look2.y * verticalSpeed * Time.deltaTime;
				(moveTarget != null ? moveTarget : xrRigRoot).position = p;
			}

			// Maintain constant height (Bird PoV stays airborne)
			if (maintainConstantHeight)
			{
				var p = (moveTarget != null ? moveTarget : xrRigRoot).position;
				p.y = targetHeight;
				(moveTarget != null ? moveTarget : xrRigRoot).position = p;
			}

			// Optional bounds constraint
			if (constrainToBounds && boundsCenter != null)
			{
				var c = boundsCenter.position;
			var p = (moveTarget != null ? moveTarget : xrRigRoot).position;
			p.x = Mathf.Clamp(p.x, c.x - boundsHalfSize.x, c.x + boundsHalfSize.x);
			p.y = Mathf.Clamp(p.y, c.y - boundsHalfSize.y, c.y + boundsHalfSize.y);
			p.z = Mathf.Clamp(p.z, c.z - boundsHalfSize.z, c.z + boundsHalfSize.z);
			(moveTarget != null ? moveTarget : xrRigRoot).position = p;
			}
		}

		private void TryCacheDevices()
		{
			var lefts = new List<InputDevice>();
			InputDevices.GetDevicesWithCharacteristics(InputDeviceCharacteristics.HeldInHand | InputDeviceCharacteristics.Controller | InputDeviceCharacteristics.Left, lefts);
			if (lefts.Count > 0) leftHandDevice = lefts[0];
			var rights = new List<InputDevice>();
			InputDevices.GetDevicesWithCharacteristics(InputDeviceCharacteristics.HeldInHand | InputDeviceCharacteristics.Controller | InputDeviceCharacteristics.Right, rights);
			if (rights.Count > 0) rightHandDevice = rights[0];
		}

		private static Vector2 ReadAxis(InputDevice dev, InputFeatureUsage<Vector2> usage)
		{
			if (dev.isValid && dev.TryGetFeatureValue(usage, out Vector2 v)) return v;
			return Vector2.zero;
		}

		private static Vector2 ReadAxisWithFallback(InputDevice dev)
		{
			Vector2 v = ReadAxis(dev, CommonUsages.primary2DAxis);
			if (v.sqrMagnitude <= 0.0001f)
			{
				v = ReadAxis(dev, CommonUsages.secondary2DAxis);
			}
			return v;
		}
	}
}
