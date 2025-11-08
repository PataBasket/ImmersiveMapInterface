using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR;
using ImmersiveMapInterface.Experiment.Selection;

namespace ImmersiveMapInterface.Interaction
{
    /// <summary>
    /// Maps Quest controller inputs to board/miniature interactions and selection.
    /// Left hand grip handles board rotation (including miniature), right hand trigger handles selection.
    /// </summary>
    [DisallowMultipleComponent]
    public class VRInputBinder : MonoBehaviour
    {
        [Header("References")]
        public SelectionSystem selection;
        public BoardGrabRotate grabRotate;
        [Tooltip("Optional additional grab targets (e.g., miniature board).")]
        public BoardGrabRotate[] additionalGrabRotates;
        public Transform leftHandTransform;  // used for board grabbing / ray hits
        public Transform rightHandTransform; // used for selection ray

        [Header("Settings")]
        [Range(0f, 1f)]
        public float triggerThreshold = 0.5f;
        public bool usePrimaryButtonForSelect = true;
        [Tooltip("Maximum ray distance for board grabbing (meters).")]
        public float grabRayDistance = 25f;
        [Tooltip("Layer mask for grab ray hits (set to board/miniature layers).")]
        public LayerMask grabRayMask = ~0;

        private InputDevice rightController;
        private InputDevice leftController;
        private bool prevTrigger;
        private bool prevGrip;
        private bool prevSecondaryButton;
        private BoardGrabRotate activeGrabRotate;

        private void Reset()
        {
            AutoAssign();
        }

        private void Awake()
        {
            AutoAssign();
        }

        private void OnEnable()
        {
            var binders = FindObjectsOfType<VRInputBinder>(true);
            if (binders.Length > 1 && binders[0] != this)
            {
                Debug.LogWarning("VRInputBinder: Duplicate binder detected; disabling this instance.");
                enabled = false;
                return;
            }
            TryCacheDevices();
            if (selection == null)
            {
                Debug.LogWarning("VRInputBinder: SelectionSystem reference is missing.");
            }
        }

        private void Update()
        {
            if (!rightController.isValid || !leftController.isValid) TryCacheDevices();
            AutoAssignIfMissing();

            // Selection (right hand trigger / A button)
            float triggerVal = ReadFloat(rightController, CommonUsages.trigger);
            bool triggerPressed = triggerVal >= triggerThreshold || ReadBool(rightController, CommonUsages.triggerButton);
            if (usePrimaryButtonForSelect)
            {
                triggerPressed = triggerPressed || ReadBool(rightController, CommonUsages.primaryButton);
            }
            if (triggerPressed && !prevTrigger)
            {
                selection?.TrySelectAtPointer();
            }
            prevTrigger = triggerPressed;

            // B button clears selection
            bool bPressed = ReadBool(rightController, CommonUsages.secondaryButton);
            if (bPressed && !prevSecondaryButton)
            {
                selection?.ClearSelection();
            }
            prevSecondaryButton = bPressed;

            // Left grip -> board/miniature rotation
            Vector3? currentRayHit = TryGetRayHit(leftHandTransform);
            bool gripPressed = ReadBool(leftController, CommonUsages.gripButton);
            if (!gripPressed && activeGrabRotate != null)
            {
                activeGrabRotate.EndGrab();
                activeGrabRotate = null;
            }

            if (gripPressed && !prevGrip)
            {
                Vector3 handPos = leftHandTransform != null ? leftHandTransform.position : Vector3.zero;
                var target = SelectGrabRotate(handPos, currentRayHit);
                if (target != null)
                {
                    activeGrabRotate = target;
                    activeGrabRotate.BeginGrab(leftHandTransform, currentRayHit);
                }
            }
            else if (gripPressed && activeGrabRotate != null)
            {
                if (!activeGrabRotate.enabled || !activeGrabRotate.gameObject.activeInHierarchy)
                {
                    activeGrabRotate = null;
                }
                else
                {
                    if (currentRayHit.HasValue)
                    {
                        activeGrabRotate.UpdateRayHit(currentRayHit.Value);
                    }
                    activeGrabRotate.UpdateGrab();
                }
            }
            prevGrip = gripPressed;
        }

        private void TryCacheDevices()
        {
            var devices = new List<InputDevice>();
            InputDevices.GetDevicesWithCharacteristics(InputDeviceCharacteristics.HeldInHand | InputDeviceCharacteristics.Controller | InputDeviceCharacteristics.Right, devices);
            if (devices.Count > 0) rightController = devices[0];
            devices.Clear();
            InputDevices.GetDevicesWithCharacteristics(InputDeviceCharacteristics.HeldInHand | InputDeviceCharacteristics.Controller | InputDeviceCharacteristics.Left, devices);
            if (devices.Count > 0) leftController = devices[0];
        }

        private static bool ReadBool(InputDevice device, InputFeatureUsage<bool> usage)
        {
            return device.isValid && device.TryGetFeatureValue(usage, out bool value) && value;
        }

        private static float ReadFloat(InputDevice device, InputFeatureUsage<float> usage)
        {
            return device.isValid && device.TryGetFeatureValue(usage, out float value) ? value : 0f;
        }

        private void AutoAssign()
        {
            if (selection == null)
            {
                selection = FindObjectOfType<SelectionSystem>();
            }
            if (grabRotate == null)
            {
                grabRotate = FindObjectOfType<BoardGrabRotate>();
            }
            if (additionalGrabRotates == null || additionalGrabRotates.Length == 0)
            {
                additionalGrabRotates = FindObjectsOfType<BoardGrabRotate>(true);
            }
            if (leftHandTransform == null)
            {
                var left = GameObject.Find("LeftPointer") ?? GameObject.Find("LeftHandAnchor");
                if (left != null) leftHandTransform = left.transform;
            }
            if (rightHandTransform == null)
            {
                var pointer = GameObject.Find("RightPointer") ?? GameObject.Find("RightHandAnchor");
                if (pointer != null) rightHandTransform = pointer.transform;
            }
        }

        private void AutoAssignIfMissing()
        {
            if (selection == null || grabRotate == null || leftHandTransform == null || rightHandTransform == null)
            {
                AutoAssign();
            }
        }

        private BoardGrabRotate SelectGrabRotate(Vector3 handPosition, Vector3? rayHit)
        {
            BoardGrabRotate best = null;
            float bestScore = float.MaxValue;

            void Consider(BoardGrabRotate candidate)
            {
                if (candidate == null || !candidate.enabled || !candidate.gameObject.activeInHierarchy) return;
                bool isRay = rayHit.HasValue;
                Vector3 probe = isRay ? rayHit.Value : handPosition;
                if (!candidate.CanGrabAt(probe, out float score, isRay)) return;
                if (score < bestScore)
                {
                    bestScore = score;
                    best = candidate;
                }
            }

            Consider(grabRotate);
            if (additionalGrabRotates != null)
            {
                foreach (var candidate in additionalGrabRotates)
                {
                    if (candidate == grabRotate) continue;
                    Consider(candidate);
                }
            }

            return best;
        }

        private Vector3? TryGetRayHit(Transform hand)
        {
            if (hand == null) return null;
            if (Physics.Raycast(hand.position, hand.forward, out var hit, grabRayDistance, grabRayMask, QueryTriggerInteraction.Ignore))
            {
                return hit.point;
            }
            return null;
        }
    }
}
