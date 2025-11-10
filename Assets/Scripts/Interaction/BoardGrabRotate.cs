using System.Collections.Generic;
using UnityEngine;

namespace ImmersiveMapInterface.Interaction
{
    /// <summary>
    /// Handles grabbing/rotating the board or miniature via explicit anchors.
    /// Corner anchors allow yaw, edge anchors allow pitch.
    /// </summary>
    public class BoardGrabRotate : MonoBehaviour
    {
        [Header("Target")]
        public Transform boardRoot;
        public Transform reference; // controller transform supplied by VRInputBinder
        [Tooltip("Prefab spawned per anchor to visualize hover/grab state.")]
        public GameObject grabVisualPrefab;

        [Header("Yaw")]
        public float yawSensitivity = 2.0f;
        public bool invertYaw = true;
        public bool useControllerYaw = true;

        [Header("Pitch")]
        public bool allowPitch = true;
        public float pitchSensitivity = 1.0f;
        public bool invertPitch = false;
        [Tooltip("Extra multiplier applied to pitch delta (use <1 for finer control).")]
        public float pitchAngleMultiplier = 1.0f;
        [Tooltip("Clamp pitch angle to this absolute value (degrees).")]
        public float pitchAngleLimit = 90f;

        [System.Serializable]
        public class GrabAnchor
        {
            public Transform point;
            public bool allowYaw = true;
            public bool allowPitch = false;
        }

        [Header("Anchors")]
        public GrabAnchor[] grabAnchors;
        public bool requireAnchor = false;
        public float anchorSnapDistance = 0.25f;
        public float anchorSnapDistanceRay = 0.35f;
        public bool alignGrabToAnchor = true;
        public bool visualizeAnchors = true;
        public Color anchorGizmoColor = new Color(0.5f, 0.8f, 1f, 0.6f);

        [Header("Debug")]
        public bool logAnchorVisuals = false;

        private bool grabbing;
        private Vector3 grabStartDir;
        private Quaternion boardStartRot;
        private float startYawDeg;
        private float startPitchDeg;
        private Transform currentAnchor;
        private GrabAnchor currentAnchorData;
        private Vector3 grabPivot;
        private Vector3 grabHitPoint;
        private bool useRayHit;
        private float currentPitchAmount;
        private readonly List<GameObject> anchorVisuals = new List<GameObject>();

        public bool IsGrabbing => grabbing;

        public void BeginGrab(Transform hand, Vector3? rayHitPoint)
        {
            if (boardRoot == null || hand == null) return;
            Vector3 referencePoint = rayHitPoint ?? hand.position;
            if (!EvaluateAnchor(referencePoint, true, out _, rayHitPoint.HasValue)) return;

            reference = hand;
            grabbing = true;
            useRayHit = rayHitPoint.HasValue;
            grabHitPoint = referencePoint;
            currentPitchAmount = 0f;

            Vector3 pivot = GetCurrentPivot();
            grabStartDir = ProjectOnPlane(grabHitPoint - pivot, Vector3.up).normalized;
            boardStartRot = boardRoot.rotation;
            startYawDeg = YawFromForward(ProjectOnPlane(hand.forward, Vector3.up).normalized);
            startPitchDeg = PitchFromForward(hand.forward);

            UpdateAnchorVisuals();
        }

        public void UpdateGrab()
        {
            if (!grabbing || boardRoot == null || reference == null) return;

            float yawAngle = 0f;
            if (useControllerYaw)
            {
                float currentYaw = YawFromForward(ProjectOnPlane(reference.forward, Vector3.up).normalized);
                yawAngle = Mathf.DeltaAngle(startYawDeg, currentYaw) * yawSensitivity * (invertYaw ? -1f : 1f);
            }
            else
            {
                Vector3 pivot = GetCurrentPivot();
                Vector3 referencePoint = useRayHit ? grabHitPoint : reference.position;
                var currentDir = ProjectOnPlane(referencePoint - pivot, Vector3.up).normalized;
                if (currentDir.sqrMagnitude < 1e-6f || grabStartDir.sqrMagnitude < 1e-6f) return;
                if (currentAnchorData == null || currentAnchorData.allowYaw)
                {
                    yawAngle = Vector3.SignedAngle(grabStartDir, currentDir, Vector3.up) *
                               yawSensitivity * (invertYaw ? -1f : 1f);
                }
                else
                {
                    yawAngle = 0f;
                }
            }

            Quaternion delta = Quaternion.AngleAxis(yawAngle, Vector3.up);

            if (allowPitch && currentAnchorData != null && currentAnchorData.allowPitch)
            {
                float currentPitch = PitchFromForward(reference.forward);
                float targetPitch = Mathf.Clamp(
                    Mathf.DeltaAngle(startPitchDeg, currentPitch) * pitchSensitivity * pitchAngleMultiplier *
                    (invertPitch ? -1f : 1f),
                    -pitchAngleLimit,
                    pitchAngleLimit);

                float deltaPitch = targetPitch - currentPitchAmount;
                currentPitchAmount = targetPitch;

                if (Mathf.Abs(deltaPitch) > 0.01f)
                {
                    Vector3 axis = GetPitchAxis();
                    var pitchQ = Quaternion.AngleAxis(deltaPitch, axis);
                    delta = pitchQ * delta;
                }
            }

            boardRoot.rotation = delta * boardStartRot;
        }

