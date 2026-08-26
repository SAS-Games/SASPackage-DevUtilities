using System;
using UnityEngine;

namespace SAS.Utilities.RuntimeSceneInspector
{
    [Serializable]
    public sealed class RuntimeSceneInspectorConfiguration
    {
        internal RuntimeSceneInspectorConfiguration(bool enableShaderInspectionDefaults)
        {
            if (!enableShaderInspectionDefaults)
                return;
            m_AllowShaderInspection = true;
            m_AllowShaderValueChanges = true;
            m_AllowMaterialPropertyBlockChanges = true;
            m_AllowMaterialInstantiation = true;
        }

        public RuntimeSceneInspectorConfiguration()
        {
        }

        [SerializeField] private bool m_EnableInspector = true;

        [SerializeField] private bool m_AutomaticallyCreateBootstrap = true;
        [SerializeField] private bool m_PauseWhenOpen;
        [SerializeField] private bool m_ConsumeInput = true;

        [SerializeField, Min(0.1f)] private float m_HierarchyRefreshInterval = 1f;

        [SerializeField] private bool m_IncludeInactiveObjects = true;
        [SerializeField] private bool m_AutomaticRefresh = true;

        [SerializeField] private bool m_AllowObjectPicking = true;

        [SerializeField] private LayerMask m_ObjectPickingLayerMask = ~0;
        [SerializeField] private bool m_PickUiObjects = true;
        [SerializeField] private bool m_PickTriggerColliders = true;
        [SerializeField] private bool m_UseRendererBoundsFallback = true;

        [SerializeField] private bool m_AllowValueChanges = true;

        [SerializeField] private bool m_AllowActivationChanges = true;
        [SerializeField] private bool m_AllowComponentEnableChanges = true;
        [SerializeField] private string[] m_BlockedNamespaces = { "UnityEditor" };
        [SerializeField] private string[] m_BlockedComponentTypes = Array.Empty<string>();

        [SerializeField] private bool m_AllowShaderInspection;

        [SerializeField] private bool m_AllowShaderValueChanges;
        [SerializeField] private bool m_AllowMaterialPropertyBlockChanges;
        [SerializeField] private bool m_AllowMaterialInstantiation;
        [SerializeField] private bool m_AllowSharedMaterialChanges;
        [SerializeField] private bool m_AllowGlobalShaderChanges;
        [SerializeField] private bool m_AllowTextureChanges;
        [SerializeField] private bool m_ShowHiddenShaderProperties;
        [SerializeField, Min(1)] private int m_MaxInspectorMaterialInstances = 256;
        [SerializeField, Min(1)] private int m_MaxVisibleShaderProperties = 256;

        [SerializeField] private float m_NormalNumericStep = 1f;
        [SerializeField] private float m_LargeNumericStep = 10f;
        [SerializeField] private float m_SmallNumericStep = 0.1f;

        [SerializeField, Min(0.05f)] private float m_NavigationRepeatDelay = 0.35f;

        [SerializeField, Min(0.02f)] private float m_NavigationRepeatRate = 0.08f;
        [SerializeField, Range(0.1f, 0.95f)] private float m_ControllerDeadZone = 0.55f;

        [SerializeField, Range(0.75f, 2f)] private float m_UiScale = 1f;

        [SerializeField] private Color m_BackgroundColor = new(0.055f, 0.065f, 0.085f, 0.86f);
        [SerializeField] private Color m_FocusColor = new(0.22f, 0.75f, 1f);
        [SerializeField, Range(0.25f, 0.75f)] private float m_HierarchyPanelWidth = 0.42f;

        [SerializeField] private Font m_RegularFont;

        [SerializeField] private Font m_BoldFont;

