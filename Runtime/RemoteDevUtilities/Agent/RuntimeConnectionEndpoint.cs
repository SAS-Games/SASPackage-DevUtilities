using System;
using System.Collections.Generic;
using SAS.Utilities.RemoteDevUtilities.Protocol;
using SAS.Utilities.RemoteDevUtilities.Protocol.Connection;
using SAS.Utilities.RemoteDevUtilities.Protocol.Serialization;
using UnityEngine;

namespace SAS.Utilities.RemoteDevUtilities.Agent
{
    internal sealed class RuntimeConnectionEndpoint : IRuntimeRemoteEndpoint
    {
        private static readonly string[] SupportedMessages =
        {
            RemoteMessageTypes.HandshakeRequest,
            RemoteMessageTypes.SessionEndRequest,
            RemoteMessageTypes.PingRequest
        };

        private RuntimeRemoteEndpointContext _context;

        public event Action<bool> SessionStateChanged;
        public IEnumerable<string> MessageTypes => SupportedMessages;
        public bool IsSessionAccepted { get; private set; }
        public string AcceptedEditorSessionId { get; private set; }

        public void Initialize(RuntimeRemoteEndpointContext context) => _context = context;

        public void Handle(RemoteEnvelope envelope)
        {
            switch (envelope.MessageType)
            {
                case RemoteMessageTypes.HandshakeRequest:
                    HandleHandshake(envelope);
                    break;
                case RemoteMessageTypes.PingRequest:
                    HandlePing(envelope);
                    break;
                case RemoteMessageTypes.SessionEndRequest:
                    IsSessionAccepted = false;
                    AcceptedEditorSessionId = null;
                    _context.Sender.Send(RemoteMessageTypes.SessionEndResponse, envelope.RequestId, new RemoteSessionEndResponse { Ended = true });
                    SessionStateChanged?.Invoke(false);
                    break;
            }
        }

        public void Tick()
        {
        }

        public void Dispose()
        {
            SessionStateChanged = null;
            _context = null;
        }

        public void NotifyDisconnected()
        {
            IsSessionAccepted = false;
            AcceptedEditorSessionId = null;
            SessionStateChanged?.Invoke(false);
        }

        private void HandleHandshake(RemoteEnvelope envelope)
        {
            if (!RemoteProtocolSerializer.TryDeserializePayload(envelope, out RemoteHandshakeRequest request, out string error))
            {
                SendRejected(envelope.RequestId, error);
                return;
            }

            if (request.ProtocolVersion != RemoteProtocolConstants.Version || envelope.ProtocolVersion != RemoteProtocolConstants.Version)
            {
                SendRejected(envelope.RequestId, $"Protocol mismatch. Runtime={RemoteProtocolConstants.Version}, " + $"Editor={request.ProtocolVersion}.");
                return;
            }

            if (string.IsNullOrWhiteSpace(request.EditorSessionId) || !string.Equals(request.EditorSessionId, envelope.SessionId, StringComparison.Ordinal))
            {
                SendRejected(envelope.RequestId, "The Editor session identifier was missing or inconsistent.");
                return;
            }

            if (_context.Sender.RequiresAccessToken && !AccessTokenMatches(_context.Settings.TcpAccessToken, request.AccessToken))
            {
                SendRejected(envelope.RequestId, "The Remote Dev Utilities access token was rejected.");
                return;
            }

            IsSessionAccepted = true;
            AcceptedEditorSessionId = request.EditorSessionId;
            _context.Sender.Send(RemoteMessageTypes.HandshakeResponse, envelope.RequestId, new RemoteHandshakeResponse
            {
                Accepted = true,
                ProtocolVersion = RemoteProtocolConstants.Version,
                PackageVersion = RemoteProtocolConstants.PackageVersion,
                RuntimeSessionId = _context.RuntimeSessionId,
                Target = new RemoteTargetDescriptor
                {
                    ProductName = Application.productName,
                    ApplicationVersion = Application.version,
                    UnityVersion = Application.unityVersion,
                    Platform = Application.platform.ToString(),
                    DeviceName = SystemInfo.deviceName,
                    IsDebugBuild = UnityEngine.Debug.isDebugBuild,
                    IsDevUtilitiesEnabled = IsDevUtilitiesEnabled()
                }
            });
            SessionStateChanged?.Invoke(true);
        }

        private void HandlePing(RemoteEnvelope envelope)
        {
            if (!RemoteProtocolSerializer.TryDeserializePayload(envelope, out RemotePingRequest request, out _))
                return;

            _context.Sender.Send(RemoteMessageTypes.PingResponse, envelope.RequestId, new RemotePingResponse
            {
                EditorTimestamp = request.EditorTimestamp,
                RuntimeTimestamp = Time.realtimeSinceStartupAsDouble,
                RuntimeFrame = Time.frameCount
            });
        }

        private void SendRejected(long requestId, string error)
        {
            _context.Sender.Send(RemoteMessageTypes.HandshakeResponse, requestId, new RemoteHandshakeResponse
            {
                Accepted = false,
                Error = error,
                ProtocolVersion = RemoteProtocolConstants.Version,
                PackageVersion = RemoteProtocolConstants.PackageVersion,
                RuntimeSessionId = _context.RuntimeSessionId
            });
        }

        private static bool AccessTokenMatches(string expected, string received)
        {
            expected ??= string.Empty;
            received ??= string.Empty;
            if (expected.Length != received.Length)
                return false;

            int difference = 0;
            for (int i = 0; i < expected.Length; i++)
                difference |= expected[i] ^ received[i];
            return difference == 0;
        }

        private static bool IsDevUtilitiesEnabled()
        {
#if ENABLE_DEBUG
            return true;
#else
            return false;
#endif
        }
    }
}
