using System;
using UnityEditor;
using UnityEngine;

namespace HP.Utilities.RemoteDevUtilities.Editor.DebugHost
{
    /// <summary>
    /// Per-user Editor workflow preferences for the Debug Host. The asset is
    /// stored under UserSettings and is never included in a Player build.
    /// </summary>
    [FilePath("UserSettings/RemoteDevUtilitiesDebugHostSettings.asset", FilePathAttribute.Location.ProjectFolder)]
    internal sealed class RemoteDebugHostSettings : ScriptableSingleton<RemoteDebugHostSettings>
    {
        [SerializeField] private bool _includeDeveloperConsoleUi;
        [SerializeField] private bool _launchDebugHostOnPlayerConnect;

        internal bool IncludeDeveloperConsoleUi => _includeDeveloperConsoleUi;
        internal bool LaunchDebugHostOnPlayerConnect => _launchDebugHostOnPlayerConnect;
        internal static event Action Changed;

        internal void SetIncludeDeveloperConsoleUi(bool value)
        {
            if (_includeDeveloperConsoleUi == value)
                return;

            _includeDeveloperConsoleUi = value;
            Save(true);
            Changed?.Invoke();
        }

        internal void SetLaunchDebugHostOnPlayerConnect(bool value)
        {
            if (_launchDebugHostOnPlayerConnect == value)
                return;

            _launchDebugHostOnPlayerConnect = value;
            Save(true);
            Changed?.Invoke();
        }
    }
}
