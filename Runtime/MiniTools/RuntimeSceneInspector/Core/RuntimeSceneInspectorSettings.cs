using System;
using UnityEngine;

namespace SAS.Utilities.RuntimeSceneInspector
{
    [CreateAssetMenu(fileName = "RuntimeSceneInspectorSettings", menuName = "SAS/Dev Utilities/Runtime Scene Inspector Settings")]
    public sealed class RuntimeSceneInspectorSettings : ScriptableObject
    {
        [Header("Availability")] [SerializeField] private bool m_EnableInspector = true;

        [SerializeField] private bool m_AutomaticallyCreateBootstrap = true;
        [Header("Behaviour")] [SerializeField] private bool m_PauseWhenOpen;
        [SerializeField] private bool m_ConsumeInput = true;

        [Header("Hierarchy")] [SerializeField, Min(0.1f)] private float m_HierarchyRefreshInterval = 1f;

        [SerializeField] private bool m_IncludeInactiveObjects = true;
        [SerializeField] private bool m_AutomaticRefresh = true;

        [Header("Object Picking")] [SerializeField] private bool m_AllowObjectPicking = true;

        [SerializeField] private LayerMask m_ObjectPickingLayerMask = ~0;
        [SerializeField] private bool m_PickUiObjects = true;
        [SerializeField] private bool m_PickTriggerColliders = true;
        [SerializeField] private bool m_UseRendererBoundsFallback = true;

        [Header("Permissions")] [SerializeField] private bool m_AllowValueChanges = true;

        [SerializeField] private bool m_AllowActivationChanges = true;
        [SerializeField] private bool m_AllowComponentEnableChanges = true;
        [SerializeField] private string[] m_BlockedNamespaces = { "UnityEditor" };
        [SerializeField] private string[] m_BlockedComponentTypes = Array.Empty<string>();

        [Header("Shader Inspection")] [SerializeField] private bool m_AllowShaderInspection = true;

        [SerializeField] private bool m_AllowShaderValueChanges = true;
        [SerializeField] private bool m_AllowMaterialPropertyBlockChanges = true;
        [SerializeField] private bool m_AllowMaterialInstantiation = true;
        [SerializeField] private bool m_AllowSharedMaterialChanges;
        [SerializeField] private bool m_AllowGlobalShaderChanges;
        [SerializeField] private bool m_AllowTextureChanges;
        [SerializeField] private bool m_ShowHiddenShaderProperties;
        [SerializeField, Min(1)] private int m_MaxInspectorMaterialInstances = 256;
        [SerializeField, Min(1)] private int m_MaxVisibleShaderProperties = 256;

        [Header("Editing")] [SerializeField] private float m_NormalNumericStep = 1f;
        [SerializeField] private float m_LargeNumericStep = 10f;
        [SerializeField] private float m_SmallNumericStep = 0.1f;

        [Header("Input")] [SerializeField, Min(0.05f)] private float m_NavigationRepeatDelay = 0.35f;

        [SerializeField, Min(0.02f)] private float m_NavigationRepeatRate = 0.08f;
        [SerializeField, Range(0.1f, 0.95f)] private float m_ControllerDeadZone = 0.55f;

        [Header("Appearance")] [SerializeField, Range(0.75f, 2f)] private float m_UiScale = 1f;

        [SerializeField] private Color m_BackgroundColor = new(0.055f, 0.065f, 0.085f, 0.86f);
        [SerializeField] private Color m_FocusColor = new(0.22f, 0.75f, 1f);
        [SerializeField, Range(0.25f, 0.75f)] private float m_HierarchyPanelWidth = 0.42f;

        [Header("Fonts")] [SerializeField] private Font m_RegularFont;

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

        internal static RuntimeSceneInspectorSettings LoadOrCreateDefaults()
        {
            RuntimeSceneInspectorSettings settings = Resources.Load<RuntimeSceneInspectorSettings>("RuntimeSceneInspectorSettings");
            if (settings != null)
                return settings;

            settings = CreateInstance<RuntimeSceneInspectorSettings>();
            settings.hideFlags = HideFlags.HideAndDontSave;
            return settings;
        }
    }
}