        public void EndGrab()
        {
            grabbing = false;
            reference = null;
            currentAnchor = null;
            currentAnchorData = null;
            grabPivot = boardRoot != null ? boardRoot.position : transform.position;
            currentPitchAmount = 0f;
            HideAllAnchorVisuals();
        }

        public bool CanGrabAt(Vector3 position, out float score, bool isRay)
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
                    if (anchor?.point == null) continue;
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
                        UpdateAnchorVisuals();
                    }
                    score = bestDist;
                    return true;
                }

                if (requireAnchor)
                {
                    return false;
                }
            }

            score = Vector3.Distance(position, GetBoardRootPosition());
            if (commit)
            {
                currentAnchor = null;
                currentAnchorData = null;
                grabPivot = GetBoardRootPosition();
                HideAllAnchorVisuals();
            }
            return true;
        }

        public void UpdateHover(Vector3 position, bool isRay)
        {
            if (grabAnchors == null || grabAnchors.Length == 0 || grabVisualPrefab == null) return;
            if (grabbing)
            {
                UpdateAnchorVisuals();
                return;
            }

            float snapDistance = isRay ? anchorSnapDistanceRay : anchorSnapDistance;
            int bestIndex = -1;
            float bestDist = float.MaxValue;
            for (int i = 0; i < grabAnchors.Length; i++)
            {
                var anchor = grabAnchors[i];
                if (anchor?.point == null) continue;
                float dist = Vector3.Distance(position, anchor.point.position);
                if (dist < bestDist)
                {
                    bestDist = dist;
                    bestIndex = i;
                }
            }

            bool within = bestIndex >= 0 && bestDist <= snapDistance;
            HideAllAnchorVisuals();
            if (within)
            {
                var anchor = grabAnchors[bestIndex];
                SetAnchorVisualActive(bestIndex, true, anchor.point.position);
            }
        }

        public void ClearHoverVisuals()
        {
            if (!grabbing)
            {
                HideAllAnchorVisuals();
            }
        }

        public void UpdateRayHit(Vector3 hitPoint)
        {
            if (!grabbing) return;
            grabHitPoint = hitPoint;
        }

        private Vector3 GetCurrentPivot()
        {
            if (alignGrabToAnchor && currentAnchor != null)
            {
                return currentAnchor.position;
            }
            return grabPivot != Vector3.zero ? grabPivot : GetBoardRootPosition();
        }

        private Vector3 GetBoardRootPosition()
        {
            return boardRoot != null ? boardRoot.position : transform.position;
        }

        private Vector3 GetPitchAxis()
        {
            if (boardRoot == null) return Vector3.right;
            if (currentAnchorData == null || currentAnchor == null) return boardRoot.right;

            Vector3 local = boardRoot.InverseTransformPoint(currentAnchor.position);
            if (Mathf.Abs(local.x) >= Mathf.Abs(local.z))
            {
                return boardRoot.forward;
            }
            return boardRoot.right;
        }

        private static Vector3 ProjectOnPlane(Vector3 v, Vector3 n)
        {
            return v - Vector3.Dot(v, n) * n;
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
            return Mathf.Atan2(fwd.y, horiz) * Mathf.Rad2Deg;
        }

        private void OnDrawGizmosSelected()
        {
            if (!visualizeAnchors || grabAnchors == null) return;
            Gizmos.color = anchorGizmoColor;
            foreach (var anchor in grabAnchors)
            {
                if (anchor?.point == null) continue;
                Gizmos.DrawWireSphere(anchor.point.position, anchorSnapDistance);
            }
        }

        private void UpdateAnchorVisuals()
        {
            if (grabVisualPrefab == null || grabAnchors == null || grabAnchors.Length == 0) return;
            EnsureAnchorVisualInstances();
            for (int i = 0; i < grabAnchors.Length; i++)
            {
                var anchor = grabAnchors[i];
                if (anchor?.point == null) continue;
                bool active = grabbing && currentAnchorData == anchor;
                Vector3 pos = anchor.point.position;
                SetAnchorVisualActive(i, active, pos);
            }
        }

        private void EnsureAnchorVisualInstances()
        {
            if (grabVisualPrefab == null || grabAnchors == null) return;
            while (anchorVisuals.Count < grabAnchors.Length)
            {
                var instance = Instantiate(grabVisualPrefab, transform);
                instance.SetActive(false);
                anchorVisuals.Add(instance);
            }
        }

        private void HideAllAnchorVisuals()
        {
            foreach (var visual in anchorVisuals)
            {
                if (visual != null) visual.SetActive(false);
            }
        }

        private void SetAnchorVisualActive(int index, bool active, Vector3 position)
        {
            EnsureAnchorVisualInstances();
            if (index < 0 || index >= anchorVisuals.Count) return;
            var visual = anchorVisuals[index];
            if (visual == null) return;

            if (!active)
            {
                if (visual.activeSelf)
                {
                    visual.SetActive(false);
                    if (logAnchorVisuals) Debug.Log($"BoardGrabRotate: Anchor[{index}] visual OFF");
                }
                return;
            }

            visual.transform.position = position;
            if (!visual.activeSelf && logAnchorVisuals)
            {
                Debug.Log($"BoardGrabRotate: Anchor[{index}] visual ON at {position}");
            }
            visual.SetActive(true);
        }
    }
}
