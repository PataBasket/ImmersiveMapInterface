using UnityEngine;

namespace ImmersiveMapInterface.Interaction
{
    // Runtime visual manager: show controllers, hands, or両方(Both)。
    // 多くのMeta XRプレハブはモードに応じて自動で非表示にしますが、
    // このスクリプトは毎フレーム可視状態を強制することで一貫表示を確保します。
    public class ControllerVisualToggle : MonoBehaviour
    {
        public enum VisualMode { FollowSystem, ControllersOnly, HandsOnly, Both }

        [Header("Targets (assign if available)")]
        public GameObject leftControllerVisual;
        public GameObject rightControllerVisual;
        public GameObject leftHandVisual;
        public GameObject rightHandVisual;

        [Header("Anchor Name Candidates")]
        public string[] leftAnchorNames = { "LeftPointer", "LeftControllerAnchor", "LeftHandAnchor", "LeftHandAnchorDetached" };
        public string[] rightAnchorNames = { "RightPointer", "RightControllerAnchor", "RightHandAnchor", "RightHandAnchorDetached" };

        [Header("Also Toggle (optional)")]
        public GameObject[] extraControllerVisuals;
        public GameObject[] extraHandVisuals;

        [Header("Mode")]
        public VisualMode mode = VisualMode.Both;

        [Header("Auto-Find By Name (optional)")]
        public bool autoFind = true;

        [Header("Proxy (when controller models missing)")]
        public bool spawnProxyIfMissing = true;
        [Tooltip("If true, prefer lightweight proxy meshes whenever no visible renderer is found under the assigned controller objects.")]
        public bool preferProxyControllers = false;
        public Vector3 proxyLocalPosition = new Vector3(0f, -0.03f, 0.06f);
        public Vector3 proxyLocalScale = new Vector3(0.03f, 0.02f, 0.12f);

        [Header("Force Visibility (override SDK toggles)")]
        [Tooltip("Call OVRControllerVisual.ForceOnVisibility each frame (if component exists) and disable ActiveState wrappers.")]
        public bool forceControllerVisibility = true;
        public bool disableActiveStateWrappers = true;

        private void Awake()
        {
            if (autoFind)
            {
                leftControllerVisual = leftControllerVisual ?? GameObject.Find("OVRLeftControllerVisual");
                rightControllerVisual = rightControllerVisual ?? GameObject.Find("OVRRightControllerVisual");
                leftHandVisual = leftHandVisual ?? GameObject.Find("OVRLeftHandVisual");
                rightHandVisual = rightHandVisual ?? GameObject.Find("OVRRightHandVisual");
            }

            if (spawnProxyIfMissing)
            {
                // If controller visuals are missing, spawn simple proxies under hand anchors
                if (leftControllerVisual == null || preferProxyControllers || !HasRenderable(leftControllerVisual))
                    leftControllerVisual = CreateControllerProxy(FindAnchor(leftAnchorNames), "ControllerProxy_L");
                if (rightControllerVisual == null || preferProxyControllers || !HasRenderable(rightControllerVisual))
                    rightControllerVisual = CreateControllerProxy(FindAnchor(rightAnchorNames), "ControllerProxy_R");
            }
        }

        private void OnEnable()
        {
            Apply();
        }

        private void LateUpdate()
        {
            // 強制適用：他のスクリプトが可視状態を変えても上書きする
            Apply();
        }

        public void Apply()
        {
            switch (mode)
            {
                case VisualMode.ControllersOnly:
                    SetControllers(true); SetHands(false); break;
                case VisualMode.HandsOnly:
                    SetControllers(false); SetHands(true); break;
                case VisualMode.Both:
                    SetControllers(true); SetHands(true); break;
                case VisualMode.FollowSystem:
                default:
                    // 何もしない（システムに任せる）
                    break;
            }
        }

        private void SetControllers(bool active)
        {
            SetActiveSafe(leftControllerVisual, active);
            SetActiveSafe(rightControllerVisual, active);
            if (extraControllerVisuals != null)
            {
                foreach (var go in extraControllerVisuals) SetActiveSafe(go, active);
            }

            if (forceControllerVisibility && active)
            {
                ForceOnControllerVisual(leftControllerVisual);
                ForceOnControllerVisual(rightControllerVisual);
            }
        }

        private void SetHands(bool active)
        {
            SetActiveSafe(leftHandVisual, active);
            SetActiveSafe(rightHandVisual, active);
            if (extraHandVisuals != null)
            {
                foreach (var go in extraHandVisuals) SetActiveSafe(go, active);
            }
        }

        private static void SetActiveSafe(GameObject go, bool active)
        {
            if (go != null)
            {
                if (go.activeSelf != active) go.SetActive(active);
            }
        }

        private void ForceOnControllerVisual(GameObject root)
        {
            if (root == null) return;
            // Disable Active State wrappers that auto-hide based on hand presence
            if (disableActiveStateWrappers)
            {
                var wrappers = root.GetComponentsInChildren<MonoBehaviour>(true);
                foreach (var w in wrappers)
                {
                    var typeName = w.GetType().Name;
                    if (typeName.Contains("ActiveState") || typeName.Contains("Visibility") || typeName.Contains("Wrapper"))
                    {
                        // disable only wrappers; avoid disabling core renderers
                        if (w.enabled) w.enabled = false;
                    }
                }
            }

            // Call OVRControllerVisual.ForceOnVisibility if present
            var comps = root.GetComponentsInChildren<Component>(true);
            foreach (var c in comps)
            {
                var t = c.GetType();
                if (t.Name == "OVRControllerVisual" || t.Name.EndsWith("ControllerVisual"))
                {
                    var m = t.GetMethod("ForceOnVisibility");
                    if (m != null)
                    {
                        m.Invoke(c, null);
                    }
                }
            }
        }

        private Transform FindAnchor(string name)
        {
            return FindAnchor(new[] { name });
        }

        private Transform FindAnchor(string[] names)
        {
            var t = transform;
            var stack = new System.Collections.Generic.Stack<Transform>();
            stack.Push(t);
            while (stack.Count > 0)
            {
                var cur = stack.Pop();
                foreach (var name in names)
                {
                    if (cur.name == name) return cur;
                }
                for (int i = 0; i < cur.childCount; i++) stack.Push(cur.GetChild(i));
            }
            foreach (var name in names)
            {
                var go = GameObject.Find(name);
                if (go != null) return go.transform;
            }
            return null;
        }

        private GameObject CreateControllerProxy(Transform anchor, string name)
        {
            if (anchor == null) return null;
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(anchor, false);
            go.transform.localPosition = proxyLocalPosition;
            go.transform.localRotation = Quaternion.identity;
            go.transform.localScale = proxyLocalScale;
            // make it unlit gray
            var mr = go.GetComponent<MeshRenderer>();
            if (mr != null)
            {
                mr.sharedMaterial = new Material(Shader.Find("Universal Render Pipeline/Lit"))
                {
                    color = new Color(0.8f, 0.8f, 0.8f, 1f)
                };
            }
            var col = go.GetComponent<Collider>();
            if (col) col.enabled = false; // not interactive
            return go;
        }

        private static bool HasRenderable(GameObject root)
        {
            if (root == null) return false;
            var renderers = root.GetComponentsInChildren<Renderer>(true);
            foreach (var r in renderers)
            {
                if (r.enabled && r.sharedMaterial != null) return true;
            }
            return false;
        }
    }
}