        public bool EnableInspector => m_EnableInspector;
        public bool AutomaticallyCreateBootstrap => m_AutomaticallyCreateBootstrap;
        public bool PauseWhenOpen => m_PauseWhenOpen;
        public bool ConsumeInput => m_ConsumeInput;
        public float HierarchyRefreshInterval => Mathf.Max(0.1f, m_HierarchyRefreshInterval);
        public bool IncludeInactiveObjects => m_IncludeInactiveObjects;
        public bool AutomaticRefresh => m_AutomaticRefresh;
        public bool AllowObjectPicking => m_AllowObjectPicking;
        public int ObjectPickingLayerMask => m_ObjectPickingLayerMask.value;
        public bool PickUiObjects => m_PickUiObjects;
        public bool PickTriggerColliders => m_PickTriggerColliders;
        public bool UseRendererBoundsFallback => m_UseRendererBoundsFallback;
        public bool AllowValueChanges => m_AllowValueChanges;
        public bool AllowActivationChanges => m_AllowActivationChanges;
        public bool AllowComponentEnableChanges => m_AllowComponentEnableChanges;
        public string[] BlockedNamespaces => m_BlockedNamespaces ?? Array.Empty<string>();
        public string[] BlockedComponentTypes => m_BlockedComponentTypes ?? Array.Empty<string>();
        public bool AllowShaderInspection => m_AllowShaderInspection;
        public bool AllowShaderValueChanges => m_AllowShaderValueChanges;
        public bool AllowMaterialPropertyBlockChanges => m_AllowMaterialPropertyBlockChanges;
        public bool AllowMaterialInstantiation => m_AllowMaterialInstantiation;
        public bool AllowSharedMaterialChanges => m_AllowSharedMaterialChanges;
        public bool AllowGlobalShaderChanges => m_AllowGlobalShaderChanges;
        public bool AllowTextureChanges => m_AllowTextureChanges;
        public bool ShowHiddenShaderProperties => m_ShowHiddenShaderProperties;
        public int MaxInspectorMaterialInstances => Mathf.Max(1, m_MaxInspectorMaterialInstances);
        public int MaxVisibleShaderProperties => Mathf.Max(1, m_MaxVisibleShaderProperties);
        public float NormalNumericStep => m_NormalNumericStep;
        public float LargeNumericStep => m_LargeNumericStep;
        public float SmallNumericStep => m_SmallNumericStep;
        public float NavigationRepeatDelay => m_NavigationRepeatDelay;
        public float NavigationRepeatRate => m_NavigationRepeatRate;
        public float ControllerDeadZone => m_ControllerDeadZone;
        public float UiScale => m_UiScale;
        public Color BackgroundColor => m_BackgroundColor;
        public Color FocusColor => m_FocusColor;
        public float HierarchyPanelWidth => m_HierarchyPanelWidth;
        public Font RegularFont => m_RegularFont;
        public Font BoldFont => m_BoldFont != null ? m_BoldFont : m_RegularFont;

        internal void CopyFrom(RuntimeSceneInspectorConfiguration source)
        {
            if (source == null)
                return;

            m_EnableInspector = source.m_EnableInspector;
            m_AutomaticallyCreateBootstrap = source.m_AutomaticallyCreateBootstrap;
            m_PauseWhenOpen = source.m_PauseWhenOpen;
            m_ConsumeInput = source.m_ConsumeInput;
            m_HierarchyRefreshInterval = source.m_HierarchyRefreshInterval;
            m_IncludeInactiveObjects = source.m_IncludeInactiveObjects;
            m_AutomaticRefresh = source.m_AutomaticRefresh;
            m_AllowObjectPicking = source.m_AllowObjectPicking;
            m_ObjectPickingLayerMask = source.m_ObjectPickingLayerMask;
            m_PickUiObjects = source.m_PickUiObjects;
            m_PickTriggerColliders = source.m_PickTriggerColliders;
            m_UseRendererBoundsFallback = source.m_UseRendererBoundsFallback;
            m_AllowValueChanges = source.m_AllowValueChanges;
            m_AllowActivationChanges = source.m_AllowActivationChanges;
            m_AllowComponentEnableChanges = source.m_AllowComponentEnableChanges;
            m_BlockedNamespaces = source.m_BlockedNamespaces == null
                ? Array.Empty<string>()
                : (string[])source.m_BlockedNamespaces.Clone();
            m_BlockedComponentTypes = source.m_BlockedComponentTypes == null
                ? Array.Empty<string>()
                : (string[])source.m_BlockedComponentTypes.Clone();
            m_AllowShaderInspection = source.m_AllowShaderInspection;
            m_AllowShaderValueChanges = source.m_AllowShaderValueChanges;
            m_AllowMaterialPropertyBlockChanges = source.m_AllowMaterialPropertyBlockChanges;
            m_AllowMaterialInstantiation = source.m_AllowMaterialInstantiation;
            m_AllowSharedMaterialChanges = source.m_AllowSharedMaterialChanges;
            m_AllowGlobalShaderChanges = source.m_AllowGlobalShaderChanges;
            m_AllowTextureChanges = source.m_AllowTextureChanges;
            m_ShowHiddenShaderProperties = source.m_ShowHiddenShaderProperties;
            m_MaxInspectorMaterialInstances = source.m_MaxInspectorMaterialInstances;
            m_MaxVisibleShaderProperties = source.m_MaxVisibleShaderProperties;
            m_NormalNumericStep = source.m_NormalNumericStep;
            m_LargeNumericStep = source.m_LargeNumericStep;
            m_SmallNumericStep = source.m_SmallNumericStep;
            m_NavigationRepeatDelay = source.m_NavigationRepeatDelay;
            m_NavigationRepeatRate = source.m_NavigationRepeatRate;
            m_ControllerDeadZone = source.m_ControllerDeadZone;
            m_UiScale = source.m_UiScale;
            m_BackgroundColor = source.m_BackgroundColor;
            m_FocusColor = source.m_FocusColor;
            m_HierarchyPanelWidth = source.m_HierarchyPanelWidth;
            m_RegularFont = source.m_RegularFont;
            m_BoldFont = source.m_BoldFont;
        }
    }

