using System.IO;
using NUnit.Framework;
using SAS.Utilities.RemoteDevUtilities.Protocol;
using SAS.Utilities.RemoteDevUtilities.Transport.Tcp;

namespace SAS.Utilities.RemoteDevUtilities.Transport.Tcp.Tests
{
    public sealed class RemoteTcpFrameProtocolTests
    {
        [Test]
        public void FrameRoundTripPreservesPayload()
        {
            byte[] expected = { 1, 2, 3, 4, 5 };
            using var stream = new MemoryStream();

            RemoteTcpFrameProtocol.WriteFrame(stream, expected);
            stream.Position = 0;

            Assert.That(RemoteTcpFrameProtocol.ReadFrame(stream), Is.EqualTo(expected));
            Assert.That(RemoteTcpFrameProtocol.ReadFrame(stream), Is.Null);
        }

        [Test]
        public void ReadRejectsInvalidLength()
        {
            using var stream = new MemoryStream(new byte[] { 0, 0, 0, 0 });

            Assert.Throws<InvalidDataException>(() => RemoteTcpFrameProtocol.ReadFrame(stream));
        }

        [Test]
        public void ReadRejectsTruncatedPayload()
        {
            using var stream = new MemoryStream(new byte[] { 0, 0, 0, 4, 1, 2 });

            Assert.Throws<EndOfStreamException>(() => RemoteTcpFrameProtocol.ReadFrame(stream));
        }

        [Test]
        public void WriteRejectsOversizedPayload()
        {
            using var stream = new MemoryStream();
            var payload = new byte[RemoteProtocolConstants.MaximumMessageBytes + 1];

            Assert.Throws<InvalidDataException>(() => RemoteTcpFrameProtocol.WriteFrame(stream, payload));
        }
    }
}
