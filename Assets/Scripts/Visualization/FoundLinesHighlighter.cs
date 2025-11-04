using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;

namespace ImmersiveMapInterface.Visualization
{
    public class FoundLinesHighlighter : MonoBehaviour
    {
        [Header("Materials")]
        public Material redMaterial;
        public bool includeMiniature = true;

        private readonly Dictionary<(int pole,int slot), Renderer> map = new();
        private static readonly Regex nameRegex = new Regex(@"(MiniPiece|Piece)_P(\d+)_S(\d+)", RegexOptions.Compiled);

        // Preview highlight via MaterialPropertyBlock (non-destructive)
        private readonly List<Renderer> previewRenderers = new List<Renderer>();
        private Renderer hoverRenderer;

        private void Awake()
        {
            BuildMap();
        }

        [ContextMenu("Rebuild Map")]
        public void BuildMap()
        {
            map.Clear();
            var renderers = FindObjectsOfType<Renderer>(includeInactive: true);
            foreach (var r in renderers)
            {
                var m = nameRegex.Match(r.gameObject.name);
                if (!m.Success) continue;
                bool isMini = m.Groups[1].Value == "MiniPiece";
                if (!includeMiniature && isMini) continue;
                if (int.TryParse(m.Groups[2].Value, out int pole) && int.TryParse(m.Groups[3].Value, out int slot))
                {
                    map[(pole, slot)] = r;
                }
            }
        }

        public void HighlightCells(IEnumerable<(int pole,int slot)> cells)
        {
            if (redMaterial == null) { Debug.LogWarning("FoundLinesHighlighter: redMaterial not set"); return; }
            foreach (var c in cells)
            {
                if (!map.TryGetValue(c, out var r) || r == null)
                {
                    // Pieces may have been regenerated after Awake. Rebuild map once and retry.
                    BuildMap();
                    map.TryGetValue(c, out r);
                }
                if (r != null) r.sharedMaterial = redMaterial;
            }
        }

        public void SetPreviewCell((int pole,int slot) cell, Color color)
        {
            if (!map.TryGetValue(cell, out var r) || r == null)
            {
                BuildMap();
                map.TryGetValue(cell, out r);
            }
            if (r == null) return;
            var block = new MaterialPropertyBlock();
            r.GetPropertyBlock(block);
            block.SetColor("_BaseColor", color);
            block.SetColor("_Color", color);
            r.SetPropertyBlock(block);
            if (!previewRenderers.Contains(r)) previewRenderers.Add(r);
        }

        public void ClearPreview()
        {
            foreach (var r in previewRenderers)
            {
                if (r != null)
                {
                    r.SetPropertyBlock(null);
                }
            }
            previewRenderers.Clear();
        }

        // Hover highlight (single cell). Uses a separate slot from preview.
        public void SetHoverCell((int pole,int slot) cell, Color color)
        {
            if (!map.TryGetValue(cell, out var r) || r == null)
            {
                BuildMap();
                map.TryGetValue(cell, out r);
            }
            if (hoverRenderer == r) return;
            // clear previous hover if it wasn't also a preview target
            if (hoverRenderer != null && !previewRenderers.Contains(hoverRenderer))
            {
                hoverRenderer.SetPropertyBlock(null);
            }
            hoverRenderer = r;
            if (hoverRenderer == null) return;
            var block = new MaterialPropertyBlock();
            hoverRenderer.GetPropertyBlock(block);
            block.SetColor("_BaseColor", color);
            block.SetColor("_Color", color);
            hoverRenderer.SetPropertyBlock(block);
        }

        public void ClearHover()
        {
            if (hoverRenderer != null)
            {
                // Do not clear if the hover renderer is also in the preview set
                if (!previewRenderers.Contains(hoverRenderer))
                {
                    hoverRenderer.SetPropertyBlock(null);
                }
                hoverRenderer = null;
            }
        }
    }
}
