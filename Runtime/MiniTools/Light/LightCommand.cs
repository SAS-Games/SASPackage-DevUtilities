using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace SAS.Utilities.DeveloperConsole
{
    [CreateAssetMenu(fileName = "New Light Command", menuName = DeveloperConsole.CommandBasePath + "Light Command")]
    public class LightCommand : CompositeConsoleCommand
    {
        private const string TypeFilterHelp = "all | directional | point | spot | area";

        private readonly TransformOffsetManager _offsetManager = new();
        private readonly Dictionary<Light, bool> _originalEnabledStates = new();
        private readonly StringBuilder _statusBuilder = new(512);

        public override string HelpText =>
            "Light debugging commands:\n" +
            "  Light.Status [type]\n" +
            "  Light.SetAll <On|Off> [type]\n" +
            "  Light.Cull <onscreen|offscreen> <On|Off> [type]\n" +
            "  Light.Restore\n" +
            "  Light.Offset <x> <y> <z> [type]\n" +
            "  Light.ResetOffset\n" +
            "  Light.Reset\n" +
            $"Types: {TypeFilterHelp}";

        private bool Status(string[] args)
        {
            if (!TryParseOptionalTypeFilter(args, 0, out string typeFilter))
                return false;

            Light[] lights = FindLights();
            Camera camera = Camera.main;
            Plane[] planes = camera != null ? GeometryUtility.CalculateFrustumPlanes(camera) : null;

            int matched = 0;
            int enabled = 0;
            int disabled = 0;
            int inactive = 0;
            int onscreen = 0;
            int offscreen = 0;
            var countsByType = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

            foreach (Light light in lights)
            {
                if (!light || !MatchesTypeFilter(light, typeFilter))
                    continue;

                matched++;
                string typeName = light.type.ToString();
                countsByType.TryGetValue(typeName, out int count);
                countsByType[typeName] = count + 1;

                if (!light.gameObject.activeInHierarchy)
                    inactive++;
                else if (light.enabled)
                    enabled++;
                else
                    disabled++;

                if (planes == null)
                    continue;

                if (IsLightVisible(light, planes))
                    onscreen++;
                else
                    offscreen++;
            }

            CleanupDestroyedState();
            _statusBuilder.Length = 0;
            _statusBuilder.AppendLine($"Light.Status [{typeFilter}]")
                .AppendLine($"Matched      : {matched}")
                .AppendLine($"Enabled      : {enabled}")
                .AppendLine($"Disabled     : {disabled}")
                .AppendLine($"Inactive GO  : {inactive}");

            if (planes != null)
            {
                _statusBuilder.AppendLine($"On-screen    : {onscreen}")
                    .AppendLine($"Off-screen   : {offscreen}");
            }
            else
            {
                _statusBuilder.AppendLine("Visibility   : unavailable (no Main Camera)");
            }

            _statusBuilder.AppendLine($"State backups: {_originalEnabledStates.Count}")
                .AppendLine($"Offset tracks: {_offsetManager.TrackedCount}")
                .Append("By type      : ");

            if (countsByType.Count == 0)
            {
                _statusBuilder.Append("none");
            }
            else
            {
                bool first = true;
                var typeNames = new List<string>(countsByType.Keys);
                typeNames.Sort(StringComparer.OrdinalIgnoreCase);
                foreach (string typeName in typeNames)
                {
                    if (!first)
                        _statusBuilder.Append(", ");
                    _statusBuilder.Append(typeName).Append('=').Append(countsByType[typeName]);
                    first = false;
                }
            }

            Debug.Log(_statusBuilder.ToString());
            return true;
        }

        private bool SetAll(string[] args)
        {
            if (args == null || args.Length < 1 || args.Length > 2 ||
                !BoolUtil.TryParse(args[0], out bool enable) ||
                !TryParseOptionalTypeFilter(args, 1, out string typeFilter))
            {
                Debug.LogError(
                    "Light.SetAll: Invalid usage.\n" +
                    "Usage: Light.SetAll <On|Off> [type]\n" +
                    $"Types: {TypeFilterHelp}");
                return false;
            }

            int matched = 0;
            int affected = 0;
            foreach (Light light in FindLights())
            {
                if (!light || !MatchesTypeFilter(light, typeFilter))
                    continue;

                matched++;
                if (TrySetEnabled(light, enable))
                    affected++;
            }

            Debug.Log(
                $"Light.SetAll [{typeFilter} -> {(enable ? "ON" : "OFF")}]: " +
                $"changed {affected}/{matched} matching lights. Use Light.Restore to undo.");
            return true;
        }

        private bool OffsetLights(string[] args)
        {
            if (args == null || args.Length < 3 || args.Length > 4)
            {
                Debug.LogError(
                    "Light.Offset: Invalid usage.\n" +
                    "Usage: Light.Offset <x> <y> <z> [type]\n" +
                    "Example: Light.Offset 0 5 0 point");
                return false;
            }

            if (!VectorParseUtil.TryParseVector3(args[0], args[1], args[2], out Vector3 offset) ||
                !TryParseOptionalTypeFilter(args, 3, out string typeFilter))
            {
                Debug.LogError(
                    "Light.Offset: Invalid offset or light type.\n" +
                    $"Types: {TypeFilterHelp}");
                return false;
            }

            Light[] lights = FindLights();
            var transforms = new List<Transform>(lights.Length);
            foreach (Light light in lights)
            {
                if (light && MatchesTypeFilter(light, typeFilter))
                    transforms.Add(light.transform);
            }

            _offsetManager.ApplyOffset(transforms, offset);
            Debug.Log(
                $"Light.Offset [{typeFilter}]: Applied {offset} to {transforms.Count} lights. " +
                "Use Light.ResetOffset to restore their original positions.");
            return true;
        }

        private bool ResetOffset(string[] args)
        {
            if (args != null && args.Length != 0)
            {
                Debug.LogError("Usage: Light.ResetOffset");
                return false;
            }

            int restored = _offsetManager.TrackedCount;
            _offsetManager.Reset();
            Debug.Log($"Light.ResetOffset: Restored {restored} light positions.");
            return true;
        }

        private bool CullLightsByVisibility(string[] args)
        {
            if (args == null || args.Length < 2 || args.Length > 3 ||
                !TryParseVisibility(args[0], out bool targetOffscreen) ||
                !BoolUtil.TryParse(args[1], out bool enable) ||
                !TryParseOptionalTypeFilter(args, 2, out string typeFilter))
            {
                Debug.LogError(
                    "Light.Cull: Invalid usage.\n" +
                    "Usage: Light.Cull <onscreen|offscreen> <On|Off> [type]\n" +
                    "Example: Light.Cull offscreen Off point");
                return false;
            }

            Camera camera = Camera.main;
            if (!camera)
            {
                Debug.LogError("Light.Cull: No enabled camera tagged MainCamera was found.");
                return false;
            }

            Plane[] planes = GeometryUtility.CalculateFrustumPlanes(camera);
            int total = 0;
            int onscreen = 0;
            int offscreen = 0;
            int matched = 0;
            int affected = 0;

            foreach (Light light in FindLights())
            {
                if (!light || !MatchesTypeFilter(light, typeFilter))
                    continue;

                total++;
                bool isVisible = IsLightVisible(light, planes);
                bool isOffscreen = !isVisible;

                if (isVisible)
                    onscreen++;
                else
                    offscreen++;

                if (isOffscreen != targetOffscreen)
                    continue;

                matched++;
                if (TrySetEnabled(light, enable))
                    affected++;
            }

            string visibility = targetOffscreen ? "offscreen" : "onscreen";
            Debug.Log(
                $"Light.Cull [{visibility}, {typeFilter} -> {(enable ? "ON" : "OFF")}]:\n" +
                $"Matching type : {total}\n" +
                $"On-screen    : {onscreen}\n" +
                $"Off-screen   : {offscreen}\n" +
                $"Targeted     : {matched}\n" +
                $"Changed      : {affected}\n" +
                "Use Light.Restore to undo enabled-state changes.");
            return true;
        }

        private bool Restore(string[] args)
        {
            if (args != null && args.Length != 0)
            {
                Debug.LogError("Usage: Light.Restore");
                return false;
            }

            int restored = RestoreEnabledStates();
            Debug.Log($"Light.Restore: Restored enabled state on {restored} lights.");
            return true;
        }

        private bool Reset(string[] args)
        {
            if (args != null && args.Length != 0)
            {
                Debug.LogError("Usage: Light.Reset");
                return false;
            }

            int restoredStates = RestoreEnabledStates();
            int restoredPositions = _offsetManager.TrackedCount;
            _offsetManager.Reset();
            Debug.Log(
                $"Light.Reset: Restored {restoredStates} enabled states and " +
                $"{restoredPositions} positions.");
            return true;
        }

        private bool TrySetEnabled(Light light, bool enabled)
        {
            if (light.enabled == enabled)
                return false;

            if (!_originalEnabledStates.ContainsKey(light))
                _originalEnabledStates.Add(light, light.enabled);

            light.enabled = enabled;
            return true;
        }

        private int RestoreEnabledStates()
        {
            int restored = 0;
            foreach (KeyValuePair<Light, bool> entry in _originalEnabledStates)
            {
                if (!entry.Key)
                    continue;

                entry.Key.enabled = entry.Value;
                restored++;
            }

            _originalEnabledStates.Clear();
            return restored;
        }

        private void CleanupDestroyedState()
        {
            if (_originalEnabledStates.Count == 0)
                return;

            List<Light> destroyed = null;
            foreach (Light light in _originalEnabledStates.Keys)
            {
                if (light)
                    continue;

                destroyed ??= new List<Light>();
                destroyed.Add(light);
            }

            if (destroyed == null)
                return;

            foreach (Light light in destroyed)
                _originalEnabledStates.Remove(light);
        }

        private static Light[] FindLights() =>
            FindObjectsByType<Light>(FindObjectsInactive.Include, FindObjectsSortMode.None);

        private static bool TryParseOptionalTypeFilter(string[] args, int index, out string typeFilter)
        {
            typeFilter = "all";
            int length = args?.Length ?? 0;
            if (length == index)
                return true;
            if (length != index + 1 || string.IsNullOrWhiteSpace(args[index]))
                return false;

            typeFilter = args[index].Trim().ToLowerInvariant();
            switch (typeFilter)
            {
                case "all":
                case "directional":
                case "point":
                case "spot":
                case "area":
                    return true;
                default:
                    Debug.LogError($"Unknown light type '{args[index]}'. Types: {TypeFilterHelp}");
                    return false;
            }
        }

        private static bool MatchesTypeFilter(Light light, string typeFilter)
        {
            if (typeFilter == "all")
                return true;

            string lightType = light.type.ToString();
            if (typeFilter == "area")
            {
                return lightType.Equals("Area", StringComparison.OrdinalIgnoreCase) ||
                       lightType.Equals("Rectangle", StringComparison.OrdinalIgnoreCase) ||
                       lightType.Equals("Disc", StringComparison.OrdinalIgnoreCase);
            }

            return lightType.Equals(typeFilter, StringComparison.OrdinalIgnoreCase);
        }

        private static bool TryParseVisibility(string value, out bool offscreen)
        {
            offscreen = false;
            if (string.IsNullOrWhiteSpace(value))
                return false;

            if (value.Equals("offscreen", StringComparison.OrdinalIgnoreCase) ||
                value.Equals("off", StringComparison.OrdinalIgnoreCase))
            {
                offscreen = true;
                return true;
            }

            return value.Equals("onscreen", StringComparison.OrdinalIgnoreCase) ||
                   value.Equals("on", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsLightVisible(Light light, Plane[] planes)
        {
            if (light.type == LightType.Directional)
                return true;

            Vector3 position = light.transform.position;
            Bounds bounds;
            if (light.type == LightType.Point)
            {
                bounds = new Bounds(position, Vector3.one * Mathf.Max(0.01f, light.range * 2f));
            }
            else if (light.type == LightType.Spot)
            {
                float range = Mathf.Max(0.01f, light.range);
                float radius = Mathf.Tan(light.spotAngle * 0.5f * Mathf.Deg2Rad) * range;
                Vector3 center = position + light.transform.forward * range * 0.5f;
                float diameter = Mathf.Max(0.01f, radius * 2f);
                float boundsSize = Mathf.Max(range, diameter);
                bounds = new Bounds(center, Vector3.one * boundsSize);
            }
            else
            {
                bounds = new Bounds(position, Vector3.one);
            }

            return GeometryUtility.TestPlanesAABB(planes, bounds);
        }
    }
}
