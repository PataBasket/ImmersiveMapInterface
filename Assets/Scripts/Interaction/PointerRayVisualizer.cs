using UnityEngine;
using ImmersiveMapInterface.Experiment.Selection;

namespace ImmersiveMapInterface.Interaction
{
    /// <summary>
    /// Draws a simple laser pointer (LineRenderer) from the pointer origin so players can see what the selection ray is targeting.
    /// </summary>
    [RequireComponent(typeof(LineRenderer))]
    public class PointerRayVisualizer : MonoBehaviour
    {
        [Header("Sources")]
        public SelectionSystem selectionSystem;

        [Header("Appearance")]
        public Color rayColor = new Color(0.35f, 0.8f, 1f, 0.95f);
        public float startWidth = 0.004f;
        public float endWidth = 0.0008f;
        public float minLength = 0.15f;
        [Tooltip("Lerp speed (units/sec) when the target length changes.")]
        public float followSpeed = 25f;
        [Tooltip("Clamp the visual to the first collider hit so it matches interaction distance.")]
        public bool clampToHit = true;

        private LineRenderer line;
        private float currentLength;

        private static readonly int EmissionColor = Shader.PropertyToID("_EmissionColor");

        private void Awake()
        {
            EnsureLineRenderer();
            if (selectionSystem == null)
            {
                selectionSystem = FindObjectOfType<SelectionSystem>();
            }
            UpdateVisual(true);
        }

        private void OnEnable()
        {
            EnsureLineRenderer();
            line.enabled = true;
            currentLength = 0f;
            UpdateVisual(true);
        }

        private void LateUpdate()
        {
            UpdateVisual(false);
        }

        private void EnsureLineRenderer()
        {
            if (line == null)
            {
                line = GetComponent<LineRenderer>();
                line.useWorldSpace = false;
                line.positionCount = 2;
                line.textureMode = LineTextureMode.Stretch;
                var shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Sprites/Default");
                if (line.sharedMaterial == null || (line.sharedMaterial.shader != shader && shader != null))
                {
                    var mat = new Material(shader ?? Shader.Find("Sprites/Default"));
                    line.sharedMaterial = mat;
                }
            }

            line.startWidth = startWidth;
            line.endWidth = endWidth;
            var transparent = new Color(rayColor.r, rayColor.g, rayColor.b, 0f);
            line.startColor = rayColor;
            line.endColor = transparent;
            if (line.sharedMaterial != null)
            {
                line.sharedMaterial.color = rayColor;
                if (line.sharedMaterial.HasProperty(EmissionColor))
                {
                    line.sharedMaterial.SetColor(EmissionColor, rayColor);
                }
            }
        }

        private void UpdateVisual(bool instant)
        {
            if (line == null) return;

            float targetLength = selectionSystem != null ? selectionSystem.CurrentPointerLength : 5f;
            LayerMask mask = selectionSystem != null ? selectionSystem.PointerLayerMask : Physics.DefaultRaycastLayers;

            if (clampToHit)
            {
                var origin = transform.position;
                var dir = transform.forward;
                if (Physics.Raycast(origin, dir, out var hit, targetLength, mask, QueryTriggerInteraction.Ignore))
                {
                    targetLength = hit.distance;
                }
            }

            targetLength = Mathf.Max(minLength, targetLength);
            if (instant || followSpeed <= 0f)
            {
                currentLength = targetLength;
            }
            else
            {
                float lerp = 1f - Mathf.Exp(-followSpeed * Time.deltaTime);
                currentLength = Mathf.Lerp(currentLength, targetLength, lerp);
            }

            line.SetPosition(0, Vector3.zero);
            line.SetPosition(1, Vector3.forward * currentLength);
        }

#if UNITY_EDITOR || UNITY_STANDALONE || UNITY_ANDROID
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoAttach()
        {
            TryAttach("RightPointer", new Color(0.35f, 0.8f, 1f, 0.95f));
            TryAttach("LeftPointer", new Color(1f, 0.45f, 0.75f, 0.95f));
        }

        private static void TryAttach(string pointerName, Color color)
        {
            var pointer = GameObject.Find(pointerName);
            if (pointer == null) return;

            var viz = pointer.GetComponent<PointerRayVisualizer>();
            if (viz == null)
            {
                viz = pointer.AddComponent<PointerRayVisualizer>();
            }

            if (viz.selectionSystem == null)
            {
                viz.selectionSystem = Object.FindObjectOfType<SelectionSystem>();
            }

            viz.rayColor = color;
            viz.EnsureLineRenderer();
            viz.UpdateVisual(true);
        }
#endif
    }
}
