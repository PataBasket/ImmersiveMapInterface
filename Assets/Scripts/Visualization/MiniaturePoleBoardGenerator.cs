using System;
using System.Collections.Generic;
using UnityEngine;
using ImmersiveMapInterface.Board;

namespace ImmersiveMapInterface.Visualization
{
    // Local-space miniature visualizer for PoleBasedBoardState.
    // Spawns piecePrefab as children with local positions (independent of actual poles in the scene).
    public class MiniaturePoleBoardGenerator : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private PoleBasedBoardState boardState;
        [SerializeField] private GameObject piecePrefab;
        [SerializeField] private bool autoFindBoardState = true;
        [Tooltip("Play開始時に既存の駒を保持したい場合は false に設定。true のままなら OnEnable で常に再生成します。")]
        [SerializeField] private bool autoGenerateOnEnable = false;
        [Tooltip("Optional: assign a pole layout (e.g., WorldInMiniature with child pole_# objects) to match spacing/rotation.")]
        [SerializeField] private Transform poleLayoutRoot;
        [SerializeField] private bool usePoleLayout = true;
        [SerializeField] private bool inheritPoleOrientation = true;
        [Tooltip("Additional offset applied to every slot along the local up direction (meters). Use negative values to sink stacks into the base.")]
        [SerializeField] private float stackVerticalOffset = 0f;

        [Header("World Scale (optional)")]
        [Tooltip("If true, pole positions are copied from the real board (worldBoardRoot) and scaled by miniatureScale. Leave off to rely on poleLayoutRoot/local spacing.")]
        [SerializeField] private bool useWorldScale = false;
        [Tooltip("Root transform of the real board (e.g., Ground). Child objects named pole_XX are used as references.")]
        [SerializeField] private Transform worldBoardRoot;
        [Tooltip("Pivot transform representing the board's center/origin (e.g., BoardRoot). Positions are taken in this local space before scaling.")]
        [SerializeField] private Transform worldPivot;
        [Tooltip("Local-space offset applied (in worldPivot space) before scaling. Useful if pivot is not at the board center.")]
        [SerializeField] private Vector3 worldPivotLocalOffset = Vector3.zero;
        [Tooltip("Scale applied to the world board to create the miniature. Example: 0.025 = 1/40.")]
        [SerializeField][Min(0f)] private float miniatureScale = 0.025f;
        [Tooltip("Automatically scale vertical spacing using worldSlotSpacing * miniatureScale.")]
        [SerializeField] private bool autoScaleSlotSpacing = true;
        [Tooltip("Distance (in world units) between successive pieces along a pole on the real board.")]
        [SerializeField] private float worldSlotSpacing = 1.0f;
        [Tooltip("If true, pieceScale is multiplied by miniatureScale.")]
        [SerializeField] private bool scalePieceSizeWithMiniature = false;

        [Header("Reference Board Sync")]
        [Tooltip("Copy dimensions from a world board generator and apply miniatureScale.")]
        [SerializeField] private bool syncFromReferenceBoard = false;
        [SerializeField] private PoleBasedBoardGenerator referenceBoard;

        [Header("Appearance")]
        [SerializeField] private float poleSpacing = 0.2f; // miniature grid spacing on XZ (used if poleParent is null)
        [SerializeField] private float pieceSpacing = 0.15f; // miniature vertical spacing (Y)
        [SerializeField] private float pieceScale = 0.12f;
        [SerializeField] private Vector3 pieceRotationEuler = new Vector3(0f,0f,0f);
        [SerializeField] private Material whiteMaterial;
        [SerializeField] private Material blackMaterial;
        [SerializeField] private Material emptyMaterial;
        [SerializeField] private Color fallbackWhiteColor = new Color(0.9f, 0.9f, 0.9f, 1f);
        [SerializeField] private Color fallbackBlackColor = new Color(0.1f, 0.1f, 0.1f, 1f);
        [SerializeField] private Color fallbackEmptyColor = new Color(0.2f, 0.2f, 0.2f, 0.2f);

