using System;
using UnityEngine;

namespace ImmersiveMapInterface.Board
{
    /// <summary>
    /// Generates and manages pieces on poles using Sphere.fbx and white/black materials.
    /// </summary>
    public class PoleBasedBoardGenerator : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private PoleBasedBoardState boardState;
        [SerializeField] private GameObject piecePrefab; // Sphere.fbx prefab
        [SerializeField] private Material whiteMaterial;
        [SerializeField] private Material blackMaterial;
        [SerializeField] private Material emptyMaterial;

        [Header("Layout")]
        [SerializeField] private float poleSpacing = 2.0f;
        [SerializeField] private float pieceSpacing = 1.0f;
		[SerializeField] private Transform poleParent; // Ground object with pole children
		[SerializeField] private Transform piecesRoot; // Optional parent for pieces (scale 1,1,1)

        [Header("Piece Appearance")]
        [SerializeField] private float pieceScale = 0.8f;
        [SerializeField] private Color emptyColor = new Color(0.8f, 0.8f, 0.8f, 0.3f);
		[SerializeField] private Vector3 pieceRotationEuler = new Vector3(0f, 0f, 0f);

        private GameObject[,] pieceObjects = new GameObject[PoleBasedBoardState.PoleCount, PoleBasedBoardState.PiecesPerPole];

        private void Awake()
        {
            if (boardState == null) boardState = GetComponent<PoleBasedBoardState>();
            if (poleParent == null) poleParent = GameObject.Find("Ground")?.transform;
        }

        private void OnEnable()
        {
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

        public void GeneratePieces()
        {
            if (poleParent == null)
            {
                Debug.LogError("PoleParent not assigned! Assign Ground object.");
                return;
            }

            if (piecePrefab == null)
            {
                Debug.LogError("PiecePrefab not assigned! Assign Sphere.fbx prefab.");
                return;
            }

            // Clear existing pieces
            ClearPieces();

            // Generate pieces for each pole
            for (int poleIndex = 0; poleIndex < PoleBasedBoardState.PoleCount; poleIndex++)
            {
                for (int slotIndex = 0; slotIndex < PoleBasedBoardState.PiecesPerPole; slotIndex++)
                {
				Vector3 worldPos = PoleBasedBoardState.GetPieceWorldPosition(poleIndex, slotIndex, poleParent, poleSpacing, pieceSpacing);
				Quaternion worldRot = Quaternion.Euler(pieceRotationEuler);
				
				Transform parentForPiece = piecesRoot != null ? piecesRoot : transform;
				GameObject piece = Instantiate(piecePrefab, worldPos, worldRot, parentForPiece);
                    piece.name = $"Piece_P{poleIndex}_S{slotIndex}";
                    piece.transform.localScale = Vector3.one * pieceScale;
                    
                    pieceObjects[poleIndex, slotIndex] = piece;
                }
            }

            // Initialize visual state
            HandleBoardReset();
        }

		public void DeletePieces()
		{
			ClearPieces();
		}

        private void ClearPieces()
        {
            for (int pole = 0; pole < PoleBasedBoardState.PoleCount; pole++)
            {
                for (int slot = 0; slot < PoleBasedBoardState.PiecesPerPole; slot++)
                {
                    if (pieceObjects[pole, slot] != null)
                    {
                        DestroyImmediate(pieceObjects[pole, slot]);
                        pieceObjects[pole, slot] = null;
                    }
                }
            }
        }

        private void HandlePieceChanged(int poleIndex, int slotIndex, PoleBasedBoardState.PieceColor color)
        {
            if (!PoleBasedBoardState.IsValidPoleSlot(poleIndex, slotIndex)) return;
            
            GameObject piece = pieceObjects[poleIndex, slotIndex];
            if (piece == null) return;

            UpdatePieceVisual(piece, color);
        }

        private void HandleBoardReset()
        {
            for (int pole = 0; pole < PoleBasedBoardState.PoleCount; pole++)
            {
                for (int slot = 0; slot < PoleBasedBoardState.PiecesPerPole; slot++)
                {
                    GameObject piece = pieceObjects[pole, slot];
                    if (piece == null) continue;

                    PoleBasedBoardState.PieceColor color = boardState != null ? 
                        boardState.GetPiece(pole, slot) : PoleBasedBoardState.PieceColor.Empty;
                    
                    UpdatePieceVisual(piece, color);
                }
            }
        }

		private void UpdatePieceVisual(GameObject piece, PoleBasedBoardState.PieceColor color)
		{
			var renderer = piece.GetComponent<Renderer>();
			if (renderer == null) return;

			// Prefer sharedMaterial in Edit mode to avoid instantiation warnings/leaks.
			switch (color)
			{
				case PoleBasedBoardState.PieceColor.White:
					if (whiteMaterial != null) renderer.sharedMaterial = whiteMaterial;
					break;
				case PoleBasedBoardState.PieceColor.Black:
					if (blackMaterial != null) renderer.sharedMaterial = blackMaterial;
					break;
				case PoleBasedBoardState.PieceColor.Empty:
				default:
					if (emptyMaterial != null)
					{
						renderer.sharedMaterial = emptyMaterial;
					}
					else
					{
						// Fall back to a MaterialPropertyBlock to tint without creating an instance
						var block = new MaterialPropertyBlock();
						renderer.GetPropertyBlock(block);
						block.SetColor("_BaseColor", emptyColor);
						block.SetColor("_Color", emptyColor);
						renderer.SetPropertyBlock(block);
					}
					break;
			}
		}

        [ContextMenu("Generate Pieces")]
        private void GeneratePiecesContextMenu()
        {
            GeneratePieces();
        }

		[ContextMenu("Delete Pieces")]
		private void DeletePiecesContextMenu()
		{
			DeletePieces();
		}
    }
}
