using System;
using System.Collections.Generic;
using UnityEngine;

namespace HP.Utilities.Presentation
{
    /// <summary>
    /// Coordinates presentation suppression without knowing why a presentation
    /// is being suppressed. Optional modules depend on this registry, never the
    /// other way around.
    /// </summary>
    public static class DevUtilityPresentationRegistry
    {
        private static readonly HashSet<IDevUtilityPresentation> Presentations = new();
        private static readonly HashSet<string> SuppressionSources = new(StringComparer.Ordinal);

        public static event Action SuppressionChanged;

        public static bool IsSuppressed => SuppressionSources.Count > 0;
        public static bool CanShowLocalUi => !IsSuppressed;

        public static void Register(IDevUtilityPresentation presentation)
        {
            if (IsMissing(presentation) || !Presentations.Add(presentation))
                return;

            ApplySuppression(presentation, IsSuppressed);
        }

        public static void Unregister(IDevUtilityPresentation presentation)
        {
            if (presentation != null)
                Presentations.Remove(presentation);
        }

        /// <summary>
        /// Adds or removes an independent suppression source. Local UI is
        /// restored only after every source has released its suppression.
        /// </summary>
        public static void SetSuppressed(string sourceId, bool suppressed)
        {
            if (string.IsNullOrWhiteSpace(sourceId))
                throw new ArgumentException("A suppression source identifier is required.", nameof(sourceId));

            bool wasSuppressed = IsSuppressed;
            bool changed = suppressed ? SuppressionSources.Add(sourceId) : SuppressionSources.Remove(sourceId);
            if (!changed || wasSuppressed == IsSuppressed)
                return;

            ApplySuppressionToAll(IsSuppressed);
            SuppressionChanged?.Invoke();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetRuntimeState()
        {
            Presentations.Clear();
            SuppressionSources.Clear();
            SuppressionChanged = null;
        }

        private static void ApplySuppressionToAll(bool suppressed)
        {
            var snapshot = new List<IDevUtilityPresentation>(Presentations);
            for (int i = 0; i < snapshot.Count; i++)
            {
                IDevUtilityPresentation presentation = snapshot[i];
                if (IsMissing(presentation))
                {
                    Presentations.Remove(presentation);
                    continue;
                }

                ApplySuppression(presentation, suppressed);
            }
        }

        private static void ApplySuppression(IDevUtilityPresentation presentation, bool suppressed)
        {
            try
            {
                presentation.SetSuppressed(suppressed);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }
        }

        private static bool IsMissing(IDevUtilityPresentation presentation)
        {
            if (presentation == null)
                return true;

            return presentation is UnityEngine.Object unityObject && unityObject == null;
        }
    }
}