        [Header("Prefab Handling")]
        [Tooltip("If true, automatically counteracts the prefab's existing transform scale so that pieceScale represents the final size.")]
        [SerializeField] private bool compensatePrefabScale = false;
        [Tooltip("If true, generated miniature pieces inherit this object's layer.")]
        [SerializeField] private bool inheritLayerFromRoot = true;

        private Renderer[,] pieceRenderers = new Renderer[PoleBasedBoardState.PoleCount, PoleBasedBoardState.PiecesPerPole];
        private GameObject[,] pieceObjects = new GameObject[PoleBasedBoardState.PoleCount, PoleBasedBoardState.PiecesPerPole];
        private PoleBasedBoardState.PieceColor[,] renderedColors = new PoleBasedBoardState.PieceColor[PoleBasedBoardState.PoleCount, PoleBasedBoardState.PiecesPerPole];
        private static MaterialPropertyBlock propertyBlock;
        private Dictionary<int, Vector3> layoutLocalPositions;
        private Dictionary<int, Quaternion> layoutLocalRotations;
        private Vector3 prefabScaleCompensation = Vector3.one;
        private bool prefabScaleCached = false;
        private GameObject cachedPrefabForScale;
        private bool boardStateSubscribed = false;
        private const int ExpectedPieceCount = PoleBasedBoardState.PoleCount * PoleBasedBoardState.PiecesPerPole;


        private void OnValidate()
        {
            prefabScaleCached = false;
            cachedPrefabForScale = null;
        }

        private void Awake()
        {
            EnsureBoardState();
        }

        private void OnEnable()
        {
            EnsureBoardState();
            if (autoGenerateOnEnable)
            {
                EnsureGenerated();
            }
            else if (!AdoptExistingPieces())
            {
                EnsureGenerated();
            }
            else
            {
                EnsurePropertyBlock();
            }

            SubscribeBoardState();
            HandleBoardReset();
        }

        private void OnDisable()
        {
            UnsubscribeBoardState();
        }

        public void SetBoardState(PoleBasedBoardState newBoardState)
        {
            if (boardState == newBoardState) return;
            UnsubscribeBoardState();
            boardState = newBoardState;
            if (isActiveAndEnabled)
            {
                EnsureBoardState();
                if (autoGenerateOnEnable || transform.childCount == 0)
                {
                    EnsureGenerated();
                }
                else if (!AdoptExistingPieces())
                {
                    EnsureGenerated();
                }
                else
                {
                    EnsurePropertyBlock();
                }

                SubscribeBoardState();
                HandleBoardReset();
            }
        }

