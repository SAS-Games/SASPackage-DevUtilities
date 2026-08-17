using UnityEngine;

namespace HP.Utilities.RemoteDevUtilities.Protocol.Commands
{
    /// <summary>
    /// Controls how a command is routed between the game build and its Editor tool.
    /// </summary>
    public enum RemoteCommandRouting
    {
        /// <summary>Execute only the existing command inside the connected game build.</summary>
        [InspectorName("Execute in Build Only")]
        ExecuteInBuildOnly = 0,

        /// <summary>
        /// Control only the Editor mini-tool. The command is not executed inside the build.
        /// </summary>
        [InspectorName("Control Editor Tool Only")]
        ControlEditorToolOnly = 1,

        /// <summary>
        /// Execute the command inside the build and control the corresponding Editor mini-tool.
        /// </summary>
        [InspectorName("Execute in Build and Control Editor Tool")]
        ExecuteInBuildAndControlEditorTool = 2
    }
}
