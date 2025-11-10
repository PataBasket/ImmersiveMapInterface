using UnityEngine;

namespace ImmersiveMapInterface.Visualization
{
    /// <summary>
    /// Keeps the miniature positioned near the user's chest. Yaw follow can be toggled so that
    /// the miniature stays stable even when the head turns.
    /// </summary>
    public class MiniatureFollower : MonoBehaviour
    {
        [Header("References")]
        public Transform head;
        public Transform miniatureRoot;
        public Transform boardRoot;
        [Tooltip("Optional: reference to the grab rotate component that controls the miniature.")]
        public ImmersiveMapInterface.Interaction.BoardGrabRotate grabRotate;

        [Header("Placement")]
        public float forwardDistance = 0.5f;
        public float heightOffset = -0.2f;
        public float lateralOffset = 0f;

        [Header("Orientation")]
        public bool followYaw = true;
        public bool mirrorBoardYaw = false;
        [Tooltip("Keep user-applied rotations when the follower updates yaw/position.")]
        public bool preserveUserRotation = true;
        [Tooltip("Minimum degrees of change before we treat rotation as a manual input.")]
        [Range(0f, 10f)] public float manualRotationThreshold = 0.5f;
        [Tooltip("When true, yaw alignment is suspended while the board is being grabbed.")]
        public bool suspendFollowYawWhileGrabbing = true;
        [Tooltip("If true, yaw alignment resumes even after the user tilts the miniature.")]
        public bool allowYawAfterTilt = false;
        [Tooltip("Angles below this value (degrees) are ignored when detecting tilts.")]
        public float tiltAngleEpsilon = 0.5f;
        [Tooltip("How closely the rotation axis must align with +Y to be treated as yaw-only (1 = identical).")]
        [Range(0f, 1f)] public float tiltAxisVerticalDot = 0.98f;

        private Quaternion manualRotationOffset = Quaternion.identity;
        private bool rotationInitialized = false;
        private bool manualTiltActive = false;

        private void LateUpdate()
        {
            if (head == null || miniatureRoot == null) return;

            Vector3 headForward = head.forward;
            headForward.y = 0f;
            if (headForward.sqrMagnitude < 0.0001f)
            {
                headForward = head.forward;
            }
            headForward.Normalize();
            Vector3 headRight = new Vector3(headForward.z, 0f, -headForward.x);

            Vector3 targetPos = head.position + headForward * forwardDistance + headRight * lateralOffset;
            targetPos.y = head.position.y + heightOffset;
            miniatureRoot.position = targetPos;

            Quaternion currentRotation = miniatureRoot.rotation;
            Quaternion baseYaw = Quaternion.identity;

            if (followYaw)
            {
                float headYaw = Mathf.Atan2(headForward.x, headForward.z) * Mathf.Rad2Deg;
                baseYaw = Quaternion.Euler(0f, headYaw, 0f);
                if (mirrorBoardYaw && boardRoot != null)
                {
                    float boardYaw = boardRoot.rotation.eulerAngles.y;
                    baseYaw = Quaternion.Euler(0f, boardYaw, 0f) * baseYaw;
                }
            }

            bool isGrabbing = grabRotate != null && grabRotate.IsGrabbing;
            bool allowYawNow = followYaw
                               && !(suspendFollowYawWhileGrabbing && isGrabbing)
                               && !(manualTiltActive && !allowYawAfterTilt);

            if (!rotationInitialized)
            {
                manualRotationOffset = currentRotation * Quaternion.Inverse(baseYaw);
                rotationInitialized = true;
                manualTiltActive = false;
            }

            if (preserveUserRotation && grabRotate != null && grabRotate.IsGrabbing)
            {
                Quaternion targetRotation = baseYaw * manualRotationOffset;
                float diff = Quaternion.Angle(currentRotation, targetRotation);
                if (diff > manualRotationThreshold)
                {
                    manualRotationOffset = currentRotation * Quaternion.Inverse(baseYaw);
                    manualTiltActive = ContainsTilt(manualRotationOffset);
                }
            }
            else if (!preserveUserRotation)
            {
                manualRotationOffset = Quaternion.identity;
                manualTiltActive = false;
            }
            else if (!manualTiltActive)
            {
                manualTiltActive = ContainsTilt(manualRotationOffset);
            }
            else if (manualTiltActive && !ContainsTilt(manualRotationOffset))
            {
                manualTiltActive = false;
            }

            if (allowYawNow)
            {
                Quaternion finalRotation = baseYaw * manualRotationOffset;
                miniatureRoot.rotation = finalRotation;
            }
        }

        private bool ContainsTilt(Quaternion rotation)
        {
            rotation.ToAngleAxis(out float angle, out Vector3 axis);
            if (float.IsNaN(axis.x) || float.IsNaN(axis.y) || float.IsNaN(axis.z))
            {
                return false;
            }
            if (angle < tiltAngleEpsilon)
            {
                return false;
            }
            axis = axis.normalized;
            float verticalDot = Mathf.Abs(Vector3.Dot(axis, Vector3.up));
            return verticalDot < tiltAxisVerticalDot;
        }
    }
}
