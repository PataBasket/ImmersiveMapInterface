using UnityEngine;
using ImmersiveMapInterface.Board;
using ImmersiveMapInterface.Interaction;

namespace ImmersiveMapInterface.Experiment
{
    public class ConditionManager : MonoBehaviour
    {
        [Header("Config")]
        public ExperimentConfig config;

        [Header("References")]
        public Transform xrRigRoot;
        public Transform headTransform;
        public Transform groundRoot; // board root (e.g., Ground)

        [Header("Components To Toggle")] 
        public BirdHeadLocomotion internalLocomotion;
        public MonoBehaviour birdGrabRotate; // BoardGrabRotate (added below)
        public GameObject miniatureRoot; // container for miniature

        private void Reset()
        {
            xrRigRoot = GameObject.Find("XR Origin")?.transform ?? GameObject.Find("OVRCameraRig")?.transform;
            headTransform = Camera.main != null ? Camera.main.transform : headTransform;
            if (groundRoot == null) groundRoot = GameObject.Find("Ground")?.transform;
        }

        private void Awake()
        {
            ApplyCondition();
        }

        public void ApplyCondition()
        {
            if (config == null) return;
            SetAllDisabled();
            switch (config.condition)
            {
                case ExperimentCondition.Bird:
                    if (birdGrabRotate != null) birdGrabRotate.enabled = true;
                    break;
                case ExperimentCondition.Internal:
                    EnableInternalLocomotion();
                    break;
                case ExperimentCondition.InternalWithMiniature:
                    EnableInternalLocomotion();
                    if (miniatureRoot != null) miniatureRoot.SetActive(true);
                    break;
            }
        }

        private void EnableInternalLocomotion()
        {
            if (internalLocomotion != null)
            {
                internalLocomotion.enabled = true;
                // Configure private serialized fields via reflection to avoid changing the locomotion script.
                SetPrivateBool(internalLocomotion, "constrainToGroundPlane", false);
                SetPrivateBool(internalLocomotion, "maintainConstantHeight", false);
                SetPrivateBool(internalLocomotion, "allowVerticalAdjust", true);
                if (headTransform != null) SetPrivateTransform(internalLocomotion, "headTransform", headTransform);
                if (xrRigRoot != null) SetPrivateTransform(internalLocomotion, "xrRigRoot", xrRigRoot);
            }
        }

        private void SetAllDisabled()
        {
            if (internalLocomotion != null) internalLocomotion.enabled = false;
            if (birdGrabRotate != null) birdGrabRotate.enabled = false;
            if (miniatureRoot != null) miniatureRoot.SetActive(false);
        }

        private static void SetPrivateBool(object obj, string fieldName, bool value)
        {
            if (obj == null) return;
            var f = obj.GetType().GetField(fieldName, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (f != null && f.FieldType == typeof(bool))
            {
                f.SetValue(obj, value);
            }
        }

        private static void SetPrivateTransform(object obj, string fieldName, Transform value)
        {
            if (obj == null) return;
            var f = obj.GetType().GetField(fieldName, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (f != null && typeof(Transform).IsAssignableFrom(f.FieldType))
            {
                f.SetValue(obj, value);
            }
        }
    }
}
