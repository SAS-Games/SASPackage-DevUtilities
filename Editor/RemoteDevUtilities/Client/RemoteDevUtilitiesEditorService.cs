using System;
using SAS.Utilities.RemoteDevUtilities.Editor.Connection;
using UnityEditor;
using UnityEngine;

namespace SAS.Utilities.RemoteDevUtilities.Editor.Client
{
    [InitializeOnLoad]
    internal static class RemoteDevUtilitiesEditorService
    {
        private const string ReconnectPlayerKey = "RemoteDevUtilities.ReconnectPlayer";
        private const string AccessTokenSessionKey = "RemoteDevUtilities.AccessToken";
        private const string ReconnectStateKey = "RemoteDevUtilities.ReconnectState";
        private static RemoteDevUtilitiesClient _client;

        static RemoteDevUtilitiesEditorService()
        {
            AssemblyReloadEvents.beforeAssemblyReload += Dispose;
            EditorApplication.quitting += Dispose;
        }

        public static RemoteDevUtilitiesClient Client
        {
            get
            {
                if (_client != null)
                    return _client;

                _client = new RemoteDevUtilitiesClient();
                RemoteEditorReconnectState reconnectState = TakeReconnectState();
                if (reconnectState != null)
                {
                    EditorApplication.delayCall += () =>
                    {
                        if (_client != null && !_client.HasSelectedTarget)
                            Reconnect(_client, reconnectState);
                    };
                }

                return _client;
            }
        }

        private static void Dispose()
        {
            RemoteEditorReconnectState reconnectState = _client?.CaptureReconnectState();
            SessionState.EraseString(ReconnectStateKey);
            if (reconnectState?.IsValid == true)
                SessionState.SetString(ReconnectStateKey, JsonUtility.ToJson(reconnectState));

            _client?.Dispose();
            _client = null;
        }

        private static RemoteEditorReconnectState TakeReconnectState()
        {
            string json = SessionState.GetString(ReconnectStateKey, string.Empty);
            SessionState.EraseString(ReconnectStateKey);
            if (!string.IsNullOrWhiteSpace(json))
            {
                try
                {
                    RemoteEditorReconnectState state = JsonUtility.FromJson<RemoteEditorReconnectState>(json);
                    if (state?.IsValid == true)
                        return state;
                }
                catch (ArgumentException)
                {
                }
            }

            int legacyPlayerId = SessionState.GetInt(ReconnectPlayerKey, -1);
            string legacyToken = SessionState.GetString(AccessTokenSessionKey, string.Empty);
            SessionState.EraseInt(ReconnectPlayerKey);
            SessionState.EraseString(AccessTokenSessionKey);
            if (legacyPlayerId < 0)
                return null;

            return new RemoteEditorReconnectState
            {
                Kind = RemoteEditorConnectionKind.PlayerConnection,
                PlayerId = legacyPlayerId,
                AccessToken = legacyToken
            };
        }

        private static void Reconnect(RemoteDevUtilitiesClient client, RemoteEditorReconnectState state)
        {
            switch (state.Kind)
            {
                case RemoteEditorConnectionKind.PlayerConnection:
                    client.Connect(state.PlayerId);
                    break;
                case RemoteEditorConnectionKind.DirectTcp:
                    client.ConnectTcp(state.Host, state.Port, state.AccessToken);
                    break;
            }
        }
    }
}
