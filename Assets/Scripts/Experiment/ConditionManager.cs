using UnityEngine;
using ImmersiveMapInterface.Board;
using ImmersiveMapInterface.Interaction;
using ImmersiveMapInterface.Visualization;

namespace ImmersiveMapInterface.Experiment
{
    public class ConditionManager : MonoBehaviour
    {
        [Header("Config")]
        public ExperimentConfig config;

        [Header("References")]
        public Transform xrRigRoot;
        public Transform headTransform;
        public Transform groundRoot; // old board visual root (e.g., Ground)
        public Transform boardRoot;  // new logical board root (parent of Ground and piecesRoot)

        [Header("Components To Toggle")] 
        public BirdHeadLocomotion internalLocomotion;
        public BoardGrabRotate birdGrabRotate; // Board board rotation
        public GameObject miniatureRoot; // container for miniature
        public ImmersiveMapInterface.Interaction.BoardGrabRotate miniatureGrabRotate; // optional BoardGrabRotate for miniature
        public ImmersiveMapInterface.Visualization.MiniatureFollower miniatureFollower;  // optional follower for miniature

        [Header("Anchors")] 
        public Transform birdAnchor;       // top-down view anchor
        public Transform internalStartAnchor; // internal view start anchor

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
            DisableExternalLocomotion();
            SetAllDisabled();
            switch (config.condition)
            {
                case ExperimentCondition.Bird:
                    if (birdGrabRotate != null) birdGrabRotate.enabled = true;
                    // snap XR rig to bird anchor for a top-down view
                    if (xrRigRoot != null && birdAnchor != null)
                    {
                        xrRigRoot.SetPositionAndRotation(birdAnchor.position, birdAnchor.rotation);
                    }
                    // ensure miniature is off in Bird mode
                    if (miniatureRoot != null) miniatureRoot.SetActive(false);
                    break;
                case ExperimentCondition.Internal:
                    EnableInternalLocomotion();
                    if (xrRigRoot != null && internalStartAnchor != null)
                    {
                        xrRigRoot.SetPositionAndRotation(internalStartAnchor.position, internalStartAnchor.rotation);
                    }
                    if (miniatureRoot != null) miniatureRoot.SetActive(false);
                    if (miniatureFollower != null) miniatureFollower.followYaw = true;
                    break;
                case ExperimentCondition.InternalWithMiniature:
                    EnableInternalLocomotion();
                    if (miniatureRoot != null) miniatureRoot.SetActive(true);
                    if (xrRigRoot != null && internalStartAnchor != null)
                    {
                        xrRigRoot.SetPositionAndRotation(internalStartAnchor.position, internalStartAnchor.rotation);
                    }
                    break;
            }
            // If we have a BoardGrabRotate, try wiring its boardRoot
            if (birdGrabRotate != null && birdGrabRotate.boardRoot == null && boardRoot != null)
            {
                birdGrabRotate.boardRoot = boardRoot;
            }

            // Miniature controls: when miniature is active, prefer grab interaction over head-follow
            if (miniatureRoot != null)
            {
                bool miniActive = miniatureRoot.activeSelf;
                if (miniatureFollower != null)
                {
                    if (miniatureFollower.head == null && headTransform != null)
                    {
                        miniatureFollower.head = headTransform;
                    }
                    if (miniatureFollower.boardRoot == null && boardRoot != null)
                    {
                        miniatureFollower.boardRoot = boardRoot;
                    }
                    miniatureFollower.enabled = miniActive;
                    miniatureFollower.followYaw = config.condition != ExperimentCondition.InternalWithMiniature;
                }
                if (miniatureGrabRotate != null)
                {
                    miniatureGrabRotate.enabled = miniActive && config.condition == ExperimentCondition.InternalWithMiniature;
                }
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
                SetPrivateBool(internalLocomotion, "constrainToBounds", true);
                if (headTransform != null) SetPrivateTransform(internalLocomotion, "headTransform", headTransform);
                if (xrRigRoot != null) SetPrivateTransform(internalLocomotion, "xrRigRoot", xrRigRoot);
                if (boardRoot != null) SetPrivateTransform(internalLocomotion, "boundsCenter", boardRoot);
                SetPrivateVector3(internalLocomotion, "boundsHalfSize", new Vector3(4f, 4f, 4f));
            }
            DisableExternalLocomotion();
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

        private static void SetPrivateVector3(object obj, string fieldName, Vector3 value)
        {
            if (obj == null) return;
            var f = obj.GetType().GetField(fieldName, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (f != null && f.FieldType == typeof(Vector3))
            {
                f.SetValue(obj, value);
            }
        }

        private void DisableExternalLocomotion()
        {
            if (xrRigRoot == null) return;

            foreach (var cc in xrRigRoot.GetComponentsInChildren<CharacterController>(true))
            {
                if (cc.enabled) cc.enabled = false;
            }

            foreach (var behaviour in xrRigRoot.GetComponentsInChildren<Behaviour>(true))
            {
                if (behaviour == null || behaviour == this || behaviour == internalLocomotion) continue;
                string typeName = behaviour.GetType().Name;
                if (typeName.Contains("OVR") && (typeName.Contains("Player") || typeName.Contains("Locomotion") || typeName.Contains("Controller")))
                {
                    behaviour.enabled = false;
                }
            }
        }
    }
}
