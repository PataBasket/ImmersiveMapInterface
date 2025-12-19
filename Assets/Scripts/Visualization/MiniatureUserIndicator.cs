using UnityEngine;
using ImmersiveMapInterface.Board;
using ImmersiveMapInterface.Interaction;

namespace ImmersiveMapInterface.Visualization
{
    /// <summary>
    /// Displays a small sphere on the miniature board that mirrors the player's position on the full-sized board.
    /// </summary>
    public class MiniatureUserIndicator : MonoBehaviour
    {
        private const int GridSize = 8;

        [Header("References")]
        public Transform playerTransform;
        public Transform worldBoardOrigin;
        public PoleBasedBoardGenerator worldBoardGenerator;
        public MiniaturePoleBoardGenerator miniatureBoard;
        public Transform indicatorParent;

        [Header("Indicator Visual")]
        public GameObject indicatorObject;
        public bool autoCreateIndicator = true;
        public float indicatorScale = 0.04f;
        [Tooltip("Offset above the miniature board surface when vertical mapping is disabled or unavailable.")]
        public float indicatorHeightOffset = 0.02f;
        [Tooltip("Initial local Y position applied to the indicator before tracking begins.")]
        public float initialIndicatorY = 0.06f;
        [Tooltip("Maximum local Y position the indicator is allowed to reach.")]
        public float indicatorHeightMax = 0.22f;
        public Color indicatorColor = new Color(0.1f, 0.6f, 1f, 0.95f);

        [Header("Mapping")]
        [Tooltip("Override for world-board half extent (meters). Leave < 0 to derive from PoleSpacing.")]
        public float worldHalfExtentOverride = -1f;
        [Tooltip("Override for miniature half extent (local meters). Leave < 0 to derive from generator settings.")]
        public float miniatureHalfExtentOverride = -1f;
        [Tooltip("Smoothing factor for indicator movement (0 = snap, higher = smoother).")]
        [Range(0f, 10f)] public float smoothing = 6f;
        [Tooltip("Automatically snap to the target position when the error exceeds this distance (meters).")]
        public float snapDistance = 0.15f;
        [Header("Vertical Mapping")]
        [Tooltip("If true, indicator height follows the player's Y position relative to the board.")]
        public bool mapVerticalPosition = true;
        [Tooltip("Automatically derive vertical ranges from geometry when possible.")]
        public bool autoVerticalRange = true;
        [Tooltip("Seconds between automatic range recalculations when autoVerticalRange is true.")]
        public float autoVerticalRecomputeInterval = 2f;
        [Tooltip("Manual world-board local Y range (used if autoVerticalRange is disabled or fails).")]
        public float worldVerticalMin = -0.5f;
        public float worldVerticalMax = 0.5f;
        [Tooltip("Manual miniature local Y range (used if autoVerticalRange is disabled or fails).")]
        public float miniatureVerticalMin = 0f;
        public float miniatureVerticalMax = 0.2f;
        [Header("Movement Bounds Mapping")]
        [Tooltip("If true, world vertical mapping uses the same movement bounds as the player locomotion.")]
        public bool useMovementBounds = false;
        [Tooltip("Attempt to auto-bind BirdHeadLocomotion to read movement bounds.")]
        public bool autoBindLocomotionBounds = true;
        public BirdHeadLocomotion locomotionSource;
        public Transform movementBoundsCenter;
        public Vector3 movementBoundsHalfExtents = new Vector3(4f, 4f, 4f);
        [Header("Vertical Latch")]
        [Tooltip("Keep the indicator's height fixed at spawn until the player intentionally moves vertically.")]
        public bool latchVerticalUntilMove = true;
        [Tooltip("If true, board-space vertical displacement can also release the latch.")]
        public bool releaseOnBoardDisplacement = false;
        [Tooltip("Meters (board local space) the player must move vertically to release the latch (when enabled).")]
        public float verticalReleaseThreshold = 0.05f;
        [Tooltip("Analog input magnitude (0-1) required from the locomotion vertical stick to release the latch.")]
        public float verticalInputReleaseThreshold = 0.25f;
        [Header("Idle snapping")]
        [Tooltip("Snap immediately to target when locomotion input is idle (prevents slow drift after releasing sticks).")]
        public bool snapWhenInputIdle = true;
        [Tooltip("Primary stick magnitude under which locomotion is considered idle for snapping purposes.")]
        public float idleInputThreshold = 0.02f;

