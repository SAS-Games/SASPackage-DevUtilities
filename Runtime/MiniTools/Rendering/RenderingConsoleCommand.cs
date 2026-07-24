using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.Rendering;
#if UNITY_RENDER_PIPELINE_UNIVERSAL
using UnityEngine.Rendering.Universal;
#endif

namespace SAS.Utilities.DeveloperConsole
{
    [CreateAssetMenu(fileName = "Rendering Command", menuName = DeveloperConsole.CommandBasePath + "Rendering Command")]
    public sealed class RenderingConsoleCommand : CompositeConsoleCommand
    {
        [SerializeField] private string m_HelpText = "Runtime rendering diagnostics, the SRP Rendering Debugger, renderer features, and loaded-resource reports.";
        public override string HelpText => m_HelpText;

        private bool? _debuggerEnabled;
        private bool? _debuggerDisplayed;
#if UNITY_RENDER_PIPELINE_UNIVERSAL
        private readonly Dictionary<ScriptableRendererFeature, bool> _featureStates = new();
        private static readonly FieldInfo RendererDataListField = typeof(UniversalRenderPipelineAsset)
            .GetField("m_RendererDataList", BindingFlags.Instance | BindingFlags.NonPublic);
#endif

        private bool Status(string[] args)
        {
            if (!HasNoArguments(args))
                return false;
            Debug.Log(RuntimeRenderingReporter.BuildStatusReport() +
                      $"\nRendering Debugger: enabled={DebugManager.instance.enableRuntimeUI}, displayed={DebugManager.instance.displayRuntimeUI}");
            return true;
        }

        private bool Debugger(string[] args)
        {
#if !UNITY_EDITOR && !DEVELOPMENT_BUILD && !ENABLE_DEBUG
            return false;
#else
            if (args == null || args.Length != 1 || !BoolUtil.TryParse(args[0], out bool visible))
                return false;

            DebugManager manager = DebugManager.instance;
            _debuggerEnabled ??= manager.enableRuntimeUI;
            _debuggerDisplayed ??= manager.displayRuntimeUI;
            if (visible)
                manager.enableRuntimeUI = true;
            if (manager.displayRuntimeUI != visible)
                manager.displayRuntimeUI = visible;
            return true;
#endif
        }

        private bool Reset(string[] args)
        {
            if (!HasNoArguments(args))
                return false;

#if UNITY_RENDER_PIPELINE_UNIVERSAL
            RestoreFeatureStates();
#endif
            if (_debuggerEnabled.HasValue || _debuggerDisplayed.HasValue)
            {
                DebugManager manager = DebugManager.instance;
                if (_debuggerEnabled.HasValue)
                    manager.enableRuntimeUI = _debuggerEnabled.Value;
                if (_debuggerDisplayed.HasValue && manager.displayRuntimeUI != _debuggerDisplayed.Value)
                    manager.displayRuntimeUI = _debuggerDisplayed.Value;
                _debuggerEnabled = null;
                _debuggerDisplayed = null;
            }
            Debug.Log("[Rendering] Restored captured debugger and renderer-feature states.");
            return true;
        }

        private bool Features(string[] args)
        {
            if (!HasNoArguments(args))
                return false;
#if UNITY_RENDER_PIPELINE_UNIVERSAL
            ScriptableRendererData[] dataList = GetActiveRendererData();
            if (dataList.Length == 0)
                return false;
            string report = "[Rendering.Features]\n" + string.Join("\n", dataList.SelectMany((data, index) =>
                data.rendererFeatures.Where(feature => feature != null).Select(feature =>
                    $"- Renderer[{index}] {data.name}/{feature.name}: active={feature.isActive}, type={feature.GetType().Name}")));
            Debug.Log(report);
            return true;
#else
            Debug.LogWarning("[Rendering.Features] Requires URP.");
            return false;
#endif
        }

        private bool Feature(string[] args)
        {
#if UNITY_RENDER_PIPELINE_UNIVERSAL
            if (args == null || args.Length < 2 || !BoolUtil.TryParse(args[^1], out bool active))
                return false;
            string featureName = string.Join(" ", args, 0, args.Length - 1);
            ScriptableRendererFeature[] matches = GetActiveRendererData()
                .SelectMany(data => data.rendererFeatures)
                .Where(feature => feature != null && feature.name.Equals(featureName, StringComparison.OrdinalIgnoreCase))
                .Distinct()
                .ToArray();
            foreach (ScriptableRendererFeature feature in matches)
            {
                _featureStates.TryAdd(feature, feature.isActive);
                feature.SetActive(active);
            }
            Debug.Log($"[Rendering.Feature] Set {matches.Length} feature(s) named '{featureName}' to active={active}.");
            return matches.Length > 0;
#else
            return false;
#endif
        }

        private bool RestoreFeatures(string[] args)
        {
            if (!HasNoArguments(args))
                return false;
#if UNITY_RENDER_PIPELINE_UNIVERSAL
            int count = _featureStates.Count;
            RestoreFeatureStates();
            Debug.Log($"[Rendering.RestoreFeatures] Restored {count} renderer feature(s).");
            return true;
#else
            return false;
#endif
        }

        private bool Textures(string[] args) => ReportTop(args, RuntimeRenderingReporter.BuildTextureReport);
        private bool RenderTargets(string[] args) => ReportTop(args, RuntimeRenderingReporter.BuildRenderTargetReport);
        private bool Materials(string[] args) => ReportTop(args, RuntimeRenderingReporter.BuildMaterialReport);

        private bool Shaders(string[] args)
        {
            if (!HasNoArguments(args))
                return false;
            Debug.Log(RuntimeRenderingReporter.BuildShaderReport());
            return true;
        }

        private static bool ReportTop(string[] args, Func<int, string> reporter)
        {
            int count = 20;
            if (args == null || args.Length > 1 ||
                (args.Length == 1 && (!int.TryParse(args[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out count) || count < 1 || count > 100)))
                return false;
            Debug.Log(reporter(count));
            return true;
        }

        private static bool HasNoArguments(string[] args) => args != null && args.Length == 0;

#if UNITY_RENDER_PIPELINE_UNIVERSAL
        private static ScriptableRendererData[] GetActiveRendererData()
        {
            if (GraphicsSettings.currentRenderPipeline is not UniversalRenderPipelineAsset urp || RendererDataListField == null)
                return Array.Empty<ScriptableRendererData>();
            return (RendererDataListField.GetValue(urp) as ScriptableRendererData[])?.Where(data => data != null).ToArray()
                   ?? Array.Empty<ScriptableRendererData>();
        }

        private void RestoreFeatureStates()
        {
            foreach (KeyValuePair<ScriptableRendererFeature, bool> entry in _featureStates)
                if (entry.Key != null) entry.Key.SetActive(entry.Value);
            _featureStates.Clear();
        }
#endif
    }
}
