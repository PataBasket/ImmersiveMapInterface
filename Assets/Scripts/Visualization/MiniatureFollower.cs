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

        private Quaternion manualRotationOffset = Quaternion.identity;
        private Quaternion lastBaseRotation = Quaternion.identity;
        private Quaternion lastAppliedRotation = Quaternion.identity;
        private bool rotationInitialized = false;

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
            Quaternion baseRotation = currentRotation;

            if (followYaw)
            {
                float headYaw = Mathf.Atan2(headForward.x, headForward.z) * Mathf.Rad2Deg;
                baseRotation = Quaternion.Euler(0f, headYaw, 0f);
                if (mirrorBoardYaw && boardRoot != null)
                {
                    float boardYaw = boardRoot.rotation.eulerAngles.y;
                    baseRotation = Quaternion.Euler(0f, boardYaw, 0f) * baseRotation;
                }
            }

            if (!rotationInitialized)
            {
                lastBaseRotation = baseRotation;
                lastAppliedRotation = currentRotation;
                manualRotationOffset = Quaternion.identity;
                rotationInitialized = true;
            }

            if (preserveUserRotation && grabRotate != null && grabRotate.IsGrabbing)
            {
                float diff = Quaternion.Angle(currentRotation, lastAppliedRotation);
                if (diff > manualRotationThreshold)
                {
                    manualRotationOffset = currentRotation * Quaternion.Inverse(lastBaseRotation);
                }
            }
            else if (!preserveUserRotation)
            {
                manualRotationOffset = Quaternion.identity;
            }

            Quaternion finalRotation;
            if (followYaw)
            {
                finalRotation = baseRotation * manualRotationOffset;
            }
            else
            {
                finalRotation = preserveUserRotation ? currentRotation : baseRotation;
            }

            miniatureRoot.rotation = finalRotation;
            lastBaseRotation = baseRotation;
            lastAppliedRotation = finalRotation;
        }
    }
}
