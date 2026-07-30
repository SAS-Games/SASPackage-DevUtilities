using System;
using UnityEngine;
using UnityEngine.Serialization;

namespace SAS.Utilities.RemoteDevUtilities
{
    public enum BuildDebugUiVisibility
    {
        [InspectorName("Show When Enabled")]
        ShowWhenEnabled = 0,
        [InspectorName("Always Hidden")]
        AlwaysHidden = 1,
        [InspectorName("Hidden While Editor Connected")]
        HiddenWhileEditorConnected = 3
    }

    [Serializable]
    internal sealed class RemoteDevUtilitiesRuntimeConfiguration
    {
        [SerializeField] private bool _enableRemoteAgent = true;
        [SerializeField, InspectorName("Debug UI in Build")]
        [Tooltip("Controls debug-tool UI rendered inside the game build. Debug Host and Remote Dev Utilities Editor UI are configured separately.")]
        private BuildDebugUiVisibility _buildDebugUiVisibility =
            BuildDebugUiVisibility.HiddenWhileEditorConnected;
        [SerializeField] private bool _allowCommandExecution = true;
        [SerializeField] private bool _streamLogs = true;
        [SerializeField] private bool _allowMiniTools = true;
        [SerializeField] private bool _allowRuntimeDebugger = true;
        [SerializeField] private bool _keepPlayerRunningInBackground = true;
        [Header("ENABLE_DEBUG Network Transport")]
        [Tooltip("Direct-IP TCP port used by Development and non-Development Players built with ENABLE_DEBUG.")]
        [SerializeField, Range(1024, 65535)]
        private int _tcpPort = Protocol.RemoteProtocolConstants.DefaultTcpPort;
        [Tooltip("Allow the TCP transport to listen on network interfaces in addition to loopback. A non-empty access token is required.")]
        [SerializeField] private bool _allowTcpConnectionsFromOtherMachines;
        [Tooltip("Shared access token required by the runtime handshake. TCP traffic is not encrypted; use Remote Dev Utilities only on a trusted development network.")]
        [SerializeField] private string _tcpAccessToken = string.Empty;
        [SerializeField, Min(16)] private int _maxQueuedLogs = 512;
        [SerializeField, Min(1)] private int _maxLogsPerBatch = 64;

        internal bool EnableRemoteAgent => _enableRemoteAgent;
        internal BuildDebugUiVisibility BuildUiVisibility =>
            RemoteDevUtilitiesRuntimeSettings.NormalizeBuildUiVisibility(
                _buildDebugUiVisibility);
        internal bool AllowCommandExecution => _allowCommandExecution;
        internal bool StreamLogs => _streamLogs;
        internal bool AllowMiniTools => _allowMiniTools;
        internal bool AllowRuntimeDebugger => _allowRuntimeDebugger;
        internal bool KeepPlayerRunningInBackground =>
            _keepPlayerRunningInBackground;
        internal int TcpPort => Mathf.Clamp(_tcpPort, 1024, 65535);
        internal bool AllowTcpConnectionsFromOtherMachines =>
            _allowTcpConnectionsFromOtherMachines;
        internal string TcpAccessToken =>
            string.IsNullOrWhiteSpace(_tcpAccessToken)
                ? string.Empty
                : _tcpAccessToken;
        internal int MaxQueuedLogs => Mathf.Max(16, _maxQueuedLogs);
        internal int MaxLogsPerBatch => Mathf.Max(1, _maxLogsPerBatch);