        private Transform indicatorTransform;
        private Vector3 currentLocalPosition;
        private float nextAutoRangeTime = -1f;
        private float autoWorldMin;
        private float autoWorldMax;
        private float autoMiniMin;
        private float autoMiniMax;
        private bool autoWorldValid;
        private bool autoMiniValid;
        private float resolvedIndicatorHeight;
        private bool hasSnappedOnce;
        private bool indicatorReady;
        private bool verticalReleased;
        private float latchedBoardLocalY;
        private float latchedIndicatorY;

        private void Awake()
        {
            if (miniatureBoard == null)
            {
                miniatureBoard = GetComponentInChildren<MiniaturePoleBoardGenerator>(true);
            }
            if (worldBoardGenerator == null)
            {
                worldBoardGenerator = FindObjectOfType<PoleBasedBoardGenerator>();
            }
            if (worldBoardOrigin == null && worldBoardGenerator != null)
            {
                worldBoardOrigin = worldBoardGenerator.transform;
            }
            if (indicatorParent == null && miniatureBoard != null)
            {
                indicatorParent = miniatureBoard.transform;
            }
            resolvedIndicatorHeight = initialIndicatorY;
            InitializeIndicator();
        }

        private void Start()
        {
            UpdateIndicatorPosition(true);
        }

        private void LateUpdate()
        {
            if (!indicatorReady) return;
            UpdateIndicatorPosition(false);
        }

        private void UpdateIndicatorPosition(bool instant)
        {
            if (!indicatorReady || playerTransform == null || worldBoardOrigin == null || indicatorTransform == null || indicatorParent == null || miniatureBoard == null)
            {
                return;
            }

            if (autoVerticalRange && Time.time >= nextAutoRangeTime)
            {
                RecomputeAutoVerticalRanges();
            }

            float worldHalf = GetWorldHalfExtent();
            float miniHalf = GetMiniatureHalfExtent(worldHalf);
            if (worldHalf <= 0.0001f || miniHalf <= 0.0001f)
            {
                return;
            }

            Vector3 localOnBoard = worldBoardOrigin.InverseTransformPoint(playerTransform.position);
            float normX = Mathf.Clamp(localOnBoard.x / worldHalf, -1f, 1f);
            float normZ = Mathf.Clamp(localOnBoard.z / worldHalf, -1f, 1f);

            float heightInput = localOnBoard.y;
            bool shouldRelease = ShouldReleaseLatch(localOnBoard.y);
            if (shouldRelease && latchVerticalUntilMove && !verticalReleased)
            {
                verticalReleased = true;
                RefreshResolvedHeight(localOnBoard.y);
            }
            else if (!latchVerticalUntilMove || verticalReleased)
            {
                RefreshResolvedHeight(heightInput);
            }

            float targetY = (latchVerticalUntilMove && !verticalReleased) ? latchedIndicatorY : resolvedIndicatorHeight;
            Vector3 targetLocal = new Vector3(normX * miniHalf, targetY, normZ * miniHalf);
            targetLocal.y = Mathf.Min(targetLocal.y, indicatorHeightMax);
            currentLocalPosition = targetLocal;
            hasSnappedOnce = true;

            indicatorTransform.localPosition = currentLocalPosition;
        }

