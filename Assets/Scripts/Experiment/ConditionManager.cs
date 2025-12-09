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
        public ImmersiveMapInterface.Visualization.MiniaturePoleBoardGenerator miniatureBoardGenerator;

        [Header("Anchors")] 
        public Transform birdAnchor;       // top-down view anchor
        public Transform internalStartAnchor; // internal view start anchor

        [Header("Miniature Options")]
        [Tooltip("If true, the miniature will align its yaw with the head while active.")]
        public bool miniatureFollowsHeadYaw = false;

        [Header("Bounds Override (Internal)")]
        [Tooltip("If enabled, ConditionManager will set BirdHeadLocomotion bounds on ApplyCondition.")]
        public bool overrideBounds = false;
        [Tooltip("Center used for Internal bounds when override is enabled. If null, boardRoot is used.")]
        public Transform boundsCenterOverride;
        [Tooltip("Half-size used for Internal bounds when override is enabled.")]
        public Vector3 boundsHalfSizeOverride = new Vector3(4f, 4f, 4f);

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
            if (miniatureGrabRotate != null && miniatureGrabRotate.boardRoot == null && miniatureRoot != null)
            {
                miniatureGrabRotate.boardRoot = miniatureRoot.transform;
            }

            // Miniature controls: when miniature is active, prefer grab interaction over head-follow
            if (miniatureRoot != null)
            {
                bool miniActive = miniatureRoot.activeSelf;
                PoleBasedBoardState sharedBoard = null;
                if (groundRoot != null)
                {
                    sharedBoard = groundRoot.GetComponent<PoleBasedBoardState>();
                }
                if (sharedBoard == null && boardRoot != null)
                {
                    sharedBoard = boardRoot.GetComponentInChildren<PoleBasedBoardState>();
                }
                if (miniatureBoardGenerator == null)
                {
                    miniatureBoardGenerator = miniatureRoot.GetComponentInChildren<ImmersiveMapInterface.Visualization.MiniaturePoleBoardGenerator>(true);
                }
                if (miniatureBoardGenerator != null)
                {
                    if (sharedBoard == null)
                    {
                        Debug.LogWarning("ConditionManager: shared PoleBasedBoardState not found. Miniature colors may be incorrect.", this);
                    }
                    miniatureBoardGenerator.SetBoardState(sharedBoard);
                }
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
                    if (miniatureGrabRotate != null)
                    {
                        miniatureFollower.grabRotate = miniatureGrabRotate;
                    }
                    miniatureFollower.enabled = miniActive;
                    miniatureFollower.followYaw = miniatureFollowsHeadYaw;
                    miniatureFollower.allowYawAfterTilt = miniatureFollowsHeadYaw;
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
                if (overrideBounds)
                {
                    var center = boundsCenterOverride != null ? boundsCenterOverride : boardRoot;
                    if (center != null) SetPrivateTransform(internalLocomotion, "boundsCenter", center);
                    SetPrivateVector3(internalLocomotion, "boundsHalfSize", boundsHalfSizeOverride);
                }
                
                // Prefer moving TrackingSpace (OVRCameraRig) instead of root to avoid floor-snapping by runtime
                Transform moveTarget = null;
                if (xrRigRoot != null)
                {
                    var tspace = xrRigRoot.Find("TrackingSpace");
                    if (tspace != null) moveTarget = tspace;
                }
                if (moveTarget != null)
                {
                    var f = internalLocomotion.GetType().GetField("moveTarget", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    if (f != null) f.SetValue(internalLocomotion, moveTarget);
                }
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
