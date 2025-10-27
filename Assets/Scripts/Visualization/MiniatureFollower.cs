using UnityEngine;

namespace ImmersiveMapInterface.Visualization
{
    // Keeps a miniature root in front of the user's chest and orients it by head yaw.
    // Optionally composes with board yaw (to mirror board orientation) — tweak later as needed.
    public class MiniatureFollower : MonoBehaviour
    {
        public Transform head; // HMD camera
        public Transform miniatureRoot; // the miniature container
        public Transform boardRoot; // optional, to mirror board yaw

        [Header("Placement")]
        public float forwardDistance = 0.5f; // meters in front of head
        public float heightOffset = -0.2f;   // relative to head (chest height)
        public float lateralOffset = 0f;     // optional sideways offset

        [Header("Orientation")]
        public bool mirrorBoardYaw = false;

        private void LateUpdate()
        {
            if (head == null || miniatureRoot == null) return;

            // Position: in front on horizontal plane
            Vector3 fwd = head.forward; fwd.y = 0f; fwd.Normalize();
            Vector3 right = new Vector3(fwd.z, 0f, -fwd.x);
            Vector3 basePos = head.position + fwd * forwardDistance + right * lateralOffset;
            basePos.y = head.position.y + heightOffset;
            miniatureRoot.position = basePos;

            // Rotation: head yaw only; optionally compose with board yaw
            float headYaw = Mathf.Atan2(fwd.x, fwd.z) * Mathf.Rad2Deg; // yaw around Y
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

