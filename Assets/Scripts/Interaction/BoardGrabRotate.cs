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
        public Transform reference;
        [Tooltip("Prefab spawned per anchor to visualize hover/grab state.")]
        public GameObject grabVisualPrefab;

        [Header("Yaw")]
        public float yawSensitivity = 2.0f;
        public bool invertYaw = true;
        public bool useControllerYaw = true;

        [Header("Pitch")]
        public bool allowPitch = true;
        public float pitchSensitivity = 90f;
        public bool invertPitch = false;
        [Tooltip("Clamp pitch angle to this absolute value (degrees).")]
        public float pitchAngleLimit = 90f;
        [Header("Pitch Input Filtering")]
        [Tooltip("Ignore tiny vertical motions when converting hand movement to pitch (meters).")]
        [Min(0f)] public float pitchDeadZoneMeters = 0.01f;
        [Tooltip("0 = no response, 1 = raw input. Lower values smooth out jitter.")]
        [Range(0f, 1f)] public float pitchFilterStrength = 0.15f;

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
        [Tooltip("Extra tolerance added on top of anchorSnapDistanceRay to make ray grabs more forgiving.")]
        [Min(0f)] public float anchorRayAssistPadding = 0.1f;
        public bool alignGrabToAnchor = true;
        public bool visualizeAnchors = true;
        public Color anchorGizmoColor = new Color(0.5f, 0.8f, 1f, 0.6f);
        [Tooltip("Uniform scale multiplier applied to instantiated grab visuals.")]
        [Min(0.001f)] public float anchorVisualScale = 1f;
        [Tooltip("If false, hover/grab always use the hand position even if a ray hit exists.")]
        public bool allowRayInput = true;

        [Header("Debug")]
        public bool logAnchorVisuals = false;

        private bool grabbing;
        private Vector3 grabStartDir;
        private Quaternion boardStartRot;
        private float startYawDeg;
        private Transform currentAnchor;
        private GrabAnchor currentAnchorData;
        private Vector3 grabPivot;
        private Vector3 grabHitPoint;
        private bool useRayHit;
        private float currentPitchAmount;
        private float startHandHeight;
        private Vector3 fixedPitchAxis = Vector3.right;
        private float filteredDeltaHeight;
        private float pitchInputSign = 1f;
        private readonly List<GameObject> anchorVisuals = new List<GameObject>();
        private readonly List<Vector3> anchorVisualBaseScales = new List<Vector3>();

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
            filteredDeltaHeight = 0f;
            startHandHeight = hand.position.y;

            Vector3 pivot = GetCurrentPivot();
            grabStartDir = ProjectOnPlane(grabHitPoint - pivot, Vector3.up).normalized;
            boardStartRot = boardRoot.rotation;
            startYawDeg = YawFromForward(ProjectOnPlane(hand.forward, Vector3.up).normalized);
            SetupPitchAxis();

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

            bool yawAllowed = currentAnchorData == null || currentAnchorData.allowYaw;
            Quaternion yawRotation = yawAllowed ? Quaternion.AngleAxis(yawAngle, Vector3.up) : Quaternion.identity;

            if (allowPitch && currentAnchorData != null && currentAnchorData.allowPitch)
            {
                float deltaHeight = reference.position.y - startHandHeight;
                if (Mathf.Abs(deltaHeight) < pitchDeadZoneMeters)
                {
                    deltaHeight = 0f;
                }

                float lerpFactor = Mathf.Clamp01(pitchFilterStrength);
                filteredDeltaHeight = lerpFactor > 0f
                    ? Mathf.Lerp(filteredDeltaHeight, deltaHeight, lerpFactor)
                    : deltaHeight;

                float desiredPitch = filteredDeltaHeight * pitchSensitivity * pitchInputSign;
                if (invertPitch) desiredPitch *= -1f;
                currentPitchAmount = Mathf.Clamp(desiredPitch, -pitchAngleLimit, pitchAngleLimit);
            }
            else
            {
                filteredDeltaHeight = 0f;
                currentPitchAmount = 0f;
            }

            Quaternion pitchRotation = Quaternion.identity;
            if (Mathf.Abs(currentPitchAmount) > 0.001f)
            {
                pitchRotation = Quaternion.AngleAxis(currentPitchAmount, fixedPitchAxis);
            }

            boardRoot.rotation = yawRotation * pitchRotation * boardStartRot;
        }

        public void EndGrab()
        {
            grabbing = false;
            reference = null;
            currentAnchor = null;
            currentAnchorData = null;
            grabPivot = boardRoot != null ? boardRoot.position : transform.position;
            currentPitchAmount = 0f;
            filteredDeltaHeight = 0f;
            pitchInputSign = 1f;
            fixedPitchAxis = Vector3.right;
            startHandHeight = 0f;
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
            float baseSnapDistance = isRay ? anchorSnapDistanceRay : anchorSnapDistance;
            float snapDistance = baseSnapDistance + (isRay ? anchorRayAssistPadding : 0f);

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

        private void SetupPitchAxis()
        {
            filteredDeltaHeight = 0f;
            pitchInputSign = 1f;
            fixedPitchAxis = boardStartRot * Vector3.right;

            if (boardRoot == null || currentAnchorData == null || currentAnchor == null || !currentAnchorData.allowPitch)
            {
                return;
            }

            Vector3 local = boardRoot.InverseTransformPoint(currentAnchor.position);
            bool eastWestEdge = Mathf.Abs(local.x) > Mathf.Abs(local.z);
            if (eastWestEdge)
            {
                fixedPitchAxis = boardStartRot * Vector3.forward;
                pitchInputSign = local.x >= 0f ? 1f : -1f;
            }
            else
            {
                fixedPitchAxis = boardStartRot * Vector3.right;
                pitchInputSign = local.z >= 0f ? -1f : 1f;
            }
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
            if (grabVisualPrefab == null || grabAnchors == null) return;
            EnsureAnchorVisualInstances();
            for (int i = 0; i < grabAnchors.Length; i++)
            {
                var anchor = grabAnchors[i];
                if (anchor?.point == null) continue;
                bool active = grabbing && currentAnchorData == anchor;
                SetAnchorVisualActive(i, active, anchor.point.position);
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
                anchorVisualBaseScales.Add(instance.transform.localScale);
                ApplyAnchorVisualScale(instance, anchorVisualBaseScales.Count - 1);
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
            ApplyAnchorVisualScale(visual, index);

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

        private void ApplyAnchorVisualScale(GameObject visual, int index)
        {
            if (visual == null) return;
            Vector3 baseScale = (index >= 0 && index < anchorVisualBaseScales.Count)
                ? anchorVisualBaseScales[index]
                : visual.transform.localScale;
            visual.transform.localScale = baseScale * anchorVisualScale;
        }

        private void ApplyAnchorVisualScaleToAll()
        {
            for (int i = 0; i < anchorVisuals.Count; i++)
            {
                ApplyAnchorVisualScale(anchorVisuals[i], i);
            }
        }

        private void OnValidate()
        {
            anchorVisualScale = Mathf.Max(0.001f, anchorVisualScale);
            ApplyAnchorVisualScaleToAll();
        }
    }
}
