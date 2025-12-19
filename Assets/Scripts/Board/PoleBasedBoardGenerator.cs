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
        [Header("Pole Layout Override")]
        [Tooltip("If true, use child transforms named with the pole prefix (e.g., pole_0) for precise placement.")]
        [SerializeField] private bool usePoleTransforms = true;
        [Tooltip("When using pole transforms, align piece rotation with the pole's orientation before applying the extra Euler offset.")]
        [SerializeField] private bool inheritPoleOrientation = true;
        [Tooltip("Prefix used when parsing pole child names. The suffix must be the pole index (0-63).")]
        [SerializeField] private string poleNamePrefix = "pole_";
        [Tooltip("If true, pole transformの回転・位置はポール中央を指すものとみなし、スタック全体を半分だけ下げます。")]
        [SerializeField] private bool polePivotIsCenter = true;
        [Tooltip("ポールのup方向に沿って適用する追加オフセット（メートル）。Pivotを基準に最下段を微調整できます。")]
        [SerializeField] private float poleBaseOffset = 0f;

        [Header("Piece Appearance")]
        [SerializeField] private float pieceScale = 0.8f;
        [SerializeField] private Color emptyColor = new Color(0.8f, 0.8f, 0.8f, 0.3f);
		[SerializeField] private Vector3 pieceRotationEuler = new Vector3(0f, 0f, 0f);
        [SerializeField] private bool addColliderIfMissing = true;
        
        [Header("Selection Collider")]
        [Tooltip("If true, will create/resize a SphereCollider for each piece when generated.")]
        [SerializeField] private bool configureCollider = true;
        [Tooltip("If true, use a fixed world radius for colliders; otherwise use pieceScale * colliderRadiusFactor (local).")]
		[SerializeField] private bool useFixedWorldRadius = true;
		[Tooltip("Sphere collider radius in world meters when using fixed mode.")]
		[SerializeField] private float colliderWorldRadius = 0.08f;
        [Tooltip("When not using fixed world radius, local collider radius = pieceScale * factor.")]
        [Range(0f, 1f)]
        [SerializeField] private float colliderRadiusFactor = 0.45f;
        [Tooltip("If true, center collider on the renderer visual center; otherwise use Vector3.zero.")]
        [SerializeField] private bool centerFromRenderer = true;
        [Tooltip("Additional local offset added to computed collider center.")]
        [SerializeField] private Vector3 colliderCenterOffsetLocal = Vector3.zero;

        [Header("Layering")]
        [Tooltip("If true, generated pieces inherit the layer from PiecesRoot (or this object when PiecesRoot is null).")]
        [SerializeField] private bool inheritLayerFromParent = true;

        public float PoleSpacing => poleSpacing;
        public float PieceSpacing => pieceSpacing;
        public float PieceScale => pieceScale;

        private GameObject[,] pieceObjects = new GameObject[PoleBasedBoardState.PoleCount, PoleBasedBoardState.PiecesPerPole];
        private Transform[] cachedPoleTransforms;
        private Transform cachedPoleRoot;
        private int cachedPoleCount = -1;
        private bool poleLayoutDirty = true;

        private void Awake()
        {
            if (boardState == null) boardState = GetComponent<PoleBasedBoardState>();
            if (poleParent == null) poleParent = GameObject.Find("Ground")?.transform;
            poleLayoutDirty = true;
        }

        private void OnValidate()
        {
            poleLayoutDirty = true;
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
            CachePoleTransforms();

            // Generate pieces for each pole
            for (int poleIndex = 0; poleIndex < PoleBasedBoardState.PoleCount; poleIndex++)
            {
                for (int slotIndex = 0; slotIndex < PoleBasedBoardState.PiecesPerPole; slotIndex++)
                {
				Transform poleTransform = GetPoleTransform(poleIndex);
				Vector3 worldPos;
				Quaternion worldRot = Quaternion.Euler(pieceRotationEuler);
				if (poleTransform != null)
				{
					Vector3 up = poleTransform.up.sqrMagnitude > 1e-5f ? poleTransform.up.normalized : Vector3.up;
					float baseOffset = poleBaseOffset;
					if (polePivotIsCenter)
					{
						baseOffset -= PoleBasedBoardState.PiecesPerPole * pieceSpacing * 0.5f;
					}
					float vertical = baseOffset + (slotIndex + 0.5f) * pieceSpacing;
					worldPos = poleTransform.position + up * vertical;
					if (inheritPoleOrientation)
					{
						worldRot = poleTransform.rotation * Quaternion.Euler(pieceRotationEuler);
					}
				}
				else
				{
					worldPos = PoleBasedBoardState.GetPieceWorldPosition(poleIndex, slotIndex, poleParent, poleSpacing, pieceSpacing);
				}
				
				Transform parentForPiece = piecesRoot != null ? piecesRoot : transform;
                    GameObject piece = Instantiate(piecePrefab, worldPos, worldRot, parentForPiece);
                    piece.name = $"Piece_P{poleIndex}_S{slotIndex}";
                    piece.transform.localScale = Vector3.one * pieceScale;
                    ApplyPieceLayer(piece);
                    if (addColliderIfMissing)
                    {
                        EnsureCollider(piece);
                    }
                    
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
                        DestroyPieceObject(pieceObjects[pole, slot]);
                        pieceObjects[pole, slot] = null;
                    }
                }
            }
            DestroyOrphanedPieceChildren();
        }

        private void DestroyOrphanedPieceChildren()
        {
            Transform parent = piecesRoot != null ? piecesRoot : transform;
            if (parent == null) return;
            for (int i = parent.childCount - 1; i >= 0; i--)
            {
                var child = parent.GetChild(i);
                if (child == null) continue;
                string name = child.name;
                if (!name.StartsWith("Piece_", StringComparison.OrdinalIgnoreCase)) continue;
                bool tracked = false;
                for (int pole = 0; pole < PoleBasedBoardState.PoleCount && !tracked; pole++)
                {
                    for (int slot = 0; slot < PoleBasedBoardState.PiecesPerPole; slot++)
                    {
                        if (pieceObjects[pole, slot] == child.gameObject)
                        {
                            tracked = true;
                            break;
                        }
                    }
                }
                if (!tracked)
                {
                    DestroyPieceObject(child.gameObject);
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

        private void ApplyPieceLayer(GameObject piece)
        {
            if (!inheritLayerFromParent || piece == null) return;
            int targetLayer = gameObject.layer;
            if (piecesRoot != null) targetLayer = piecesRoot.gameObject.layer;
            else if (poleParent != null) targetLayer = poleParent.gameObject.layer;
            if (targetLayer < 0 || targetLayer > 31) return;
            SetLayerRecursively(piece.transform, targetLayer);
        }

        private static void SetLayerRecursively(Transform root, int layer)
        {
            if (root == null) return;
            root.gameObject.layer = layer;
            for (int i = 0; i < root.childCount; i++)
            {
                SetLayerRecursively(root.GetChild(i), layer);
            }
        }

	private void UpdatePieceVisual(GameObject piece, PoleBasedBoardState.PieceColor color)
	{
		var renderers = piece.GetComponentsInChildren<Renderer>(true);
		if (renderers == null || renderers.Length == 0) return;

		foreach (var renderer in renderers)
		{
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
	}

    private void EnsureCollider(GameObject piece)
    {
        if (piece == null) return;
        // Prefer a single SphereCollider on the root
        var sphere = piece.GetComponent<SphereCollider>();
        if (sphere == null)
        {
            sphere = piece.AddComponent<SphereCollider>();
            sphere.isTrigger = false;
        }

        // Disable any other colliders under this piece to avoid oversized hits
        var allCols = piece.GetComponentsInChildren<Collider>(true);
        foreach (var c in allCols)
        {
            if (c != sphere) c.enabled = false;
        }

        FitSphereColliderToRenderer(piece, sphere);
    }

    private void FitSphereColliderToRenderer(GameObject piece, SphereCollider sphere)
    {
        // Optionally center collider using the renderer center
        Vector3 centerLocal = Vector3.zero;
        if (centerFromRenderer)
        {
            var r = piece.GetComponentInChildren<Renderer>();
            if (r != null)
            {
                var lb = r.localBounds;
                Vector3 centerWorld = r.transform.TransformPoint(lb.center);
                centerLocal = piece.transform.InverseTransformPoint(centerWorld);
            }
        }
        sphere.center = centerLocal + colliderCenterOffsetLocal;

        // Compute radius in local space
        float radiusLocal;
        if (useFixedWorldRadius)
        {
            Vector3 ls = piece.transform.lossyScale;
            float maxScale = Mathf.Max(Mathf.Abs(ls.x), Mathf.Max(Mathf.Abs(ls.y), Mathf.Abs(ls.z)));
            radiusLocal = colliderWorldRadius / (maxScale > 1e-5f ? maxScale : 1f);
        }
        else
        {
            radiusLocal = Mathf.Max(0.005f, pieceScale * colliderRadiusFactor);
        }
        sphere.radius = radiusLocal;
    }

    [ContextMenu("Fix Piece Colliders")]
    private void FixPieceCollidersContextMenu()
    {
        for (int pole = 0; pole < PoleBasedBoardState.PoleCount; pole++)
        {
            for (int slot = 0; slot < PoleBasedBoardState.PiecesPerPole; slot++)
            {
                var piece = pieceObjects[pole, slot];
                if (piece != null) EnsureCollider(piece);
            }
        }
        Debug.Log("PoleBasedBoardGenerator: Fixed piece colliders.");
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

        private void CachePoleTransforms()
        {
            if (!usePoleTransforms)
            {
                cachedPoleTransforms = null;
                cachedPoleRoot = null;
                cachedPoleCount = -1;
                return;
            }

            if (poleParent == null)
            {
                Debug.LogWarning("PoleBasedBoardGenerator: Pole parent missing, cannot cache pole transforms.", this);
                cachedPoleTransforms = null;
                cachedPoleRoot = null;
                cachedPoleCount = -1;
                return;
            }

            if (!poleLayoutDirty && cachedPoleRoot == poleParent && cachedPoleTransforms != null)
            {
                return;
            }

            poleLayoutDirty = false;
            cachedPoleRoot = poleParent;
            if (cachedPoleTransforms == null || cachedPoleTransforms.Length != PoleBasedBoardState.PoleCount)
            {
                cachedPoleTransforms = new Transform[PoleBasedBoardState.PoleCount];
            }
            Array.Clear(cachedPoleTransforms, 0, cachedPoleTransforms.Length);

            foreach (Transform child in poleParent.GetComponentsInChildren<Transform>(true))
            {
                if (TryParsePoleIndex(child.name, out int poleIndex) && (uint)poleIndex < cachedPoleTransforms.Length)
                {
                    cachedPoleTransforms[poleIndex] = child;
                }
            }

            int count = 0;
            for (int i = 0; i < cachedPoleTransforms.Length; i++)
            {
                if (cachedPoleTransforms[i] != null) count++;
            }

            if (count != cachedPoleCount)
            {
                cachedPoleCount = count;
                if (count == PoleBasedBoardState.PoleCount)
                {
                    Debug.Log("PoleBasedBoardGenerator: cached all pole transforms.", this);
                }
                else
                {
                    Debug.LogWarning($"PoleBasedBoardGenerator: cached {count} pole transforms (expected {PoleBasedBoardState.PoleCount}). Missing poles will fall back to uniform grid positions.", this);
                }
            }
        }

        private Transform GetPoleTransform(int poleIndex)
        {
            if (!usePoleTransforms || cachedPoleTransforms == null) return null;
            if ((uint)poleIndex >= cachedPoleTransforms.Length) return null;
            return cachedPoleTransforms[poleIndex];
        }

        private bool TryParsePoleIndex(string name, out int poleIndex)
        {
            poleIndex = -1;
            if (string.IsNullOrEmpty(name) || string.IsNullOrEmpty(poleNamePrefix)) return false;
            if (!name.StartsWith(poleNamePrefix, StringComparison.OrdinalIgnoreCase)) return false;
            var suffix = name.Substring(poleNamePrefix.Length);
            return int.TryParse(suffix, out poleIndex);
        }

        private void DestroyPieceObject(GameObject go)
        {
            if (go == null) return;
            if (Application.isPlaying)
                Destroy(go);
            else
                DestroyImmediate(go);
        }
    }
}
