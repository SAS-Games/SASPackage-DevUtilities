using SAS.Utilities.RemoteDevUtilities.Editor.Commands.Presentation;
using SAS.Utilities.RemoteDevUtilities.Editor.Client;
using SAS.Utilities.RemoteDevUtilities.Editor.MiniTools;
using SAS.Utilities.RemoteDevUtilities.Editor.MiniTools.Configuration;
using SAS.Utilities.RemoteDevUtilities.Protocol.Commands;
using SAS.Utilities.RemoteDevUtilities.Protocol.MiniTools;

namespace SAS.Utilities.RemoteDevUtilities.Editor.Commands.Presentation
{
    [RemoteCommandPresentationHandler(300)]
    internal sealed class RemoteMiniToolCommandPresentationHandler : IRemoteCommandPresentationHandler
    {
        public bool TryExecute(
            RemoteDevUtilitiesClient client,
            string commandName,
            string[] arguments,
            out RemoteCommandPresentationResult result)
        {
            result = default;
            if (!client.TryGetFeature(out RemoteMiniToolClient miniTools) ||
                !TryResolveBinding(miniTools, commandName, out RemoteCommandPresentationBinding binding) ||
                binding.Routing == RemoteCommandRouting.ExecuteInBuildOnly)
                return false;

            if (!client.IsConnected)
            {
                result = RemoteCommandPresentationResult.Local(
                    false, "Connect to a runtime Player before executing this command.");
                return true;
            }

            if (!binding.TryResolveVisibility(arguments, out bool visible))
            {
                result = RemoteCommandPresentationResult.Local(
                    false, $"'{binding.CommandName}' expects On or Off as its first argument.");
                return true;
            }

            if (!miniTools.TryGetTool(binding.MiniToolId, out RemoteMiniToolDescriptor descriptor))
            {
                result = RemoteCommandPresentationResult.Local(
                    false, $"The connected Player does not provide mini-tool '{binding.MiniToolId}'.");
                return true;
            }

            ApplyPresentation(miniTools, binding, descriptor, visible);
            if (binding.Routing == RemoteCommandRouting.ExecuteInBuildAndControlEditorTool)
            {
                result = RemoteCommandPresentationResult.Remote();
                return true;
            }

            string action = visible ? "Started" : "Stopped";
            string displayName = string.IsNullOrWhiteSpace(descriptor.DisplayName)
                ? binding.MiniToolId
                : descriptor.DisplayName;
            result = RemoteCommandPresentationResult.Local(
                true, $"{action} Editor presentation for '{displayName}'.");
            return true;
        }

        private static bool TryResolveBinding(
            RemoteMiniToolClient miniTools,
            string commandName,
            out RemoteCommandPresentationBinding binding)
        {
            if (RemoteCommandPresentationRegistry.TryGetAdvancedRegistration(commandName, out binding))
                return true;
            if (RemoteCommandPresentationRegistry.TryGetProjectOverride(commandName, out binding))
                return true;
            if (RemoteMiniToolCommandManifestResolver.TryFindBinding(
                    miniTools.Tools, commandName, out RemoteCommandPresentationBinding targetBinding) &&
                !RemoteCommandPresentationRegistry.HasProjectOverrideForMiniTool(targetBinding.MiniToolId))
            {
                binding = targetBinding;
                return true;
            }
            return RemoteCommandPresentationRegistry.TryGetDefinitionBinding(commandName, out binding);
        }

        private static void ApplyPresentation(
            RemoteMiniToolClient miniTools,
            RemoteCommandPresentationBinding binding,
            RemoteMiniToolDescriptor descriptor,
            bool visible)
        {
            RemoteMiniToolVisibilitySettings settings = RemoteMiniToolVisibilitySettings.instance;
            settings.RegisterCatalog(miniTools.Tools);
            if (visible)
                settings.SetVisible(binding.MiniToolId, true);

            RemoteMiniToolDataChannels dataChannels = GetDebugHostDataChannels(descriptor);
            miniTools.SetSubscription(
                binding.MiniToolId,
                RemoteMiniToolSubscriptionOwner.DebugHost,
                visible,
                descriptor.DefaultIntervalSeconds,
                dataChannels);
        }

        private static RemoteMiniToolDataChannels GetDebugHostDataChannels(RemoteMiniToolDescriptor descriptor)
        {
            RemoteMiniToolDataChannels channels = RemoteMiniToolDataChannels.None;
            if ((descriptor.Capabilities & RemoteMiniToolCapabilities.TypedDebugHostSnapshot) != 0)
                channels |= RemoteMiniToolDataChannels.TypedSnapshot;
            else if ((descriptor.Capabilities & RemoteMiniToolCapabilities.NativeWorkspaceFields) != 0)
                channels |= RemoteMiniToolDataChannels.NativeWorkspaceFields;
            if ((descriptor.Capabilities & RemoteMiniToolCapabilities.EventStream) != 0)
                channels |= RemoteMiniToolDataChannels.EventStream;
            return channels;
        }
    }
}
