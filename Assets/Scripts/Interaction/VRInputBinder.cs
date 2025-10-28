using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR;
using ImmersiveMapInterface.Experiment.Selection;

namespace ImmersiveMapInterface.Interaction
{
    /// <summary>
    /// Maps Quest controller inputs to board/miniature interactions and selection.
    /// </summary>
    public class VRInputBinder : MonoBehaviour
    {
        [Header("References")]
        public SelectionSystem selection;
        public BoardGrabRotate grabRotate;
        public Transform rightHandTransform; // e.g., RightPointer or RightHandAnchor

        [Header("Settings")]
        [Range(0f, 1f)]
        public float triggerThreshold = 0.5f;
        public bool usePrimaryButtonForSelect = true;

        private InputDevice rightController;
        private bool prevTrigger;
        private bool prevGrip;
        private bool prevSecondaryButton;

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
            TryCacheDevices();
            if (selection == null)
            {
                Debug.LogWarning("VRInputBinder: SelectionSystem reference is missing.");
            }
        }

        private void Update()
        {
            if (!rightController.isValid) TryCacheDevices();
            if (rightHandTransform == null)
            {
                AutoAssign();
            }

            // Trigger (and optionally A) → select
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

            // B button → clear selection
            bool bPressed = ReadBool(rightController, CommonUsages.secondaryButton);
            if (bPressed && !prevSecondaryButton)
            {
                selection?.ClearSelection();
            }
            prevSecondaryButton = bPressed;

            // Grip → grab rotate
            bool gripPressed = ReadBool(rightController, CommonUsages.gripButton);
            if (gripPressed && !prevGrip)
            {
                if (grabRotate != null && grabRotate.enabled && rightHandTransform != null)
                {
                    grabRotate.BeginGrab(rightHandTransform);
                }
            }
            else if (!gripPressed && prevGrip)
            {
                if (grabRotate != null && grabRotate.enabled)
                {
                    grabRotate.EndGrab();
                }
            }
            else if (gripPressed)
            {
                if (grabRotate != null && grabRotate.enabled)
                {
                    grabRotate.UpdateGrab();
                }
            }
            prevGrip = gripPressed;
        }

        private void TryCacheDevices()
        {
            var devices = new List<InputDevice>();
            InputDevices.GetDevicesWithCharacteristics(InputDeviceCharacteristics.HeldInHand | InputDeviceCharacteristics.Controller | InputDeviceCharacteristics.Right, devices);
            if (devices.Count > 0) rightController = devices[0];
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
                // Prefer the board grab rotate (first found)
                grabRotate = FindObjectOfType<BoardGrabRotate>();
            }
            if (rightHandTransform == null)
            {
                var pointer = GameObject.Find("RightPointer") ?? GameObject.Find("RightHandAnchor");
                if (pointer != null) rightHandTransform = pointer.transform;
            }
        }
    }
}
