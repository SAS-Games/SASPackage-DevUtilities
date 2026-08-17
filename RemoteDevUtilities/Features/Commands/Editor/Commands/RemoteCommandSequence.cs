using System;
using System.Collections.Generic;
using UnityEngine;

namespace HP.Utilities.RemoteDevUtilities.Editor.Commands.Sequences
{
    public enum RemoteCommandUnavailablePolicy
    {
        FailImmediately,
        WaitUntilAvailable
    }

    [Serializable]
    public sealed class RemoteCommandSequenceStep
    {
        [SerializeField] private bool m_Enabled = true;
        [SerializeField] private string m_CommandLine = string.Empty;
        [SerializeField] private RemoteCommandUnavailablePolicy m_WhenUnavailable;
        [SerializeField, Min(0.1f)] private float m_WaitTimeoutSeconds = 5f;

        public bool Enabled => m_Enabled;
        public string CommandLine => m_CommandLine ?? string.Empty;
        public RemoteCommandUnavailablePolicy WhenUnavailable => m_WhenUnavailable;
        public float WaitTimeoutSeconds => Mathf.Max(0.1f, m_WaitTimeoutSeconds);

        internal RemoteCommandSequenceStep()
        {
        }

        internal RemoteCommandSequenceStep(
            string commandLine,
            bool enabled = true,
            RemoteCommandUnavailablePolicy whenUnavailable = RemoteCommandUnavailablePolicy.FailImmediately,
            float waitTimeoutSeconds = 5f)
        {
            m_Enabled = enabled;
            m_CommandLine = commandLine ?? string.Empty;
            m_WhenUnavailable = whenUnavailable;
            m_WaitTimeoutSeconds = Mathf.Max(0.1f, waitTimeoutSeconds);
        }
    }

    /// <summary>
    /// Editor-owned, ordered set of commands that can be executed against any
    /// connected Player exposing the required runtime command catalog.
    /// </summary>
    [CreateAssetMenu(fileName = "Remote Command Sequence", menuName = "HP/Dev Utilities/Remote Command Sequence")]
    public sealed class RemoteCommandSequence : ScriptableObject
    {
        [SerializeField, TextArea(2, 4)] private string m_Description = string.Empty;
        [SerializeField] private bool m_StopOnFailure = true;
        [SerializeField] private List<RemoteCommandSequenceStep> m_Steps = new();

        public string Description => m_Description ?? string.Empty;
        public bool StopOnFailure => m_StopOnFailure;
        public IReadOnlyList<RemoteCommandSequenceStep> Steps => m_Steps ??= new List<RemoteCommandSequenceStep>();

        internal void Configure(bool stopOnFailure, params RemoteCommandSequenceStep[] steps)
        {
            m_StopOnFailure = stopOnFailure;
            m_Steps = steps == null
                ? new List<RemoteCommandSequenceStep>()
                : new List<RemoteCommandSequenceStep>(steps);
        }
    }
}
