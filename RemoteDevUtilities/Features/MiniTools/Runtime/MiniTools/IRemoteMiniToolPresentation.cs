using SAS.Utilities.RemoteDevUtilities.Protocol.MiniTools;

namespace SAS.Utilities.RemoteDevUtilities.MiniTools
{
    public interface IRemoteMiniToolPresentation
    {
        void ApplySample(RemoteMiniToolDescriptor descriptor, RemoteMiniToolSample sample);
    }
}