        internal void CopyFrom(
            RemoteDevUtilitiesRuntimeSettings settings)
        {
            if (settings == null)
                return;

            _enableRemoteAgent = settings.EnableRemoteAgent;
            _buildDebugUiVisibility = settings.BuildUiVisibility;
            _allowCommandExecution = settings.AllowCommandExecution;
            _streamLogs = settings.StreamLogs;
            _allowMiniTools = settings.AllowMiniTools;
            _allowRuntimeDebugger = settings.AllowRuntimeDebugger;
            _keepPlayerRunningInBackground =
                settings.KeepPlayerRunningInBackground;
            _tcpPort = settings.TcpPort;
            _allowTcpConnectionsFromOtherMachines =
                settings.AllowTcpConnectionsFromOtherMachines;
            _tcpAccessToken = settings.TcpAccessToken;
            _maxQueuedLogs = settings.MaxQueuedLogs;
            _maxLogsPerBatch = settings.MaxLogsPerBatch;
        }
    }

    public sealed class RemoteDevUtilitiesRuntimeSettings :
        ScriptableObject,
        ISerializationCallbackReceiver
    {
        private const string ResourceName = "RemoteDevUtilitiesSettings";
        private static RemoteDevUtilitiesRuntimeSettings
            s_BuildSnapshot;

        [SerializeField] private bool m_EnableRemoteAgent = true;
        [FormerlySerializedAs("m_PresentationMode")]
        [FormerlySerializedAs("m_PlayerDebugUiMode")]
        [SerializeField, InspectorName("Debug UI in Build")]
        [Tooltip("Controls debug-tool UI rendered inside the game build. Debug Host and Remote Dev Utilities Editor UI are configured separately.")]
        private BuildDebugUiVisibility m_BuildDebugUiVisibility =
            BuildDebugUiVisibility.ShowWhenEnabled;
        [SerializeField] private bool m_AllowCommandExecution = true;
        [SerializeField] private bool m_StreamLogs = true;
        [SerializeField] private bool m_AllowMiniTools = true;
        [SerializeField] private bool m_AllowRuntimeDebugger = true;
        [SerializeField] private bool m_KeepPlayerRunningInBackground = true;
        [Header("ENABLE_DEBUG Network Transport")]
        [Tooltip("Direct-IP TCP port used by Development and non-Development Players built with ENABLE_DEBUG.")]
        [SerializeField, Range(1024, 65535)] private int m_TcpPort = Protocol.RemoteProtocolConstants.DefaultTcpPort;
        [Tooltip("Allow the TCP transport to listen on network interfaces in addition to loopback. " + "A non-empty access token is required.")]
        [SerializeField] private bool m_AllowTcpConnectionsFromOtherMachines;
        [Tooltip("Shared access token required by the runtime handshake. TCP traffic is not encrypted; " + "use Remote Dev Utilities only on a trusted development network.")]
        [SerializeField] private string m_TcpAccessToken = string.Empty;
        [SerializeField, Min(16)] private int m_MaxQueuedLogs = 512;
        [SerializeField, Min(1)] private int m_MaxLogsPerBatch = 64;
        [SerializeField, HideInInspector] private bool m_IsBuildSnapshot;

        public bool EnableRemoteAgent => m_EnableRemoteAgent;
        public BuildDebugUiVisibility BuildUiVisibility =>
            NormalizeBuildUiVisibility(m_BuildDebugUiVisibility);
        public bool AllowCommandExecution => m_AllowCommandExecution;
        public bool StreamLogs => m_StreamLogs;
        public bool AllowMiniTools => m_AllowMiniTools;
        public bool AllowRuntimeDebugger => m_AllowRuntimeDebugger;
        public bool KeepPlayerRunningInBackground => m_KeepPlayerRunningInBackground;
        public int TcpPort => Mathf.Clamp(m_TcpPort, 1024, 65535);
        public bool AllowTcpConnectionsFromOtherMachines => m_AllowTcpConnectionsFromOtherMachines;
        public string TcpAccessToken => string.IsNullOrWhiteSpace(m_TcpAccessToken) ? string.Empty : m_TcpAccessToken;
        public int MaxQueuedLogs => Mathf.Max(16, m_MaxQueuedLogs);
        public int MaxLogsPerBatch => Mathf.Max(1, m_MaxLogsPerBatch);
        internal bool IsBuildSnapshot => m_IsBuildSnapshot;

        internal static BuildDebugUiVisibility NormalizeBuildUiVisibility(
            BuildDebugUiVisibility visibility)
        {
            return visibility switch
            {
                BuildDebugUiVisibility.ShowWhenEnabled =>
                    BuildDebugUiVisibility.ShowWhenEnabled,
                BuildDebugUiVisibility.AlwaysHidden =>
                    BuildDebugUiVisibility.AlwaysHidden,
                BuildDebugUiVisibility.HiddenWhileEditorConnected =>
                    BuildDebugUiVisibility.HiddenWhileEditorConnected,
                (BuildDebugUiVisibility)2 =>
                    BuildDebugUiVisibility.ShowWhenEnabled,
                _ => BuildDebugUiVisibility.HiddenWhileEditorConnected
            };
        }

        void ISerializationCallbackReceiver.OnBeforeSerialize()
        {
        }

        void ISerializationCallbackReceiver.OnAfterDeserialize()
        {
            m_BuildDebugUiVisibility =
                NormalizeBuildUiVisibility(m_BuildDebugUiVisibility);
        }

        private void OnValidate()
        {
            m_BuildDebugUiVisibility =
                NormalizeBuildUiVisibility(m_BuildDebugUiVisibility);
        }

        private void OnEnable()
        {
            if (m_IsBuildSnapshot)
                s_BuildSnapshot = this;
        }

        private void OnDisable()
        {
            if (ReferenceEquals(s_BuildSnapshot, this))
                s_BuildSnapshot = null;
        }

        internal void Apply(
            RemoteDevUtilitiesRuntimeConfiguration configuration,
            bool isBuildSnapshot)
        {
            if (configuration == null)
                return;

            m_EnableRemoteAgent = configuration.EnableRemoteAgent;
            m_BuildDebugUiVisibility =
                configuration.BuildUiVisibility;
            m_AllowCommandExecution =
                configuration.AllowCommandExecution;
            m_StreamLogs = configuration.StreamLogs;
            m_AllowMiniTools = configuration.AllowMiniTools;
            m_AllowRuntimeDebugger =
                configuration.AllowRuntimeDebugger;
            m_KeepPlayerRunningInBackground =
                configuration.KeepPlayerRunningInBackground;
            m_TcpPort = configuration.TcpPort;
            m_AllowTcpConnectionsFromOtherMachines =
                configuration.AllowTcpConnectionsFromOtherMachines;
            m_TcpAccessToken = configuration.TcpAccessToken;
            m_MaxQueuedLogs = configuration.MaxQueuedLogs;
            m_MaxLogsPerBatch = configuration.MaxLogsPerBatch;
            m_IsBuildSnapshot = isBuildSnapshot;
            if (m_IsBuildSnapshot)
                s_BuildSnapshot = this;
        }

        internal static RemoteDevUtilitiesRuntimeSettings LoadOrCreateDefaults()
        {
            if (s_BuildSnapshot != null)
                return s_BuildSnapshot;

            foreach (RemoteDevUtilitiesRuntimeSettings candidate in
                     Resources.FindObjectsOfTypeAll<
                         RemoteDevUtilitiesRuntimeSettings>())
            {
                if (candidate != null && candidate.m_IsBuildSnapshot)
                {
                    s_BuildSnapshot = candidate;
                    return candidate;
                }
            }

            RemoteDevUtilitiesRuntimeSettings settings = Resources.Load<RemoteDevUtilitiesRuntimeSettings>(ResourceName);
            if (settings != null)
                return settings;

            Debug.LogWarning(
                $"[RemoteDevUtilities] Resources/{ResourceName}.asset could not be loaded. " +
                "Using in-memory runtime defaults.");
            settings = CreateInstance<RemoteDevUtilitiesRuntimeSettings>();
            settings.hideFlags = HideFlags.HideAndDontSave;
            return settings;
        }
    }
}
