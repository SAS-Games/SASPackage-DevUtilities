using System;
using System.IO;
using SAS.Utilities.RemoteDevUtilities.Protocol;

namespace SAS.Utilities.RemoteDevUtilities.Transport.Tcp
{
    internal static class RemoteTcpFrameProtocol
    {
        private const int HeaderSize = 4;

        public static void WriteFrame(Stream stream, byte[] payload)
        {
            if (stream == null)
                throw new ArgumentNullException(nameof(stream));
            if (payload == null || payload.Length == 0)
                throw new ArgumentException("A non-empty payload is required.", nameof(payload));
            if (payload.Length > RemoteProtocolConstants.MaximumMessageBytes)
            {
                throw new InvalidDataException($"The remote message exceeded {RemoteProtocolConstants.MaximumMessageBytes} bytes.");
            }

            int length = payload.Length;
            var header = new[]
            {
                (byte)(length >> 24),
                (byte)(length >> 16),
                (byte)(length >> 8),
                (byte)length
            };

            stream.Write(header, 0, HeaderSize);
            stream.Write(payload, 0, payload.Length);
            stream.Flush();
        }

        public static byte[] ReadFrame(Stream stream)
        {
            if (stream == null)
                throw new ArgumentNullException(nameof(stream));

            var header = new byte[HeaderSize];
            if (!TryReadExactly(stream, header, HeaderSize, allowCleanEndOfStream: true))
                return null;

            int length = (header[0] << 24) | (header[1] << 16) | (header[2] << 8) | header[3];
            if (length <= 0 || length > RemoteProtocolConstants.MaximumMessageBytes)
            {
                throw new InvalidDataException($"The remote frame length '{length}' is invalid.");
            }

            var payload = new byte[length];
            TryReadExactly(stream, payload, length, allowCleanEndOfStream: false);
            return payload;
        }

        private static bool TryReadExactly(Stream stream, byte[] buffer, int count, bool allowCleanEndOfStream)
        {
            int offset = 0;
            while (offset < count)
            {
                int read = stream.Read(buffer, offset, count - offset);
                if (read > 0)
                {
                    offset += read;
                    continue;
                }

                if (allowCleanEndOfStream && offset == 0)
                    return false;

                throw new EndOfStreamException("The remote TCP frame ended unexpectedly.");
            }

            return true;
        }
    }
}
