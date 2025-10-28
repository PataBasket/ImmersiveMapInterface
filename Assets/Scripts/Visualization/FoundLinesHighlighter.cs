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
    }
}
