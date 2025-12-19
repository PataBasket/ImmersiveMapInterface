using UnityEngine;

namespace ImmersiveMapInterface.Interaction
{
    /// <summary>
    /// Ensures a ControllerVisualToggle exists at runtime so physical controllers are visible instead of hand ghosts.
    /// </summary>
    public static class ControllerVisualBootstrap
    {
        private const string RuntimeHelperName = "[Runtime] ControllerVisualToggle";
        private const string LeftCloneName = "ControllerVisualClone_Left";
        private const string RightCloneName = "ControllerVisualClone_Right";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void EnsureControllerVisuals()
        {
            var existing = Object.FindObjectOfType<ControllerVisualToggle>();
            if (existing != null)
            {
                ApplyDefaults(existing);
            }
            else
            {
                var host = FindRigRoot() ?? new GameObject(RuntimeHelperName);
                var toggle = host.GetComponent<ControllerVisualToggle>() ?? host.AddComponent<ControllerVisualToggle>();
                ApplyDefaults(toggle);
            }

            CloneControllerVisual("OVRLeftControllerVisual", LeftCloneName, new[] { "LeftPointer", "LeftControllerAnchor", "LeftHandAnchor" });
            CloneControllerVisual("OVRRightControllerVisual", RightCloneName, new[] { "RightPointer", "RightControllerAnchor", "RightHandAnchor" });
        }

        private static GameObject FindRigRoot()
        {
            var rig = Object.FindObjectOfType<OVRCameraRig>();
            if (rig != null) return rig.gameObject;

            var manager = Object.FindObjectOfType<OVRManager>();
            if (manager != null) return manager.gameObject;

            var byName = GameObject.Find("OVRCameraRig");
            if (byName != null) return byName;

            return Camera.main != null ? Camera.main.gameObject : null;
        }

        private static void ApplyDefaults(ControllerVisualToggle toggle)
        {
            toggle.mode = ControllerVisualToggle.VisualMode.ControllersOnly;
            toggle.autoFind = true;
            toggle.spawnProxyIfMissing = true;
            toggle.preferProxyControllers = true;
            toggle.forceControllerVisibility = true;
            toggle.disableActiveStateWrappers = true;
            toggle.enabled = true;
            toggle.Apply();
        }

        private static void CloneControllerVisual(string sourceName, string cloneName, string[] anchorCandidates)
        {
            if (GameObject.Find(cloneName) != null) return;
            var source = GameObject.Find(sourceName);
            if (source == null) return;

            var anchor = FindFirst(anchorCandidates);
            if (anchor == null) return;

            var mesh = FindChildRecursive(source.transform, "OVRControllerPrefab");
            if (mesh == null) return;

            var clone = Object.Instantiate(mesh.gameObject, anchor, false);
            clone.name = cloneName;
            clone.SetActive(true);

            foreach (var col in clone.GetComponentsInChildren<Collider>(true))
            {
                col.enabled = false;
            }
            foreach (var rb in clone.GetComponentsInChildren<Rigidbody>(true))
            {
                rb.isKinematic = true;
                rb.detectCollisions = false;
            }

            source.SetActive(false);
        }

        private static Transform FindFirst(string[] names)
        {
            if (names == null) return null;
            foreach (var n in names)
            {
                var go = GameObject.Find(n);
                if (go != null) return go.transform;
            }
            return null;
        }

        private static Transform FindChildRecursive(Transform parent, string name)
        {
            if (parent == null) return null;
            for (int i = 0; i < parent.childCount; i++)
            {
                var child = parent.GetChild(i);
                if (child.name == name) return child;
                var hit = FindChildRecursive(child, name);
                if (hit != null) return hit;
            }
            return null;
        }
    }
}
