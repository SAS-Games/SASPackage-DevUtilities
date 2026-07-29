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

    [CreateAssetMenu(fileName = "RemoteDevUtilitiesSettings", menuName = "Dev Utilities/Remote Runtime Settings")]
    public sealed class RemoteDevUtilitiesRuntimeSettings :
        ScriptableObject,
        ISerializationCallbackReceiver
    {
        private const string ResourceName = "RemoteDevUtilitiesSettings";

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

        internal static RemoteDevUtilitiesRuntimeSettings LoadOrCreateDefaults()
        {
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