        private void EnsureIndicatorObject()
        {
            if (indicatorObject == null && autoCreateIndicator)
            {
                indicatorObject = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                indicatorObject.name = "MiniatureUserIndicator";
                var collider = indicatorObject.GetComponent<Collider>();
                if (collider != null)
                {
                    if (Application.isPlaying)
                        Destroy(collider);
                    else
                        DestroyImmediate(collider);
                }
                var renderer = indicatorObject.GetComponent<Renderer>();
                if (renderer != null)
                {
                    var shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Standard");
                    if (shader != null)
                    {
                        var mat = new Material(shader);
                        mat.color = indicatorColor;
                        renderer.sharedMaterial = mat;
                    }
                }
            }

            if (indicatorObject != null)
            {
                indicatorTransform = indicatorObject.transform;
                indicatorTransform.SetParent(indicatorParent != null ? indicatorParent : transform, false);
                indicatorTransform.localRotation = Quaternion.identity;
                indicatorTransform.localScale = Vector3.one * indicatorScale;
                currentLocalPosition = indicatorTransform.localPosition;
                hasSnappedOnce = false;
                indicatorReady = false;
            }
        }

        private void LogPositionAfterOneSecond()
        {
            if (indicatorTransform == null) return;
            var local = indicatorTransform.localPosition;
            var world = indicatorTransform.position;
            Debug.Log($"MiniatureUserIndicator: +1s local {local.ToString("F3")} world {world.ToString("F3")}", this);
        }

        private void ApplyInitialLocalPosition(float worldHalf, float miniHalf)
        {
            Vector3 initialLocal = ComputeInitialLocalPosition(worldHalf, miniHalf);
            currentLocalPosition = initialLocal;
            indicatorTransform.localPosition = initialLocal;
            hasSnappedOnce = false;
            latchedIndicatorY = initialLocal.y;
            latchedBoardLocalY = (worldBoardOrigin != null && playerTransform != null)
                ? worldBoardOrigin.InverseTransformPoint(playerTransform.position).y
                : initialIndicatorY;
            verticalReleased = !latchVerticalUntilMove;
            indicatorReady = true;
            Debug.Log($"MiniatureUserIndicator: spawned local {initialLocal.ToString("F3")} world {indicatorTransform.position.ToString("F3")}", this);
            Invoke(nameof(LogPositionAfterOneSecond), 1f);
        }

        private Vector3 ComputeInitialLocalPosition(float worldHalf, float miniHalf)
        {
            float initialY = Mathf.Clamp(initialIndicatorY, miniatureVerticalMin, miniatureVerticalMax);
            Vector3 local = new Vector3(0f, initialY, 0f);
            if (playerTransform == null || worldBoardOrigin == null || worldHalf <= 0.0001f)
            {
                return local;
            }

            Vector3 localOnBoard = worldBoardOrigin.InverseTransformPoint(playerTransform.position);
            float normX = Mathf.Clamp(localOnBoard.x / worldHalf, -1f, 1f);
            float normZ = Mathf.Clamp(localOnBoard.z / worldHalf, -1f, 1f);
            local.x = normX * miniHalf;
            local.z = normZ * miniHalf;
            return local;
        }

        private bool ShouldReleaseLatch(float boardLocalY)
        {
            if (!latchVerticalUntilMove || verticalReleased) return false;
            bool inputRelease = locomotionSource != null && Mathf.Abs(locomotionSource.LastVerticalInput) >= verticalInputReleaseThreshold;
            if (inputRelease) return true;

            if (releaseOnBoardDisplacement && !float.IsNaN(latchedBoardLocalY))
            {
                float deviation = Mathf.Abs(boardLocalY - latchedBoardLocalY);
                if (deviation >= verticalReleaseThreshold)
                {
                    return true;
                }
            }
            return false;
        }

        private float GetWorldHalfExtent()
        {
            if (worldHalfExtentOverride > 0f) return worldHalfExtentOverride;
            float spacing = worldBoardGenerator != null ? Mathf.Max(0.0001f, worldBoardGenerator.PoleSpacing) : 2f;
            return (GridSize - 1) * spacing * 0.5f;
        }

