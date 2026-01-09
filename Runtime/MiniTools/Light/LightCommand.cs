using System.Collections.Generic;
using UnityEngine;

namespace SAS.Utilities.DeveloperConsole
{
    [CreateAssetMenu(fileName = "New Light Command", menuName = DeveloperConsole.CommandBasePath + "Light Command")]
    public class LightCommand : CompositeConsoleCommand
    {
        private readonly TransformOffsetManager _offsetManager = new();

        public override string HelpText => "";

        private bool OffsetLights(string[] args)
        {
            if (args == null || args.Length < 3)
            {
                Debug.LogError(
                    "Light.Offset: Missing parameters.\n" +
                    "Usage: Light.Offset <x> <y> <z>\n" +
                    "Example: Light.Offset 0 5 0"
                );
                return false;
            }

            if (!VectorParseUtil.TryParseVector3(args[0], args[1], args[2], out var offset))
            {
                Debug.LogError(
                    $"Light.Offset: Invalid values.\n" +
                    $"Received: x='{args[0]}', y='{args[1]}', z='{args[2]}'\n" +
                    "All values must be valid numbers."
                );
                return false;
            }

            Light[] lights = FindObjectsByType<Light>(FindObjectsInactive.Include, FindObjectsSortMode.None);

            var transforms = new List<Transform>(lights.Length);
            foreach (var l in lights)
            {
                if (!l) continue;
                transforms.Add(l.transform);
            }

            _offsetManager.ApplyOffset(transforms, offset);

            Debug.Log($"Light.Offset: Applied offset {offset} to {transforms.Count} lights.");
            return true;
        }

        private bool ResetOffset(string[] args)
        {
            _offsetManager.Reset();
            Debug.Log("Light.ResetOffset: Light positions restored.");
            return true;
        }

        private bool CullLightsByVisibility(string[] args)
        {
            if (args == null || args.Length < 2)
            {
                Debug.LogError(
                    "Light.Cull: Invalid usage.\n" +
                    "Usage: Light.Cull <onscreen|offscreen> <on|off>\n" +
                    "Example: Light.Cull offscreen off"
                );
                return false;
            }

            string mode = args[0].ToLowerInvariant();

            if (!BoolUtil.TryParse(args[1], out bool enable))
            {
                Debug.LogError(
                    "Light.Cull: Invalid state.\n" +
                    "Usage: Light.Cull <onscreen|offscreen> <on|off>"
                );
                return false;
            }

            bool targetOffscreen;
            if (mode == "offscreen")
                targetOffscreen = true;
            else if (mode == "onscreen")
                targetOffscreen = false;
            else
            {
                Debug.LogError("Light.Cull: First argument must be 'onscreen' or 'offscreen'.");
                return false;
            }

            Camera cam = Camera.main;
            if (!cam)
            {
                Debug.LogError("Light.Cull: No Main Camera found.");
                return false;
            }

            var lights = FindObjectsByType<Light>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);

            var planes = GeometryUtility.CalculateFrustumPlanes(cam);

            int total = 0;
            int onscreen = 0;
            int offscreen = 0;
            int affected = 0;

            foreach (var l in lights)
            {
                if (!l) continue;

                total++;

                bool isVisible = IsLightVisible(l, planes);
                bool isOffscreen = !isVisible;

                if (isVisible)
                    onscreen++;
                else
                    offscreen++;

                if (isOffscreen == targetOffscreen)
                {
                    if (l.enabled != enable)
                    {
                        l.enabled = enable;
                        affected++;
                    }
                }
            }

            Debug.Log(
                $"Light.Cull [{mode} -> {(enable ? "ON" : "OFF")}]\n" +
                $"Total Lights : {total}\n" +
                $"On-screen    : {onscreen}\n" +
                $"Off-screen   : {offscreen}\n" +
                $"Affected     : {affected}"
            );

            return true;
        }

        private bool IsLightVisible(Light light, Plane[] planes)
        {
            // Directional lights are always considered visible
            if (light.type == LightType.Directional)
                return true;

            Bounds bounds;

            if (light.type == LightType.Point || light.type == LightType.Spot)
            {
                bounds = new Bounds(
                    light.transform.position,
                    Vector3.one * light.range * 2f
                );
            }
            else
            {
                bounds = new Bounds(light.transform.position, Vector3.one);
            }

            return GeometryUtility.TestPlanesAABB(planes, bounds);
        }
    }
}