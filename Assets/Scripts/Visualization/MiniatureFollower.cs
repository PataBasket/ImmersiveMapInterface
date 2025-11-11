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
        public bool allowYawAfterTilt = true;
        [Tooltip("Angles below this value (degrees) are ignored when detecting tilts.")]
        public float tiltAngleEpsilon = 0.5f;
        [Tooltip("Minimum dot product with world up to consider the board 'upright'.")]
        [Range(0f, 1f)] public float tiltUprightDotThreshold = 0.995f;

        private Quaternion manualRotationOffset = Quaternion.identity;
        private bool rotationInitialized = false;
        private bool wasGrabbing = false;

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
            bool rotationHasTilt = HasTilt(miniatureRoot.rotation);
            bool allowYawNow = followYaw
                               && !(suspendFollowYawWhileGrabbing && isGrabbing)
                               && !(rotationHasTilt && !allowYawAfterTilt);

            if (!rotationInitialized)
            {
                manualRotationOffset = Quaternion.Inverse(baseYaw) * currentRotation;
                rotationInitialized = true;
            }

            if (preserveUserRotation && grabRotate != null && grabRotate.IsGrabbing)
            {
                Quaternion targetRotation = baseYaw * manualRotationOffset;
                float diff = Quaternion.Angle(currentRotation, targetRotation);
                if (diff > manualRotationThreshold)
                {
                    manualRotationOffset = Quaternion.Inverse(baseYaw) * currentRotation;
                }
            }
            else if (!preserveUserRotation)
            {
                manualRotationOffset = Quaternion.identity;
            }
            else if (!isGrabbing && wasGrabbing)
            {
                manualRotationOffset = Quaternion.Inverse(baseYaw) * currentRotation;
            }

            if (allowYawNow)
            {
                Quaternion finalRotation = baseYaw * manualRotationOffset;
                miniatureRoot.rotation = finalRotation;
            }

            wasGrabbing = isGrabbing;
        }

        private bool HasTilt(Quaternion rotation)
        {
            Vector3 up = rotation * Vector3.up;
            if (float.IsNaN(up.x) || float.IsNaN(up.y) || float.IsNaN(up.z))
            {
                return false;
            }
            up.Normalize();
            float dot = Vector3.Dot(up, Vector3.up);
            if (dot >= tiltUprightDotThreshold)
            {
                return false;
            }
            rotation.ToAngleAxis(out float angle, out _);
            return angle >= tiltAngleEpsilon;
        }
    }
}
