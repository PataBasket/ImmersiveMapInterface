using System;
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

        [Header("Appearance")]
        [SerializeField] private float poleSpacing = 0.2f; // miniature grid spacing on XZ
        [SerializeField] private float pieceSpacing = 0.15f; // miniature vertical spacing (Y)
        [SerializeField] private float pieceScale = 0.08f;
        [SerializeField] private Material whiteMaterial;
        [SerializeField] private Material blackMaterial;
        [SerializeField] private Material emptyMaterial;

        private Renderer[,] pieceRenderers = new Renderer[PoleBasedBoardState.PoleCount, PoleBasedBoardState.PiecesPerPole];

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
            ClearChildren();
            for (int pole = 0; pole < PoleBasedBoardState.PoleCount; pole++)
            {
                PoleBasedBoardState.PoleIndexToGrid(pole, out int x, out int z);
                Vector3 basePos = LocalPoleCenter(x, z);
                for (int slot = 0; slot < PoleBasedBoardState.PiecesPerPole; slot++)
                {
                    var go = Instantiate(piecePrefab, transform);
                    go.name = $"MiniPiece_P{pole}_S{slot}";
                    go.transform.localScale = Vector3.one * pieceScale;
                    Vector3 pos = basePos + new Vector3(0f, (slot + 0.5f) * pieceSpacing, 0f);
                    go.transform.localPosition = pos;
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

        private void ClearChildren()
        {
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                DestroyImmediate(transform.GetChild(i).gameObject);
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

