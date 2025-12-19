using UnityEngine;

namespace ImmersiveMapInterface.Interaction
{
    /// <summary>
    /// Ensures a ControllerVisualToggle exists at runtime so physical controllers are visible instead of hand ghosts.
    /// </summary>
    public static class ControllerVisualBootstrap
    {
        private const string RuntimeHelperName = "[Runtime] ControllerVisualToggle";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void EnsureControllerVisuals()
        {
            var existing = Object.FindObjectOfType<ControllerVisualToggle>();
            if (existing != null)
            {
                ApplyDefaults(existing);
                return;
            }

            var host = FindRigRoot() ?? new GameObject(RuntimeHelperName);
            var toggle = host.GetComponent<ControllerVisualToggle>() ?? host.AddComponent<ControllerVisualToggle>();
            ApplyDefaults(toggle);
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
    }
}