        private float GetMiniatureHalfExtent(float worldHalf)
        {
            if (miniatureHalfExtentOverride > 0f) return miniatureHalfExtentOverride;
            if (miniatureBoard == null) return worldHalf;

            if (miniatureBoard.UseWorldScale && worldBoardGenerator != null)
            {
                float scale = miniatureBoard.MiniatureScale;
                if (scale > 0f)
                {
                    return worldHalf * scale;
                }
            }

            float spacing = Mathf.Max(0.0001f, miniatureBoard.PoleSpacing);
            return (GridSize - 1) * spacing * 0.5f;
        }

        private float GetMappedHeight(float boardLocalY)
        {
            float worldMin = GetWorldMinBound();
            float worldMax = GetWorldMaxBound();
            float miniMin = GetMiniMinBound();
            float miniMax = GetMiniMaxBound();

            float range = Mathf.Max(0.0001f, worldMax - worldMin);
            float t = Mathf.Clamp01((boardLocalY - worldMin) / range);
            return Mathf.Lerp(miniMin, miniMax, t);
        }

        private void RecomputeAutoVerticalRanges(bool force = false)
        {
            if (!autoVerticalRange) return;
            if (!force && Time.time < nextAutoRangeTime) return;

            bool prevValid = autoWorldValid && autoMiniValid;
            autoWorldValid = TryComputeLocalVerticalBounds(worldBoardOrigin, out autoWorldMin, out autoWorldMax);
            autoMiniValid = TryComputeLocalVerticalBounds(indicatorParent != null ? indicatorParent : transform, out autoMiniMin, out autoMiniMax);

            if (!autoWorldValid)
            {
                autoWorldMin = worldVerticalMin;
                autoWorldMax = worldVerticalMax;
            }
            if (!autoMiniValid)
            {
                autoMiniMin = miniatureVerticalMin;
                autoMiniMax = miniatureVerticalMax;
            }
            if (playerTransform != null && worldBoardOrigin != null)
            {
                float localY = worldBoardOrigin.InverseTransformPoint(playerTransform.position).y;
                RefreshResolvedHeight(localY);
            }
            else
            {
                resolvedIndicatorHeight = (autoMiniValid ? autoMiniMin : miniatureVerticalMin) + indicatorHeightOffset;
            }

            if (!prevValid && autoWorldValid && autoMiniValid)
            {
                UpdateIndicatorPosition(true);
            }

            float delay = (autoWorldValid && autoMiniValid)
                ? Mathf.Max(0.1f, autoVerticalRecomputeInterval)
                : 0.1f;
            nextAutoRangeTime = Time.time + delay;
        }

        private static bool TryComputeLocalVerticalBounds(Transform root, out float minY, out float maxY)
        {
            minY = float.PositiveInfinity;
            maxY = float.NegativeInfinity;
            if (root == null) return false;

            bool found = false;
            var renderers = root.GetComponentsInChildren<Renderer>(true);
            foreach (var rend in renderers)
            {
                if (rend == null) continue;
                found |= ExpandBounds(root, rend.bounds, ref minY, ref maxY);
            }
            var colliders = root.GetComponentsInChildren<Collider>(true);
            foreach (var col in colliders)
            {
                if (col == null) continue;
                found |= ExpandBounds(root, col.bounds, ref minY, ref maxY);
            }

            if (!found)
            {
                minY = 0f;
                maxY = 0.1f;
            }

            return found;
        }

        private static bool ExpandBounds(Transform root, Bounds bounds, ref float minY, ref float maxY)
        {
            Vector3 min = bounds.min;
            Vector3 max = bounds.max;
            Vector3[] corners =
            {
                new Vector3(min.x, min.y, min.z),
                new Vector3(max.x, min.y, min.z),
                new Vector3(min.x, max.y, min.z),
                new Vector3(max.x, max.y, min.z),
                new Vector3(min.x, min.y, max.z),
                new Vector3(max.x, min.y, max.z),
                new Vector3(min.x, max.y, max.z),
                new Vector3(max.x, max.y, max.z)
            };

            bool updated = false;
            for (int i = 0; i < corners.Length; i++)
            {
                Vector3 local = root.InverseTransformPoint(corners[i]);
                if (local.y < minY) minY = local.y;
                if (local.y > maxY) maxY = local.y;
                updated = true;
            }
            return updated;
        }

