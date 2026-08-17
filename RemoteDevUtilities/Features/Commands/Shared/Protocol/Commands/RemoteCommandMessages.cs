using System;

namespace SAS.Utilities.RemoteDevUtilities.Protocol.Commands
{
    [Serializable]
    public sealed class RemoteCommandCatalogRequest
    {
    }

    [Serializable]
    public sealed class RemoteCommandCatalogResponse
    {
        public bool Available;
        public string Prefix;
        public string Error;
        public RemoteCommandDescriptor[] Commands = Array.Empty<RemoteCommandDescriptor>();
    }

    [Serializable]
    public sealed class RemoteCommandDescriptor
    {
        public string Name;
        public string HelpText;
        public string[] Presets = Array.Empty<string>();
        public bool CloseOnCompletion;
    }

    [Serializable]
    public sealed class RemoteCommandExecuteRequest
    {
        public string CommandLine;
    }

    [Serializable]
    public sealed class RemoteCommandExecuteResponse
    {
        public bool Success;
        public bool CloseRequested;
        public string Message;
    }
}
