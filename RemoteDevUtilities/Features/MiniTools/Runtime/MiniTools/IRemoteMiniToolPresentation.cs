using HP.Utilities.RemoteDevUtilities.Protocol.MiniTools;

namespace HP.Utilities.RemoteDevUtilities.MiniTools
{
    public interface IRemoteMiniToolPresentation
    {
        void ApplySample(RemoteMiniToolDescriptor descriptor, RemoteMiniToolSample sample);
    }
}
