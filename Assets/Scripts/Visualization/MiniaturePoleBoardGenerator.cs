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
        [Tooltip("If assigned, pieces spawn aligned to this poles parent (mirrors Ground layout). If null, uses local miniature grid.")]
        [SerializeField] private Transform poleParent;

        [Header("Appearance")]
        [SerializeField] private float poleSpacing = 0.2f; // miniature grid spacing on XZ (used if poleParent is null)
        [SerializeField] private float pieceSpacing = 0.15f; // miniature vertical spacing (Y)
        [SerializeField] private float pieceScale = 0.12f;
        [SerializeField] private Vector3 pieceRotationEuler = new Vector3(0f,0f,0f);
        [SerializeField] private Material whiteMaterial;
        [SerializeField] private Material blackMaterial;
        [SerializeField] private Material emptyMaterial;

        private Renderer[,] pieceRenderers = new Renderer[PoleBasedBoardState.PoleCount, PoleBasedBoardState.PiecesPerPole];
        private GameObject[,] pieceObjects = new GameObject[PoleBasedBoardState.PoleCount, PoleBasedBoardState.PiecesPerPole];
        private Dictionary<int, Transform> poleTransforms;

        private void Awake()
        {
            if (boardState == null) boardState = GetComponentInParent<PoleBasedBoardState>();
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
            CachePoleTransforms();

            ClearPieces();
            for (int pole = 0; pole < PoleBasedBoardState.PoleCount; pole++)
            {
                PoleBasedBoardState.PoleIndexToGrid(pole, out int x, out int z);
                for (int slot = 0; slot < PoleBasedBoardState.PiecesPerPole; slot++)
                {
                    Quaternion rot;
                    Vector3 worldPos = GetWorldPositionAndRotation(pole, x, z, slot, out rot);
                    var go = Instantiate(piecePrefab, worldPos, rot * Quaternion.Euler(pieceRotationEuler), transform);
                    go.name = $"MiniPiece_P{pole}_S{slot}";
                    go.transform.localScale = Vector3.one * pieceScale;
                    pieceObjects[pole, slot] = go;
                    pieceRenderers[pole, slot] = go.GetComponentInChildren<Renderer>();
                }
            }
            HandleBoardReset();
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

        private void CachePoleTransforms()
        {
            poleTransforms = null;
            if (poleParent == null) return;

            poleTransforms = new Dictionary<int, Transform>(PoleBasedBoardState.PoleCount);
            foreach (Transform child in poleParent.GetComponentsInChildren<Transform>(true))
            {
                if (!TryParsePoleIndex(child.name, out int poleIndex)) continue;
                poleTransforms[poleIndex] = child;
            }
        }

        private Vector3 GetWorldPositionAndRotation(int poleIndex, int x, int z, int slot, out Quaternion rotation)
        {
            if (poleTransforms != null && poleTransforms.TryGetValue(poleIndex, out var t))
            {
                rotation = Quaternion.LookRotation(t.forward, t.up);
                return t.position + t.up * ((slot + 0.5f) * pieceSpacing);
            }
            rotation = transform.rotation;
            Vector3 local = LocalPoleCenter(x, z);
            Vector3 world = transform.TransformPoint(local);
            return world + transform.up * ((slot + 0.5f) * pieceSpacing);
        }

        private static bool TryParsePoleIndex(string name, out int poleIndex)
        {
            poleIndex = -1;
            const string prefix = "pole_";
            int idx = name.IndexOf(prefix);
            if (idx >= 0)
            {
                string digits = name.Substring(idx + prefix.Length);
                if (int.TryParse(digits, out poleIndex)) return true;
            }
            return false;
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
            switch (color)
            {
                case PoleBasedBoardState.PieceColor.White:
                    if (whiteMaterial != null) r.sharedMaterial = whiteMaterial; break;
                case PoleBasedBoardState.PieceColor.Black:
                    if (blackMaterial != null) r.sharedMaterial = blackMaterial; break;
                default:
                    if (emptyMaterial != null) r.sharedMaterial = emptyMaterial; break;
            }
        }
    }
}
