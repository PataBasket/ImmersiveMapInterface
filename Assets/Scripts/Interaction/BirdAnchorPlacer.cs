using System.Collections.Generic;
using UnityEngine;

namespace ImmersiveMapInterface.Interaction
{
	/// <summary>
	/// Places bird-view anchor transforms around the board center (Ground).
	/// Creates a ring of anchors at given radii and heights.
	/// </summary>
	public class BirdAnchorPlacer : MonoBehaviour
	{
		[SerializeField] private Transform ground; // Ground center
		[SerializeField] private float boardHalfSize = 8f; // meters from center to edge
		[SerializeField] private float extraMargin = 1.5f; // extra distance beyond board edge
		[SerializeField] private float[] anchorHeights = new float[] { 1.6f, 3.0f };
		[SerializeField] private int pointsPerRing = 8;

		[SerializeField] private List<Transform> anchors = new List<Transform>();

		private void Reset()
		{
			if (ground == null)
			{
				var g = GameObject.Find("Ground");
				if (g != null) ground = g.transform;
			}
		}

		public void ClearAnchors()
		{
			for (int i = transform.childCount - 1; i >= 0; i--)
			{
				DestroyImmediate(transform.GetChild(i).gameObject);
			}
			anchors.Clear();
		}

		public void GenerateAnchors()
		{
			if (ground == null)
			{
				Debug.LogError("BirdAnchorPlacer: ground not assigned");
				return;
			}

			ClearAnchors();

			float radius = boardHalfSize + extraMargin;
			for (int h = 0; h < anchorHeights.Length; h++)
			{
				float height = anchorHeights[h];
				for (int i = 0; i < pointsPerRing; i++)
				{
					float angle = (360f / pointsPerRing) * i;
					Vector3 pos = ground.position + Quaternion.Euler(0f, angle, 0f) * new Vector3(0f, 0f, radius);
					pos.y = height;
					var go = new GameObject($"Anchor_h{h}_p{i}");
					go.transform.SetPositionAndRotation(pos, Quaternion.LookRotation((ground.position + new Vector3(0f, height, 0f)) - pos, Vector3.up));
					go.transform.SetParent(transform, true);
					anchors.Add(go.transform);
				}
			}
		}

		public IReadOnlyList<Transform> GetAnchors() => anchors;
	}
}

