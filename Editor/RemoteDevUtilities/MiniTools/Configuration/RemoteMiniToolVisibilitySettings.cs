using System;
using System.Collections.Generic;
using SAS.Utilities.RemoteDevUtilities.Editor.Configuration;
using SAS.Utilities.RemoteDevUtilities.Protocol.MiniTools;

namespace SAS.Utilities.RemoteDevUtilities.Editor.MiniTools.Configuration
{
    /// <summary>
    /// Focused facade for the visibility section of the unified mini-tool
    /// project settings.
    /// </summary>
    internal sealed class RemoteMiniToolVisibilitySettings
    {
        private static readonly RemoteMiniToolVisibilitySettings
            SharedInstance = new();

        internal static RemoteMiniToolVisibilitySettings instance =>
            SharedInstance;

        internal static event Action Changed;

        internal RemoteMiniToolVisibilityConfiguration Configuration =>
            RemoteDevUtilitiesProjectSettings.instance.Visibility;

        internal bool IsVisible(string toolId) =>
            Configuration.IsVisible(toolId);

        internal void RegisterCatalog(
            IEnumerable<RemoteMiniToolDescriptor> descriptors)
        {
            if (Configuration.RegisterCatalog(descriptors))
                Persist(false);
        }

        internal void Forget(string toolId)
        {
            if (Configuration.Forget(toolId))
                Persist(true);
        }

        internal void SetVisible(string toolId, bool visible)
        {
            if (Configuration.SetVisible(toolId, visible))
                Persist(true);
        }

        internal void SetShowNewToolsByDefault(bool show)
        {
            if (Configuration.SetShowNewToolsByDefault(show))
                Persist(true);
        }

        internal void ShowAll()
        {
            if (Configuration.ShowAll())
                Persist(true);
        }

        internal void HideAll()
        {
            if (Configuration.HideAll())
                Persist(true);
        }

        internal void ResetOverrides()
        {
            if (Configuration.ResetOverrides())
                Persist(true);
        }

        private static void Persist(bool notify)
        {
            RemoteDevUtilitiesProjectSettings.instance.Persist();
            if (notify)
                Changed?.Invoke();
        }
    }
}
