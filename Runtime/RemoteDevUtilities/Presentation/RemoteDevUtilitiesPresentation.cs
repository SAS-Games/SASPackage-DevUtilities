using System;
using SAS.Utilities.Presentation;
using UnityEngine;

namespace SAS.Utilities.RemoteDevUtilities.Presentation
{
    /// <summary>
    /// Translates Remote Dev Utilities session policy into a generic core
    /// presentation suppression source.
    /// </summary>
    public static class RemoteDevUtilitiesPresentation
    {
        private const string SuppressionSource = "RemoteDevUtilities.LocalPresentation";
        private static BuildDebugUiVisibility _buildUiVisibility = BuildDebugUiVisibility.ShowWhenEnabled;

        public static event Action StateChanged;

        public static bool IsRemoteSessionActive { get; private set; }
        public static BuildDebugUiVisibility BuildUiVisibility =>
            _buildUiVisibility;

        public static bool ShouldAllowBuildDebugUi
        {
            get
            {
                switch (_buildUiVisibility)
                {
                    case BuildDebugUiVisibility.ShowWhenEnabled:
                        return true;
                    case BuildDebugUiVisibility.AlwaysHidden:
                        return false;
                    case BuildDebugUiVisibility.HiddenWhileEditorConnected:
                        return !IsRemoteSessionActive;
                    default:
                        return true;
                }
            }
        }

        internal static void Configure(BuildDebugUiVisibility visibility)
        {
            BuildDebugUiVisibility normalized =
                RemoteDevUtilitiesRuntimeSettings.NormalizeBuildUiVisibility(
                    visibility);
            bool changed = _buildUiVisibility != normalized;
            _buildUiVisibility = normalized;
            ApplySuppressionPolicy();
            if (changed)
                StateChanged?.Invoke();
        }

        internal static void SetRemoteSessionActive(bool active)
        {
            bool changed = IsRemoteSessionActive != active;
            IsRemoteSessionActive = active;
            ApplySuppressionPolicy();
            if (changed)
                StateChanged?.Invoke();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetRuntimeState()
        {
            _buildUiVisibility =
                BuildDebugUiVisibility.ShowWhenEnabled;
            IsRemoteSessionActive = false;
            StateChanged = null;
        }

        private static void ApplySuppressionPolicy()
        {
            DevUtilityPresentationRegistry.SetSuppressed(
                SuppressionSource,
                !ShouldAllowBuildDebugUi);
        }
    }
}
