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

        [Header("Placement")]
        public float forwardDistance = 0.5f;
        public float heightOffset = -0.2f;
        public float lateralOffset = 0f;

        [Header("Orientation")]
        public bool followYaw = true;
        public bool mirrorBoardYaw = false;

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

            if (followYaw)
            {
                float headYaw = Mathf.Atan2(headForward.x, headForward.z) * Mathf.Rad2Deg;
                Quaternion yawRot = Quaternion.Euler(0f, headYaw, 0f);
                if (mirrorBoardYaw && boardRoot != null)
                {
                    float boardYaw = boardRoot.rotation.eulerAngles.y;
                    yawRot = Quaternion.Euler(0f, boardYaw, 0f) * yawRot;
                }
                miniatureRoot.rotation = yawRot;
            }
        }
    }
}
