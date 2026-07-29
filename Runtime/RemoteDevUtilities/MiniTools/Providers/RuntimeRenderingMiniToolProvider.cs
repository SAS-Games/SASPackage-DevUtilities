using System.Collections.Generic;
using SAS.Utilities.RemoteDevUtilities.Protocol.MiniTools;
using UnityEngine;
using UnityEngine.Rendering;

namespace SAS.Utilities.RemoteDevUtilities.MiniTools.Providers
{
    [UnityEngine.Scripting.Preserve]
    internal sealed class RuntimeRenderingMiniToolProvider :
        MiniToolFieldDataProvider
    {
        public override RemoteMiniToolField[] CaptureFields()
        {
            Renderer[] renderers = Object.FindObjectsByType<Renderer>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            Camera[] cameras = Object.FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            Light[] lights = Object.FindObjectsByType<Light>(FindObjectsInactive.Include, FindObjectsSortMode.None);

            int activeRenderers = 0;
            int visibleRenderers = 0;
            int shadowCasters = 0;
            int activeCameras = 0;
            int activeLights = 0;
            var materials = new HashSet<Material>();

            foreach (Renderer renderer in renderers)
            {
                if (renderer == null)
                    continue;
                if (renderer.enabled && renderer.gameObject.activeInHierarchy)
                    activeRenderers++;
                if (renderer.isVisible)
                    visibleRenderers++;
                if (renderer.enabled && renderer.shadowCastingMode != ShadowCastingMode.Off)
                    shadowCasters++;
                foreach (Material material in renderer.sharedMaterials)
                {
                    if (material != null)
                        materials.Add(material);
                }
            }

            foreach (Camera camera in cameras)
            {
                if (camera != null && camera.enabled && camera.gameObject.activeInHierarchy)
                    activeCameras++;
            }

            foreach (Light light in lights)
            {
                if (light != null && light.enabled && light.gameObject.activeInHierarchy)
                    activeLights++;
            }

            return new[]
            {
                Field("renderers", "Renderers", renderers.Length),
                Field("activeRenderers", "Active Renderers", activeRenderers),
                Field("visibleRenderers", "Visible Renderers", visibleRenderers),
                Field("shadowCasters", "Shadow Casters", shadowCasters),
                Field("materials", "Unique Materials", materials.Count),
                Field("cameras", "Active Cameras", activeCameras),
                Field("lights", "Active Lights", activeLights)
            };
        }

        private static RemoteMiniToolField Field(string name, string displayName, int value) => new()
        {
            Name = name,
            DisplayName = displayName,
            Value = value.ToString()
        };
    }
}