        [ContextMenu("Ensure Generated")]
        public void EnsureGenerated()
        {
            if (piecePrefab == null)
            {
                Debug.LogError("MiniaturePoleBoardGenerator: piecePrefab not assigned.");
                return;
            }

            ApplyReferenceBoardSettings();
            CachePoleLayout();
            EnsurePrefabScaleCompensation();

            float slotSpacingUsed = pieceSpacing;
            if (useWorldScale && autoScaleSlotSpacing)
            {
                slotSpacingUsed = worldSlotSpacing * miniatureScale;
            }
            float pieceScaleUsed = pieceScale;
            if (useWorldScale && scalePieceSizeWithMiniature)
            {
                pieceScaleUsed = pieceScale * miniatureScale;
            }

            ClearPieces();
            renderedColors = new PoleBasedBoardState.PieceColor[PoleBasedBoardState.PoleCount, PoleBasedBoardState.PiecesPerPole];
            for (int pole = 0; pole < PoleBasedBoardState.PoleCount; pole++)
            {
                PoleBasedBoardState.PoleIndexToGrid(pole, out int x, out int z);
                for (int slot = 0; slot < PoleBasedBoardState.PiecesPerPole; slot++)
                {
                    var go = Instantiate(piecePrefab, transform);
                    go.name = $"MiniPiece_P{pole}_S{slot}";
                    go.transform.localScale = ApplyScaleCompensation(Vector3.one * pieceScaleUsed);
                    if (inheritLayerFromRoot)
                    {
                        SetLayerRecursively(go.transform, gameObject.layer);
                    }
                    Vector3 localBase = LocalPoleCenter(x, z);
                    if (layoutLocalPositions != null && layoutLocalPositions.TryGetValue(pole, out var layoutPos))
                    {
                        localBase = layoutPos;
                    }
                    Vector3 upDir = Vector3.up;
                    if (inheritPoleOrientation && layoutLocalRotations != null && layoutLocalRotations.TryGetValue(pole, out var rotForUp))
                    {
                        upDir = rotForUp * Vector3.up;
                    }
                    float vertical = stackVerticalOffset + (slot + 0.5f) * slotSpacingUsed;
                    Vector3 localPos = localBase + upDir.normalized * vertical;
                    go.transform.localPosition = localPos;

                    Quaternion layoutRot = Quaternion.identity;
                    if (inheritPoleOrientation && layoutLocalRotations != null && layoutLocalRotations.TryGetValue(pole, out var rot))
                    {
                        layoutRot = rot;
                    }
                    go.transform.localRotation = layoutRot * Quaternion.Euler(pieceRotationEuler);
                    pieceObjects[pole, slot] = go;
                    pieceRenderers[pole, slot] = go.GetComponentInChildren<Renderer>();
                }
            }
            HandleBoardReset();
            EnsurePropertyBlock();
        }

        private Vector3 LocalPoleCenter(int x, int z)
        {
            float extent = (8 - 1) * poleSpacing * 0.5f;
            return new Vector3(x * poleSpacing - extent, 0f, z * poleSpacing - extent);
        }

        private void ClearPieces()
        {
            DestroyGeneratedChildren();
            ResetPieceArrays();
        }

