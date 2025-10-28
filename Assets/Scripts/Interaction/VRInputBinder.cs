using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR;
using ImmersiveMapInterface.Experiment.Selection;

namespace ImmersiveMapInterface.Interaction
{
    // Binds Quest controller inputs to selection and board grab-rotate.
    public class VRInputBinder : MonoBehaviour
    {
        [Header("Refs")]
        public SelectionSystem selection;
        public BoardGrabRotate grabRotate;
        public Transform rightHandTransform; // e.g., OVRCameraRig/TrackingSpace/RightHandAnchor or RightPointer

        [Header("Settings")]
        public float triggerThreshold = 0.5f;

        private InputDevice rightController;
        private bool prevTrigger;
        private bool prevGrip;
        private bool prevSecondaryButton; // B button

        private void OnEnable()
        {
            TryCacheDevices();
        }

        private void Update()
        {
            if (!rightController.isValid) TryCacheDevices();

            // Trigger → select at pointer
            float triggerVal = ReadFloat(rightController, CommonUsages.trigger);
            bool trigger = triggerVal >= triggerThreshold || ReadBool(rightController, CommonUsages.triggerButton);
            if (trigger && !prevTrigger)
            {
                if (selection != null) selection.TrySelectAtPointer();
            }
            prevTrigger = trigger;

            // B button (secondary) → clear selection
            bool b = ReadBool(rightController, CommonUsages.secondaryButton);
            if (b && !prevSecondaryButton)
            {
                if (selection != null) selection.ClearSelection();
            }
            prevSecondaryButton = b;

            // Grip → grab-rotate begin/update/end
            bool grip = ReadBool(rightController, CommonUsages.gripButton);
            if (grip && !prevGrip)
            {
                if (grabRotate != null && rightHandTransform != null) grabRotate.BeginGrab(rightHandTransform);
            }
            else if (!grip && prevGrip)
            {
                if (grabRotate != null) grabRotate.EndGrab();
            }
            else if (grip)
            {
                if (grabRotate != null) grabRotate.UpdateGrab();
            }
            prevGrip = grip;
        }

        private void TryCacheDevices()
        {
            var devices = new List<InputDevice>();
            InputDevices.GetDevicesWithCharacteristics(InputDeviceCharacteristics.HeldInHand | InputDeviceCharacteristics.Controller | InputDeviceCharacteristics.Right, devices);
            if (devices.Count > 0) rightController = devices[0];
        }

        private static bool ReadBool(InputDevice dev, InputFeatureUsage<bool> usage)
        {
            return dev.isValid && dev.TryGetFeatureValue(usage, out bool v) && v;
        }

        private static float ReadFloat(InputDevice dev, InputFeatureUsage<float> usage)
        {
            return dev.isValid && dev.TryGetFeatureValue(usage, out float v) ? v : 0f;
        }
    }
}

