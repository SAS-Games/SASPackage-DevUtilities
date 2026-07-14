using UnityEngine;

namespace SAS.Utilities.RuntimeDebugger
{
    [CreateAssetMenu(fileName = "RuntimeDebuggerSettings", menuName = "SAS/Dev Utilities/Runtime Debugger Settings")]
    public sealed class RuntimeDebuggerSettings : ScriptableObject
    {
        [Header("Availability")]
        [SerializeField] private bool m_EnableDebugger = true;
        [SerializeField] private bool m_AutomaticallyCreateBootstrap = true;
        [Header("Behaviour")]
        [SerializeField] private bool m_PauseWhenOpen;
        [SerializeField] private bool m_ConsumeInput = true;
        [Header("Hierarchy")]
        [SerializeField, Min(0.1f)] private float m_HierarchyRefreshInterval = 1f;
        [SerializeField] private bool m_IncludeInactiveObjects = true;
        [SerializeField] private bool m_AutomaticRefresh = true;
        [Header("Permissions")]
        [SerializeField] private bool m_AllowValueChanges = true;
        [SerializeField] private bool m_AllowActivationChanges = true;
        [SerializeField] private bool m_AllowComponentEnableChanges = true;
        [SerializeField] private string[] m_BlockedNamespaces = { "UnityEditor" };
        [SerializeField] private string[] m_BlockedComponentTypes = System.Array.Empty<string>();
        [Header("Editing")]
        [SerializeField] private float m_NormalNumericStep = 1f;
        [SerializeField] private float m_LargeNumericStep = 10f;
        [SerializeField] private float m_SmallNumericStep = 0.1f;
        [Header("Input")]
        [SerializeField, Min(0.05f)] private float m_NavigationRepeatDelay = 0.35f;
        [SerializeField, Min(0.02f)] private float m_NavigationRepeatRate = 0.08f;
        [SerializeField, Range(0.1f, 0.95f)] private float m_ControllerDeadZone = 0.55f;
        [Header("Appearance")]
        [SerializeField, Range(0.75f, 2f)] private float m_UiScale = 1f;
        [SerializeField] private Color m_BackgroundColor = new(0.055f, 0.065f, 0.085f, 0.98f);
        [SerializeField] private Color m_FocusColor = new(0.22f, 0.75f, 1f);
        [SerializeField, Range(0.25f, 0.75f)] private float m_HierarchyPanelWidth = 0.42f;

        public bool EnableDebugger => m_EnableDebugger;
        public bool AutomaticallyCreateBootstrap => m_AutomaticallyCreateBootstrap;
        public bool PauseWhenOpen => m_PauseWhenOpen;
        public bool ConsumeInput => m_ConsumeInput;
        public float HierarchyRefreshInterval => Mathf.Max(0.1f, m_HierarchyRefreshInterval);
        public bool IncludeInactiveObjects => m_IncludeInactiveObjects;
        public bool AutomaticRefresh => m_AutomaticRefresh;
        public bool AllowValueChanges => m_AllowValueChanges;
        public bool AllowActivationChanges => m_AllowActivationChanges;
        public bool AllowComponentEnableChanges => m_AllowComponentEnableChanges;
        public string[] BlockedNamespaces => m_BlockedNamespaces ?? System.Array.Empty<string>();
        public string[] BlockedComponentTypes => m_BlockedComponentTypes ?? System.Array.Empty<string>();
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

        internal static RuntimeDebuggerSettings LoadOrCreateDefaults()
        {
            RuntimeDebuggerSettings settings = Resources.Load<RuntimeDebuggerSettings>("RuntimeDebuggerSettings");
            if (settings != null)
                return settings;

            settings = CreateInstance<RuntimeDebuggerSettings>();
            settings.hideFlags = HideFlags.HideAndDontSave;
            return settings;
        }
    }
}
