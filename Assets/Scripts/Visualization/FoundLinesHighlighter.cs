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

        private readonly Dictionary<(int pole,int slot), List<Renderer>> map = new();
        private static readonly Regex nameRegex = new Regex(@"(MiniPiece|Piece)_P(\d+)_S(\d+)", RegexOptions.Compiled);

        // Preview highlight via MaterialPropertyBlock (non-destructive)
        private readonly List<Renderer> previewRenderers = new List<Renderer>();
        private readonly List<Renderer> hoverRenderers = new List<Renderer>();

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
                    var key = (pole, slot);
                    if (!map.TryGetValue(key, out var list) || list == null)
                    {
                        list = new List<Renderer>();
                        map[key] = list;
                    }
                    if (!list.Contains(r))
                    {
                        list.Add(r);
                    }
                }
            }
        }

        public void HighlightCells(IEnumerable<(int pole,int slot)> cells)
        {
            if (redMaterial == null) { Debug.LogWarning("FoundLinesHighlighter: redMaterial not set"); return; }
            foreach (var c in cells)
            {
                var renderers = GetRenderers(c);
                if (renderers == null) continue;
                foreach (var r in renderers)
                {
                    if (r != null) r.sharedMaterial = redMaterial;
                }
            }
        }

        public void SetPreviewCell((int pole,int slot) cell, Color color)
        {
            var renderers = GetRenderers(cell);
            if (renderers == null) return;
            foreach (var r in renderers)
            {
                if (r == null) continue;
                var block = new MaterialPropertyBlock();
                r.GetPropertyBlock(block);
                block.SetColor("_BaseColor", color);
                block.SetColor("_Color", color);
                r.SetPropertyBlock(block);
                if (!previewRenderers.Contains(r)) previewRenderers.Add(r);
            }
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
            var renderers = GetRenderers(cell);
            if (renderers == null || renderers.Count == 0)
            {
                ClearHover();
                return;
            }

            ClearHoverInternal();

            foreach (var r in renderers)
            {
                if (r == null) continue;
                var block = new MaterialPropertyBlock();
                r.GetPropertyBlock(block);
                block.SetColor("_BaseColor", color);
                block.SetColor("_Color", color);
                r.SetPropertyBlock(block);
                hoverRenderers.Add(r);
            }
        }

        public void ClearHover()
        {
            ClearHoverInternal();
        }

        private void ClearHoverInternal()
        {
            if (hoverRenderers.Count == 0) return;
            foreach (var r in hoverRenderers)
            {
                if (r == null) continue;
                if (!previewRenderers.Contains(r))
                {
                    r.SetPropertyBlock(null);
                }
            }
            hoverRenderers.Clear();
        }

        private List<Renderer> GetRenderers((int pole,int slot) cell)
        {
            if (!map.TryGetValue(cell, out var list) || list == null || list.Count == 0)
            {
                BuildMap();
                map.TryGetValue(cell, out list);
            }
            return list;
        }
    }
}