    public sealed class RuntimeSceneInspectorSettings : ScriptableObject
    {
        private static readonly RuntimeSceneInspectorConfiguration RuntimeDefaults = new();

        [SerializeField] private RuntimeSceneInspectorConfiguration m_Configuration = new(true);

        private RuntimeSceneInspectorConfiguration Configuration =>
            m_Configuration ??= new RuntimeSceneInspectorConfiguration();

        public bool EnableInspector => Configuration.EnableInspector;
        public bool AutomaticallyCreateBootstrap => Configuration.AutomaticallyCreateBootstrap;
        public bool PauseWhenOpen => Configuration.PauseWhenOpen;
        public bool ConsumeInput => Configuration.ConsumeInput;
        public float HierarchyRefreshInterval => Configuration.HierarchyRefreshInterval;
        public bool IncludeInactiveObjects => Configuration.IncludeInactiveObjects;
        public bool AutomaticRefresh => Configuration.AutomaticRefresh;
        public bool AllowObjectPicking => Configuration.AllowObjectPicking;
        public int ObjectPickingLayerMask => Configuration.ObjectPickingLayerMask;
        public bool PickUiObjects => Configuration.PickUiObjects;
        public bool PickTriggerColliders => Configuration.PickTriggerColliders;
        public bool UseRendererBoundsFallback => Configuration.UseRendererBoundsFallback;
        public bool AllowValueChanges => Configuration.AllowValueChanges;
        public bool AllowActivationChanges => Configuration.AllowActivationChanges;
        public bool AllowComponentEnableChanges => Configuration.AllowComponentEnableChanges;
        public string[] BlockedNamespaces => Configuration.BlockedNamespaces;
        public string[] BlockedComponentTypes => Configuration.BlockedComponentTypes;
        public bool AllowShaderInspection => Configuration.AllowShaderInspection;
        public bool AllowShaderValueChanges => Configuration.AllowShaderValueChanges;
        public bool AllowMaterialPropertyBlockChanges => Configuration.AllowMaterialPropertyBlockChanges;
        public bool AllowMaterialInstantiation => Configuration.AllowMaterialInstantiation;
        public bool AllowSharedMaterialChanges => Configuration.AllowSharedMaterialChanges;
        public bool AllowGlobalShaderChanges => Configuration.AllowGlobalShaderChanges;
        public bool AllowTextureChanges => Configuration.AllowTextureChanges;
        public bool ShowHiddenShaderProperties => Configuration.ShowHiddenShaderProperties;
        public int MaxInspectorMaterialInstances => Configuration.MaxInspectorMaterialInstances;
        public int MaxVisibleShaderProperties => Configuration.MaxVisibleShaderProperties;
        public float NormalNumericStep => Configuration.NormalNumericStep;
        public float LargeNumericStep => Configuration.LargeNumericStep;
        public float SmallNumericStep => Configuration.SmallNumericStep;
        public float NavigationRepeatDelay => Configuration.NavigationRepeatDelay;
        public float NavigationRepeatRate => Configuration.NavigationRepeatRate;
        public float ControllerDeadZone => Configuration.ControllerDeadZone;
        public float UiScale => Configuration.UiScale;
        public Color BackgroundColor => Configuration.BackgroundColor;
        public Color FocusColor => Configuration.FocusColor;
        public float HierarchyPanelWidth => Configuration.HierarchyPanelWidth;
        public Font RegularFont => Configuration.RegularFont;
        public Font BoldFont => Configuration.BoldFont;

        internal void Apply(RuntimeSceneInspectorConfiguration configuration)
        {
            Configuration.CopyFrom(configuration);
        }

        internal static void ConfigureDefaults(RuntimeSceneInspectorConfiguration configuration)
        {
            RuntimeDefaults.CopyFrom(configuration);
        }

        internal static RuntimeSceneInspectorSettings LoadOrCreateDefaults()
        {
            RuntimeSceneInspectorSettings settings = CreateInstance<RuntimeSceneInspectorSettings>();
            settings.Apply(RuntimeDefaults);
            settings.hideFlags = HideFlags.HideAndDontSave;
            return settings;
        }
    }
}
