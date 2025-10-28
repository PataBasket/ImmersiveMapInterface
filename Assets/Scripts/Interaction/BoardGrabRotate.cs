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
        public float yawSensitivity = 2.0f; // multiplier on computed yaw delta (degrees)
        public bool invertYaw = true;
        public bool yawOnly = true;
        public bool useControllerYaw = true; // if true, derive rotation from hand/controller yaw instead of position arc

        [Header("Pitch (optional)")]
        public bool allowPitch = false;            // allow vertical rotation like tilting the board up/down
        public float pitchSensitivity = 1.0f;      // multiplier on computed pitch delta (degrees)
        public bool invertPitch = false;

        private bool grabbing = false;
        private Vector3 grabStartDir;
        private Quaternion boardStartRot;
        private float startYawDeg;
        private float startPitchDeg;

        public void BeginGrab(Transform hand)
        {
            if (boardRoot == null || hand == null) return;
            reference = hand;
            grabbing = true;
            grabStartDir = ProjectOnPlane(hand.position - boardRoot.position, Vector3.up).normalized;
            boardStartRot = boardRoot.rotation;
            startYawDeg = YawFromForward(ProjectOnPlane(hand.forward, Vector3.up).normalized);
            startPitchDeg = PitchFromForward(hand.forward);
        }

        public void UpdateGrab()
        {
            if (!grabbing || boardRoot == null || reference == null) return;
            float angle;
            if (useControllerYaw)
            {
                float currentYaw = YawFromForward(ProjectOnPlane(reference.forward, Vector3.up).normalized);
                angle = Mathf.DeltaAngle(startYawDeg, currentYaw) * yawSensitivity * (invertYaw ? -1f : 1f);
            }
            else
            {
                var currentDir = ProjectOnPlane(reference.position - boardRoot.position, Vector3.up).normalized;
                if (currentDir.sqrMagnitude < 1e-6f || grabStartDir.sqrMagnitude < 1e-6f) return;
                angle = Vector3.SignedAngle(grabStartDir, currentDir, Vector3.up) * yawSensitivity * (invertYaw ? -1f : 1f);
            }
            var delta = Quaternion.AngleAxis(angle, Vector3.up);

            if (allowPitch)
            {
                float currentPitch = PitchFromForward(reference.forward);
                float pitchDelta = Mathf.DeltaAngle(startPitchDeg, currentPitch) * pitchSensitivity * (invertPitch ? -1f : 1f);
                var pitchQ = Quaternion.AngleAxis(pitchDelta, Vector3.right);
                delta = delta * pitchQ;
            }

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

        private static float YawFromForward(Vector3 fwdOnPlane)
        {
            if (fwdOnPlane.sqrMagnitude < 1e-6f) return 0f;
            fwdOnPlane.Normalize();
            return Mathf.Atan2(fwdOnPlane.x, fwdOnPlane.z) * Mathf.Rad2Deg;
        }

        private static float PitchFromForward(Vector3 fwd)
        {
            if (fwd.sqrMagnitude < 1e-6f) return 0f;
            fwd.Normalize();
            float horiz = new Vector2(fwd.x, fwd.z).magnitude;
            return Mathf.Atan2(fwd.y, horiz) * Mathf.Rad2Deg; // +up, -down
        }
    }
}
