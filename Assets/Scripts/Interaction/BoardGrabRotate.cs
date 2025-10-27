using UnityEngine;

namespace ImmersiveMapInterface.Interaction
{
    // One-hand grab-to-rotate (yaw-focused). Input hookup is left to the integrator
    // via calling BeginGrab/UpdateGrab/EndGrab from controller events.
    public class BoardGrabRotate : MonoBehaviour
    {
        [Header("Target")]
        public Transform boardRoot; // e.g., Ground
        public Transform reference; // controller hand transform used during grab

        [Header("Tuning")]
        public float yawSensitivity = 120f; // deg per meter lateral arc length
        public bool yawOnly = true;

        private bool grabbing = false;
        private Vector3 grabStartDir;
        private Quaternion boardStartRot;

        public void BeginGrab(Transform hand)
        {
            if (boardRoot == null || hand == null) return;
            reference = hand;
            grabbing = true;
            grabStartDir = ProjectOnPlane(hand.position - boardRoot.position, Vector3.up).normalized;
            boardStartRot = boardRoot.rotation;
        }

        public void UpdateGrab()
        {
            if (!grabbing || boardRoot == null || reference == null) return;
            var currentDir = ProjectOnPlane(reference.position - boardRoot.position, Vector3.up).normalized;
            if (currentDir.sqrMagnitude < 1e-6f || grabStartDir.sqrMagnitude < 1e-6f) return;
            float angle = SignedAngleOnPlane(grabStartDir, currentDir, Vector3.up);
            var delta = Quaternion.AngleAxis(angle, Vector3.up);
            boardRoot.rotation = delta * boardStartRot;
        }

        public void EndGrab()
        {
            grabbing = false;
            reference = null;
        }

        private static Vector3 ProjectOnPlane(Vector3 v, Vector3 n)
        {
            return v - Vector3.Dot(v, n) * n;
        }

        private static float SignedAngleOnPlane(Vector3 from, Vector3 to, Vector3 n)
        {
            var f = Vector3.Cross(n, from);
            var t = Vector3.Cross(n, to);
            float sign = Mathf.Sign(Vector3.Dot(Vector3.Cross(from, to), n));
            float angle = Vector3.SignedAngle(from, to, n);
            return angle * sign; // SignedAngle already provides sign; keep explicit for clarity
        }
    }
}

