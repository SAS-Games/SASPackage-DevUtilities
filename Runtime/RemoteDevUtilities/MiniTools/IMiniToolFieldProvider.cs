using SAS.Utilities.RemoteDevUtilities.Protocol.MiniTools;
using UnityEngine.Scripting;

namespace SAS.Utilities.RemoteDevUtilities.MiniTools
{
    /// <summary>
    /// Optional provider capability for data rendered by Native Workspace.
    /// A mini-tool that is presented only through a typed Debug Host view does
    /// not need to implement this interface.
    /// </summary>
    [RequireImplementors]
    public interface IMiniToolFieldProvider
    {
        RemoteMiniToolField[] CaptureFields();
    }

    /// <summary>
    /// Convenience base for field-only Native Workspace providers.
    /// </summary>
    public abstract class MiniToolFieldDataProvider : MiniToolDataProvider, IMiniToolFieldProvider
    {
        public abstract RemoteMiniToolField[] CaptureFields();
    }
}
