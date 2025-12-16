using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR;
using ImmersiveMapInterface.Experiment.Selection;

namespace ImmersiveMapInterface.Interaction
{
    /// <summary>
    /// Routes controller input: right-hand trigger selects, left-hand grip rotates boards.
    /// </summary>
    [DisallowMultipleComponent]
    public class VRInputBinder : MonoBehaviour
    {
        [Header("References")]
        public SelectionSystem selection;
        public BoardGrabRotate grabRotate;
        [Tooltip("Other BoardGrabRotate targets (e.g., miniature board).")]
        public BoardGrabRotate[] additionalGrabRotates;
        public Transform leftHandTransform;
        public Transform rightHandTransform;

        [Header("Settings")]
        public float grabRayDistance = 25f;
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

            HandleSelectionInput();
            HandleBoardGrabInput();
        }

        private void HandleSelectionInput()
        {
            bool triggerPressed = ReadBool(rightController, CommonUsages.primaryButton);
            if (triggerPressed && !prevTrigger)
            {
                selection?.TrySelectAtPointer();
            }
            prevTrigger = triggerPressed;

            bool bPressed = ReadBool(rightController, CommonUsages.secondaryButton);
            if (bPressed && !prevSecondaryButton)
            {
                selection?.CancelPendingSelection();
            }
            prevSecondaryButton = bPressed;
        }

        private void HandleBoardGrabInput()
        {
            Vector3? rayHit = TryGetRayHit(leftHandTransform);
            Vector3 handPoint = leftHandTransform != null ? leftHandTransform.position : Vector3.zero;
            UpdateHoverVisual(handPoint, rayHit);

            bool gripPressed = ReadBool(leftController, CommonUsages.gripButton);
            if (!gripPressed && activeGrabRotate != null)
            {
                activeGrabRotate.EndGrab();
                activeGrabRotate = null;
            }

            if (gripPressed && !prevGrip)
            {
                var target = SelectGrabRotate(handPoint, rayHit);
                if (target != null)
                {
                    activeGrabRotate = target;
                    var beginRay = (target.allowRayInput && rayHit.HasValue) ? rayHit : (Vector3?)null;
                    activeGrabRotate.BeginGrab(leftHandTransform, beginRay);
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
                    var activeRay = (activeGrabRotate.allowRayInput && rayHit.HasValue) ? rayHit : (Vector3?)null;
                    if (activeRay.HasValue)
                    {
                        activeGrabRotate.UpdateRayHit(activeRay.Value);
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
                bool isRay = candidate.allowRayInput && rayHit.HasValue;
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

        private void UpdateHoverVisual(Vector3 handPosition, Vector3? rayHit)
        {
            if (activeGrabRotate != null)
            {
                var activeRay = (activeGrabRotate.allowRayInput && rayHit.HasValue) ? rayHit : (Vector3?)null;
                activeGrabRotate.UpdateHover(activeRay ?? handPosition, activeRay.HasValue);
                foreach (var rotate in EnumerateAllRotates())
                {
                    if (rotate != activeGrabRotate)
                    {
                        rotate.ClearHoverVisuals();
                    }
                }
                return;
            }

            var target = SelectGrabRotate(handPosition, rayHit);
            foreach (var rotate in EnumerateAllRotates())
            {
                if (rotate == null) continue;
                if (rotate == target)
                {
                    bool useRay = rotate.allowRayInput && rayHit.HasValue;
                    rotate.UpdateHover(useRay ? rayHit.Value : handPosition, useRay);
                }
                else
                {
                    rotate.ClearHoverVisuals();
                }
            }
        }

        private IEnumerable<BoardGrabRotate> EnumerateAllRotates()
        {
            if (grabRotate != null) yield return grabRotate;
            if (additionalGrabRotates != null)
            {
                foreach (var rotate in additionalGrabRotates)
                {
                    if (rotate != null) yield return rotate;
                }
            }
        }
    }
}
