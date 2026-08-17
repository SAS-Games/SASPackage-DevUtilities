using System;
using System.Text;
using UnityEngine;

namespace HP.Utilities.RemoteDevUtilities.Protocol.Serialization
{
    public static class RemoteProtocolSerializer
    {
        public static byte[] Serialize<T>(string messageType, long requestId, string sessionId, T payload)
        {
            if (string.IsNullOrWhiteSpace(messageType))
                throw new ArgumentException("A message type is required.", nameof(messageType));

            var envelope = new RemoteEnvelope
            {
                ProtocolVersion = RemoteProtocolConstants.Version,
                MessageType = messageType,
                RequestId = requestId,
                SessionId = sessionId ?? string.Empty,
                PayloadJson = payload == null ? string.Empty : JsonUtility.ToJson(payload)
            };

            return Encoding.UTF8.GetBytes(JsonUtility.ToJson(envelope));
        }

        public static bool TryDeserializeEnvelope(byte[] data, out RemoteEnvelope envelope, out string error)
        {
            envelope = null;
            error = null;

            if (data == null || data.Length == 0)
            {
                error = "The remote message was empty.";
                return false;
            }

            if (data.Length > RemoteProtocolConstants.MaximumMessageBytes)
            {
                error = $"The remote message exceeded {RemoteProtocolConstants.MaximumMessageBytes} bytes.";
                return false;
            }

            try
            {
                envelope = JsonUtility.FromJson<RemoteEnvelope>(Encoding.UTF8.GetString(data));
                if (envelope == null || string.IsNullOrWhiteSpace(envelope.MessageType))
                {
                    envelope = null;
                    error = "The remote message envelope was invalid.";
                    return false;
                }

                return true;
            }
            catch (Exception exception)
            {
                error = exception.GetType().Name + ": " + exception.Message;
                return false;
            }
        }

        public static bool TryDeserializePayload<T>(RemoteEnvelope envelope, out T payload, out string error) where T : class
        {
            payload = null;
            error = null;

            if (envelope == null)
            {
                error = "The remote message envelope was null.";
                return false;
            }

            try
            {
                payload = string.IsNullOrEmpty(envelope.PayloadJson) ? Activator.CreateInstance<T>() : JsonUtility.FromJson<T>(envelope.PayloadJson);

                if (payload != null)
                    return true;

                error = $"The payload for '{envelope.MessageType}' was invalid.";
                return false;
            }
            catch (Exception exception)
            {
                error = exception.GetType().Name + ": " + exception.Message;
                return false;
            }
        }
    }
}