        private void DestroyGeneratedChildren()
        {
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                var child = transform.GetChild(i);
                if (child == null) continue;
                if (!ShouldRemoveChild(child)) continue;
                DestroyPieceObject(child.gameObject);
            }
        }

        private static bool ShouldRemoveChild(Transform child)
        {
            if (child == null) return false;
            string name = child.name;
            return name.StartsWith("MiniPiece_", StringComparison.OrdinalIgnoreCase) ||
                   name.StartsWith("Piece_", StringComparison.OrdinalIgnoreCase);
        }

        private static void DestroyPieceObject(GameObject go)
        {
            if (go == null) return;
            if (Application.isPlaying)
                Destroy(go);
            else
                DestroyImmediate(go);
        }

        private void ResetPieceArrays()
        {
            for (int pole = 0; pole < PoleBasedBoardState.PoleCount; pole++)
            {
                for (int slot = 0; slot < PoleBasedBoardState.PiecesPerPole; slot++)
                {
                    pieceObjects[pole, slot] = null;
                    pieceRenderers[pole, slot] = null;
                }
            }
        }

        private bool AdoptExistingPieces()
        {
            if (transform.childCount == 0) return false;

            ResetPieceArrays();
            renderedColors = new PoleBasedBoardState.PieceColor[PoleBasedBoardState.PoleCount, PoleBasedBoardState.PiecesPerPole];

            int mapped = 0;
            for (int i = 0; i < transform.childCount; i++)
            {
                var child = transform.GetChild(i);
                if (child == null) continue;
                if (!TryParsePieceName(child.name, out int poleIndex, out int slotIndex)) continue;
                if (!PoleBasedBoardState.IsValidPoleSlot(poleIndex, slotIndex)) continue;

                pieceObjects[poleIndex, slotIndex] = child.gameObject;
                pieceRenderers[poleIndex, slotIndex] = child.GetComponentInChildren<Renderer>(true);
                mapped++;
            }

            if (mapped == 0)
            {
                Debug.LogWarning("MiniaturePoleBoardGenerator: 既存の MiniPiece 子オブジェクトを検出できませんでした。Ensure Generated を実行して再作成します。", this);
                ResetPieceArrays();
                return false;
            }

            if (mapped != ExpectedPieceCount)
            {
                Debug.LogWarning($"MiniaturePoleBoardGenerator: MiniPiece を {mapped} 個だけ検出しました (期待 {ExpectedPieceCount})。不足分は必要に応じて Ensure Generated で補ってください (自動再生成は行いません)。", this);
            }

            return true;
        }

        private static bool TryParsePieceName(string name, out int poleIndex, out int slotIndex)
        {
            poleIndex = -1;
            slotIndex = -1;
            if (string.IsNullOrEmpty(name)) return false;

            int poleMarker = name.IndexOf("_P", StringComparison.OrdinalIgnoreCase);
            int slotMarker = name.IndexOf("_S", StringComparison.OrdinalIgnoreCase);
            if (poleMarker < 0 || slotMarker < 0 || slotMarker <= poleMarker) return false;

            int poleStart = poleMarker + 2;
            int poleEnd = name.IndexOf('_', poleStart);
            if (poleEnd < 0 || poleEnd > slotMarker) poleEnd = slotMarker;
            if (poleEnd <= poleStart) return false;

            if (!int.TryParse(name.Substring(poleStart, poleEnd - poleStart), out poleIndex)) return false;

            int slotStart = slotMarker + 2;
            int slotEnd = name.IndexOf('_', slotStart);
            if (slotEnd < 0) slotEnd = name.Length;
            if (slotEnd <= slotStart) return false;

            if (!int.TryParse(name.Substring(slotStart, slotEnd - slotStart), out slotIndex)) return false;

            return true;
        }

        private void EnsureBoardState()
        {
            if (boardState == null && autoFindBoardState)
            {
                boardState = FindObjectOfType<PoleBasedBoardState>();
            }
            if (boardState == null)
            {
                Debug.LogWarning("MiniaturePoleBoardGenerator: boardState not assigned. Pieces will not reflect actual board colors.", this);
            }

            if (useWorldScale && worldBoardRoot == null)
            {
                var ground = GameObject.Find("Ground");
                if (ground != null) worldBoardRoot = ground.transform;
            }
            if (useWorldScale && worldPivot == null)
            {
                worldPivot = worldBoardRoot;
            }
        }

        private void SubscribeBoardState()
        {
            if (boardState == null || boardStateSubscribed) return;
            boardState.OnPieceChanged += HandlePieceChanged;
            boardState.OnBoardReset += HandleBoardReset;
            boardStateSubscribed = true;
            Debug.Log($"MiniaturePoleBoardGenerator: Subscribed to board state '{boardState.name}'.", this);
        }

        private void UnsubscribeBoardState()
        {
            if (boardState == null || !boardStateSubscribed) return;
            boardState.OnPieceChanged -= HandlePieceChanged;
            boardState.OnBoardReset -= HandleBoardReset;
            boardStateSubscribed = false;
            Debug.Log("MiniaturePoleBoardGenerator: Unsubscribed from board state.", this);
        }

        private Vector3 ApplyScaleCompensation(Vector3 targetScale)
        {
            if (!compensatePrefabScale) return targetScale;
            EnsurePrefabScaleCompensation();
            return Vector3.Scale(targetScale, prefabScaleCompensation);
        }

        private void EnsurePrefabScaleCompensation()
        {
            if (!compensatePrefabScale)
            {
                prefabScaleCompensation = Vector3.one;
                prefabScaleCached = true;
                cachedPrefabForScale = piecePrefab;
                return;
            }

            if (piecePrefab == null)
            {
                prefabScaleCompensation = Vector3.one;
                prefabScaleCached = false;
                cachedPrefabForScale = null;
                return;
            }

            if (prefabScaleCached && cachedPrefabForScale == piecePrefab)
            {
                return;
            }

            cachedPrefabForScale = piecePrefab;
            prefabScaleCached = true;

            var sourceScale = piecePrefab.transform.localScale;
            prefabScaleCompensation = new Vector3(
                SafeInverse(sourceScale.x),
                SafeInverse(sourceScale.y),
                SafeInverse(sourceScale.z)
            );
        }

        private static float SafeInverse(float value)
        {
            const float epsilon = 1e-5f;
            return Mathf.Abs(value) <= epsilon ? 1f : 1f / value;
        }

        private void CachePoleLayout()
        {
            layoutLocalPositions = null;
            layoutLocalRotations = null;

            bool world = useWorldScale && worldBoardRoot != null;
            bool localLayout = usePoleLayout && poleLayoutRoot != null;
            if (!world && !localLayout) return;

            layoutLocalPositions = new Dictionary<int, Vector3>(PoleBasedBoardState.PoleCount);
            if (inheritPoleOrientation)
            {
                layoutLocalRotations = new Dictionary<int, Quaternion>(PoleBasedBoardState.PoleCount);
            }

            Transform sourceRoot = world ? worldBoardRoot : poleLayoutRoot;
            Transform pivot = world ? (worldPivot != null ? worldPivot : worldBoardRoot) : transform;
            Quaternion relativeRot = Quaternion.identity;
            if (world && pivot != null)
            {
                relativeRot = Quaternion.Inverse(transform.rotation) * pivot.rotation;
            }

            foreach (Transform child in sourceRoot.GetComponentsInChildren<Transform>(true))
            {
                if (!TryParsePoleIndex(child.name, out int poleIndex)) continue;

                Vector3 localPos;
                Quaternion localRot = Quaternion.identity;

                if (world)
                {
                    Vector3 pivotLocal = pivot != null ? pivot.InverseTransformPoint(child.position) : child.localPosition;
                    pivotLocal -= worldPivotLocalOffset;
                    localPos = relativeRot * (pivotLocal * miniatureScale);
                    if (inheritPoleOrientation && layoutLocalRotations != null)
                    {
                        localRot = Quaternion.identity;
                    }
                }
                else
                {
                    localPos = transform.InverseTransformPoint(child.position);
                    if (inheritPoleOrientation && layoutLocalRotations != null)
                    {
                        localRot = Quaternion.Inverse(transform.rotation) * child.rotation;
                    }
                }

                layoutLocalPositions[poleIndex] = localPos;
                if (inheritPoleOrientation && layoutLocalRotations != null)
                {
                    layoutLocalRotations[poleIndex] = localRot;
                }
            }

            int count = layoutLocalPositions != null ? layoutLocalPositions.Count : 0;
            if (count != PoleBasedBoardState.PoleCount)
            {
                Debug.LogWarning($"MiniaturePoleBoardGenerator: cached {count} pole transforms (expected {PoleBasedBoardState.PoleCount}). Check pole naming under '{(sourceRoot != null ? sourceRoot.name : "<null>")}'.", this);
            }
            else
            {
                Debug.Log($"MiniaturePoleBoardGenerator: cached all {count} pole transforms.", this);
            }
        }

        private void ApplyReferenceBoardSettings()
        {
            if (!syncFromReferenceBoard || referenceBoard == null) return;

            float scaleFactor = miniatureScale > 0f ? miniatureScale : 1f;

            if (referenceBoard.PoleSpacing > 0f)
            {
                poleSpacing = referenceBoard.PoleSpacing * scaleFactor;
            }

            if (referenceBoard.PieceSpacing > 0f)
            {
                if (useWorldScale)
                {
                    worldSlotSpacing = referenceBoard.PieceSpacing;
                }
                pieceSpacing = referenceBoard.PieceSpacing * scaleFactor;
            }

            if (referenceBoard.PieceScale > 0f)
            {
                pieceScale = referenceBoard.PieceScale * scaleFactor;
            }
        }

        private void HandlePieceChanged(int pole, int slot, PoleBasedBoardState.PieceColor color)
        {
            UpdateRenderer(pole, slot, color);
        }

        private void HandleBoardReset()
        {
            if (boardState == null) return;
            for (int pole = 0; pole < PoleBasedBoardState.PoleCount; pole++)
            {
                for (int slot = 0; slot < PoleBasedBoardState.PiecesPerPole; slot++)
                {
                    UpdateRenderer(pole, slot, boardState.GetPiece(pole, slot));
                }
            }
        }

        private void UpdateRenderer(int pole, int slot, PoleBasedBoardState.PieceColor color)
        {
            var r = pieceRenderers[pole, slot];
            if (r == null) return;

            Material mat = color switch
            {
                PoleBasedBoardState.PieceColor.White => whiteMaterial,
                PoleBasedBoardState.PieceColor.Black => blackMaterial,
                PoleBasedBoardState.PieceColor.Empty => emptyMaterial,
                _ => emptyMaterial
            };

            if (mat != null)
            {
                r.sharedMaterial = mat;
                r.SetPropertyBlock(null);
            }
            else
            {
                Color fallback = color switch
                {
                    PoleBasedBoardState.PieceColor.White => fallbackWhiteColor,
                    PoleBasedBoardState.PieceColor.Black => fallbackBlackColor,
                    _ => fallbackEmptyColor
                };
                EnsurePropertyBlock();
                propertyBlock.Clear();
                propertyBlock.SetColor("_BaseColor", fallback);
                propertyBlock.SetColor("_Color", fallback);
                r.SetPropertyBlock(propertyBlock);
            }
            renderedColors[pole, slot] = color;
        }

        [ContextMenu("Validate Against BoardState")]
        private void ValidateAgainstBoardState()
        {
            if (boardState == null)
            {
                Debug.LogWarning("MiniaturePoleBoardGenerator: cannot validate without boardState.", this);
                return;
            }

            int mismatch = 0;
            for (int pole = 0; pole < PoleBasedBoardState.PoleCount; pole++)
            {
                for (int slot = 0; slot < PoleBasedBoardState.PiecesPerPole; slot++)
                {
                    var expected = boardState.GetPiece(pole, slot);
                    var actual = renderedColors[pole, slot];
                    if (expected != actual)
                    {
                        mismatch++;
                        if (mismatch < 5)
                        {
                            Debug.LogWarning($"Miniature mismatch: pole {pole} slot {slot} expected {expected} but rendered {actual}.", this);
                        }
                    }
                }
            }

            if (mismatch == 0)
            {
                Debug.Log("Miniature validation passed: all rendered colors match boardState.", this);
            }
            else
            {
                Debug.LogWarning($"Miniature validation found {mismatch} mismatched cells. See warnings above for samples.", this);
            }
        }

        private void EnsurePropertyBlock()
        {
            if (propertyBlock == null)
            {
                propertyBlock = new MaterialPropertyBlock();
            }
        }

        private static bool TryParsePoleIndex(string name, out int poleIndex)
        {
            poleIndex = -1;
            const string prefix = "pole_";
            int idx = name.IndexOf(prefix, System.StringComparison.OrdinalIgnoreCase);
            if (idx >= 0)
            {
                string digits = name.Substring(idx + prefix.Length);
                if (int.TryParse(digits, out poleIndex)) return true;
            }
            return false;
        }

        private static void SetLayerRecursively(Transform root, int layer)
        {
            if (root == null || layer < 0 || layer > 31) return;
            root.gameObject.layer = layer;
            for (int i = 0; i < root.childCount; i++)
            {
                SetLayerRecursively(root.GetChild(i), layer);
            }
        }
    }
}

