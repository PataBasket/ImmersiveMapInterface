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
        [Tooltip("Optional: assign a pole layout (e.g., WorldInMiniature with child pole_# objects) to match spacing/rotation.")]
        [SerializeField] private Transform poleLayoutRoot;
        [SerializeField] private bool usePoleLayout = true;
        [SerializeField] private bool inheritPoleOrientation = true;
        [Tooltip("Additional offset applied to every slot along the local up direction (meters). Use negative values to sink stacks into the base.")]
        [SerializeField] private float stackVerticalOffset = 0f;

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

        private Renderer[,] pieceRenderers = new Renderer[PoleBasedBoardState.PoleCount, PoleBasedBoardState.PiecesPerPole];
        private GameObject[,] pieceObjects = new GameObject[PoleBasedBoardState.PoleCount, PoleBasedBoardState.PiecesPerPole];
        private static MaterialPropertyBlock propertyBlock;
        private Dictionary<int, Vector3> layoutLocalPositions;
        private Dictionary<int, Quaternion> layoutLocalRotations;

        private void Awake()
        {
            EnsureBoardState();
        }

        private void OnEnable()
        {
            EnsureGenerated();
            if (boardState != null)
            {
                boardState.OnPieceChanged += HandlePieceChanged;
                boardState.OnBoardReset += HandleBoardReset;
            }
        }

        private void OnDisable()
        {
            if (boardState != null)
            {
                boardState.OnPieceChanged -= HandlePieceChanged;
                boardState.OnBoardReset -= HandleBoardReset;
            }
        }

        public void EnsureGenerated()
        {
            CachePoleLayout();
            ClearPieces();
            for (int pole = 0; pole < PoleBasedBoardState.PoleCount; pole++)
            {
                PoleBasedBoardState.PoleIndexToGrid(pole, out int x, out int z);
                for (int slot = 0; slot < PoleBasedBoardState.PiecesPerPole; slot++)
                {
                    var go = Instantiate(piecePrefab, transform);
                    go.name = $"MiniPiece_P{pole}_S{slot}";
                    go.transform.localScale = Vector3.one * pieceScale;
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
                    float vertical = stackVerticalOffset + (slot + 0.5f) * pieceSpacing;
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
            if (pieceObjects == null) return;
            for (int pole = 0; pole < PoleBasedBoardState.PoleCount; pole++)
            {
                for (int slot = 0; slot < PoleBasedBoardState.PiecesPerPole; slot++)
                {
                    if (pieceObjects[pole, slot] == null) continue;
                    if (Application.isPlaying)
                        Destroy(pieceObjects[pole, slot]);
                    else
                        DestroyImmediate(pieceObjects[pole, slot]);
                    pieceObjects[pole, slot] = null;
                    pieceRenderers[pole, slot] = null;
                }
            }
        }

        private void EnsureBoardState()
        {
            if (boardState == null && autoFindBoardState)
            {
                boardState = FindObjectOfType<PoleBasedBoardState>();
            }
            if (boardState == null)
            {
                Debug.LogWarning("MiniaturePoleBoardGenerator: boardState not assigned. Pieces will not reflect actual board colors.");
            }
        }

        private void CachePoleLayout()
        {
            layoutLocalPositions = null;
            layoutLocalRotations = null;
            if (!usePoleLayout || poleLayoutRoot == null) return;

            layoutLocalPositions = new Dictionary<int, Vector3>(PoleBasedBoardState.PoleCount);
            if (inheritPoleOrientation)
            {
                layoutLocalRotations = new Dictionary<int, Quaternion>(PoleBasedBoardState.PoleCount);
            }

            foreach (Transform child in poleLayoutRoot.GetComponentsInChildren<Transform>(true))
            {
                if (!TryParsePoleIndex(child.name, out int poleIndex)) continue;
                Vector3 localPos = transform.InverseTransformPoint(child.position);
                layoutLocalPositions[poleIndex] = localPos;
                if (inheritPoleOrientation)
                {
                    Quaternion localRot = Quaternion.Inverse(transform.rotation) * child.rotation;
                    layoutLocalRotations[poleIndex] = localRot;
                }
            }
        }

        private void HandlePieceChanged(int pole, int slot, PoleBasedBoardState.PieceColor color)
        {
            UpdateRenderer(pole, slot, color);
        }

        private void HandleBoardReset()
        {
            for (int pole = 0; pole < PoleBasedBoardState.PoleCount; pole++)
            {
                for (int slot = 0; slot < PoleBasedBoardState.PiecesPerPole; slot++)
                {
                    UpdateRenderer(pole, slot, boardState != null ? boardState.GetPiece(pole, slot) : PoleBasedBoardState.PieceColor.Empty);
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
    }
}