        private void RefreshResolvedHeight(float boardLocalY)
        {
            float miniMin = GetMiniMinBound();
            if (mapVerticalPosition)
            {
                float miniMax = GetMiniMaxBound();
                float worldMin = GetWorldMinBound();
                float worldMax = GetWorldMaxBound();
                float worldRange = Mathf.Max(0.0001f, worldMax - worldMin);
                float miniRange = Mathf.Max(0.0001f, miniMax - miniMin);
                float normalized = Mathf.Clamp01((boardLocalY - worldMin) / worldRange);
                resolvedIndicatorHeight = miniMin + normalized * miniRange;
            }
            else
            {
                resolvedIndicatorHeight = miniMin + indicatorHeightOffset;
            }
        }

        private float GetWorldMinBound()
        {
            if (useMovementBounds && movementBoundsCenter != null && worldBoardOrigin != null)
            {
                float centerLocal = worldBoardOrigin.InverseTransformPoint(movementBoundsCenter.position).y;
                return centerLocal - Mathf.Abs(movementBoundsHalfExtents.y);
            }
            return autoVerticalRange && autoWorldValid ? autoWorldMin : worldVerticalMin;
        }

        private float GetWorldMaxBound()
        {
            if (useMovementBounds && movementBoundsCenter != null && worldBoardOrigin != null)
            {
                float centerLocal = worldBoardOrigin.InverseTransformPoint(movementBoundsCenter.position).y;
                return centerLocal + Mathf.Abs(movementBoundsHalfExtents.y);
            }
            return autoVerticalRange && autoWorldValid ? autoWorldMax : worldVerticalMax;
        }

        private float GetMiniMinBound()
        {
            return autoVerticalRange && autoMiniValid ? autoMiniMin : miniatureVerticalMin;
        }

        private float GetMiniMaxBound()
        {
            return autoVerticalRange && autoMiniValid ? autoMiniMax : miniatureVerticalMax;
        }

        private void SyncMovementBoundsFromSource()
        {
            if (!useMovementBounds) return;
            if (locomotionSource == null && autoBindLocomotionBounds)
            {
                locomotionSource = FindObjectOfType<BirdHeadLocomotion>();
            }
            if (locomotionSource != null && locomotionSource.BoundsActive)
            {
                if (movementBoundsCenter == null)
                {
                    movementBoundsCenter = locomotionSource.BoundsCenter;
                }
                movementBoundsHalfExtents = locomotionSource.BoundsHalfSize;
            }
        }

        public void ConfigureMovementBounds(Transform center, Vector3 halfExtents)
        {
            if (center != null)
            {
                movementBoundsCenter = center;
            }
            movementBoundsHalfExtents = halfExtents;
            useMovementBounds = true;
            ForceImmediateSync();
        }

        public void ForceImmediateSync()
        {
            if (!indicatorReady) return;
            UpdateIndicatorPosition(true);
        }

        private void InitializeIndicator()
        {
            EnsureIndicatorObject();
            SyncMovementBoundsFromSource();
            RecomputeAutoVerticalRanges(true);
            float worldHalf = GetWorldHalfExtent();
            float miniHalf = GetMiniatureHalfExtent(worldHalf);
            if (indicatorTransform != null && worldHalf > 0.0001f && miniHalf > 0.0001f)
            {
                ApplyInitialLocalPosition(worldHalf, miniHalf);
            }
            else if (indicatorTransform != null)
            {
                currentLocalPosition = new Vector3(0f, Mathf.Clamp(initialIndicatorY, miniatureVerticalMin, miniatureVerticalMax), 0f);
                indicatorTransform.localPosition = currentLocalPosition;
                indicatorReady = true;
                hasSnappedOnce = false;
                Debug.Log($"MiniatureUserIndicator: spawned local {currentLocalPosition.ToString("F3")} world {indicatorTransform.position.ToString("F3")}", this);
                Invoke(nameof(LogPositionAfterOneSecond), 1f);
            }
            UpdateIndicatorPosition(true);
        }

    }
}
