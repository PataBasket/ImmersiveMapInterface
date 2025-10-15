using System;
using UnityEngine;

namespace ImmersiveMapInterface.Board
{
	/// <summary>
	/// Instantiates an 8×8×8 grid of piece instances under a root transform.
	/// Links visuals to the BoardState and updates material/color per cell value.
	/// </summary>
	public class BoardGenerator : MonoBehaviour
	{
		[Header("References")]
		[SerializeField] private BoardState boardState;
		[SerializeField] private GameObject piecePrefab;

		[Header("Layout")]
		[SerializeField] private float spacing = 0.2f;
		[SerializeField] private bool centerRootAtOrigin = true;

		[Header("Appearance")]
		[SerializeField] private Color colorA = new Color(0.9f, 0.2f, 0.2f);
		[SerializeField] private Color colorB = new Color(0.2f, 0.6f, 1.0f);
		[SerializeField] private Color colorEmpty = new Color(0.8f, 0.8f, 0.8f, 0.3f);

		private Renderer[] pieceRenderers = Array.Empty<Renderer>();

		private void Awake()
		{
			if (boardState == null) boardState = GetComponent<BoardState>();
		}

		private void OnEnable()
		{
			EnsureGenerated();
			if (boardState != null)
			{
				boardState.OnCellChanged += HandleCellChanged;
				boardState.OnBoardReset += HandleBoardReset;
			}
		}

		private void OnDisable()
		{
			if (boardState != null)
			{
				boardState.OnCellChanged -= HandleCellChanged;
				boardState.OnBoardReset -= HandleBoardReset;
			}
		}

		public void EnsureGenerated()
		{
			int needed = BoardState.CellCount;
			if (pieceRenderers.Length == needed && transform.childCount == needed) return;

			// Clear old children
			for (int i = transform.childCount - 1; i >= 0; i--)
			{
				DestroyImmediate(transform.GetChild(i).gameObject);
			}

			pieceRenderers = new Renderer[needed];
			for (int i = 0; i < needed; i++)
			{
				Vector3 localPos = BoardState.IndexToLocalPosition(i, spacing);
				var go = Instantiate(piecePrefab, transform);
				go.transform.localPosition = localPos;
				go.name = $"piece_{i}";
				pieceRenderers[i] = go.GetComponentInChildren<Renderer>();
			}

			if (centerRootAtOrigin)
			{
				float extent = (BoardState.Size - 1) * spacing * 0.5f;
				transform.localPosition = new Vector3(-extent, -extent, -extent);
			}

			// Initialize visuals
			HandleBoardReset();
		}

		private void HandleCellChanged(int index, BoardState.Cell value)
		{
			if ((uint)index >= pieceRenderers.Length) return;
			UpdateRendererColor(pieceRenderers[index], value);
		}

		private void HandleBoardReset()
		{
			for (int i = 0; i < pieceRenderers.Length; i++)
			{
				UpdateRendererColor(pieceRenderers[i], boardState != null ? boardState.GetCellFromIndex(i) : BoardState.Cell.Empty);
			}
		}

	}
}

