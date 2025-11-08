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
        [Tooltip("Optional ray hit visual (set active when grabbing).")]
        public GameObject grabVisual;

        [Header("Tuning")]
        public float yawSensitivity = 2.0f; // multiplier on computed yaw delta (degrees)
        public bool invertYaw = true;
        public bool yawOnly = true;
        public bool useControllerYaw = true; // if true, derive rotation from hand/controller yaw instead of position arc

        [System.Serializable]
        public class GrabAnchor
        {
            public Transform point;
            public bool allowYaw = true;
            public bool allowPitch = false;
        }

        [Header("Anchors")]
        [Tooltip("Optional grab anchors (corners/edges). When set, the hand must be within snap distance to grab.")]
        public GrabAnchor[] grabAnchors;
        [Tooltip("Require the hand to be close to an anchor before grabbing.")]
        public bool requireAnchor = false;
        [Tooltip("Meters from an anchor within which a grab is allowed.")]
        public float anchorSnapDistance = 0.25f;
        [Tooltip("Meters from an anchor within which a ray-based grab is allowed.")]
        public float anchorSnapDistanceRay = 0.35f;
        [Tooltip("Use the anchor position as the pivot for direction calculations when available.")]
        public bool alignGrabToAnchor = true;
        [Tooltip("Draw gizmos for grab anchors when selected.")]
        public bool visualizeAnchors = true;
        public Color anchorGizmoColor = new Color(0.5f, 0.8f, 1f, 0.6f);

        [Header("Pitch (optional)")]
        public bool allowPitch = false;            // allow vertical rotation like tilting the board up/down
        public float pitchSensitivity = 1.0f;      // multiplier on computed pitch delta (degrees)
        public bool invertPitch = false;

        private bool grabbing = false;
        private Vector3 grabStartDir;
        private Quaternion boardStartRot;
        private float startYawDeg;
        private float startPitchDeg;
        private Transform currentAnchor;
        private Vector3 grabPivot;
        private Vector3 grabHitPoint;
        private bool useRayHit;
        private GrabAnchor currentAnchorData;

        public bool IsGrabbing => grabbing;
        public Transform CurrentAnchor => currentAnchor;

        public void BeginGrab(Transform hand, Vector3? rayHitPoint = null)
        {
            if (boardRoot == null || hand == null) return;
            Vector3 referencePoint = rayHitPoint ?? hand.position;
            if (!EvaluateAnchor(referencePoint, true, out _, rayHitPoint.HasValue)) return;
            reference = hand;
            grabbing = true;
            useRayHit = rayHitPoint.HasValue;
            grabHitPoint = rayHitPoint ?? hand.position;
            Vector3 pivot = GetCurrentPivot();
            grabStartDir = ProjectOnPlane(grabHitPoint - pivot, Vector3.up).normalized;
            boardStartRot = boardRoot.rotation;
            startYawDeg = YawFromForward(ProjectOnPlane(hand.forward, Vector3.up).normalized);
            startPitchDeg = PitchFromForward(hand.forward);
            UpdateGrabVisual(true);
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
            Vector3 pivot = GetCurrentPivot();
            Vector3 referencePoint = useRayHit ? grabHitPoint : reference.position;
                var currentDir = ProjectOnPlane(referencePoint - pivot, Vector3.up).normalized;
                if (currentDir.sqrMagnitude < 1e-6f || grabStartDir.sqrMagnitude < 1e-6f) return;
            bool yawEnabled = currentAnchorData == null ? true : currentAnchorData.allowYaw;
            if (!yawEnabled)
            {
                angle = 0f;
            }
            else
            {
                angle = Vector3.SignedAngle(grabStartDir, currentDir, Vector3.up) * yawSensitivity * (invertYaw ? -1f : 1f);
            }
            }
            var delta = Quaternion.AngleAxis(angle, Vector3.up);

        bool pitchAllowed = allowPitch && (currentAnchorData == null || currentAnchorData.allowPitch);
            if (pitchAllowed)
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
            currentAnchor = null;
            grabPivot = boardRoot != null ? boardRoot.position : transform.position;
            useRayHit = false;
            UpdateGrabVisual(false);
            currentAnchorData = null;
        }

        public bool CanGrabAt(Vector3 position, out float score, bool isRay = false)
        {
            return EvaluateAnchor(position, false, out score, isRay);
        }

        private bool EvaluateAnchor(Vector3 position, bool commit, out float score, bool isRay)
        {
            score = float.MaxValue;
            GrabAnchor bestAnchor = null;
            float bestDist = float.MaxValue;
            bool hasAnchors = grabAnchors != null && grabAnchors.Length > 0;
            float snapDistance = isRay ? anchorSnapDistanceRay : anchorSnapDistance;

            if (hasAnchors)
            {
                foreach (var anchor in grabAnchors)
                {
                    if (anchor == null || anchor.point == null) continue;
                    float dist = Vector3.Distance(position, anchor.point.position);
                    if (dist < bestDist)
                    {
                        bestDist = dist;
                        bestAnchor = anchor;
                    }
                }

                if (bestAnchor != null && bestDist <= snapDistance)
                {
                    if (commit)
                    {
                        currentAnchor = bestAnchor.point;
                        currentAnchorData = bestAnchor;
                        grabPivot = alignGrabToAnchor ? bestAnchor.point.position : GetBoardRootPosition();
                    }
                    score = bestDist;
                    return true;
                }

                if (requireAnchor)
                {
                    return false;
                }
            }

            // No anchor requirement or none within range
            score = Vector3.Distance(position, GetBoardRootPosition());
            if (commit)
            {
                currentAnchor = null;
                currentAnchorData = null;
                grabPivot = GetBoardRootPosition();
            }
            return true;
        }

        private Vector3 GetCurrentPivot()
        {
            if (alignGrabToAnchor && currentAnchor != null)
            {
                return currentAnchor.position;
            }
            if (grabPivot != Vector3.zero) return grabPivot;
            return GetBoardRootPosition();
        }

        private Vector3 GetBoardRootPosition()
        {
            return boardRoot != null ? boardRoot.position : transform.position;
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

        private void OnDrawGizmosSelected()
        {
            if (!visualizeAnchors || grabAnchors == null) return;
            Gizmos.color = anchorGizmoColor;
            foreach (var anchor in grabAnchors)
            {
                if (anchor == null || anchor.point == null) continue;
                Gizmos.DrawWireSphere(anchor.point.position, anchorSnapDistance);
            }
        }

        private void UpdateGrabVisual(bool active)
        {
            if (grabVisual == null) return;
            grabVisual.SetActive(active);
            if (active)
            {
                grabVisual.transform.position = grabHitPoint;
            }
        }

        public void UpdateRayHit(Vector3 hitPoint)
        {
            if (!grabbing) return;
            grabHitPoint = hitPoint;
            if (grabVisual != null && grabVisual.activeSelf)
            {
                grabVisual.transform.position = hitPoint;
            }
        }
    }
}
