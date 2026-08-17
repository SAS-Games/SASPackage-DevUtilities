using System;
using System.Collections;
using UnityEngine;

namespace SAS.DevUtilities
{
    /// <summary>
    /// Runs the original FrameStepper coroutine sequence for both local and
    /// remote controllers: resume, wait one frame, then pause again.
    /// </summary>
    internal sealed class FrameStepperStepRunner : MonoBehaviour
    {
        private Action _completed;

        internal static FrameStepperStepRunner Begin(Action completed)
        {
            var host = new GameObject("[FrameStepper Step]")
            {
                hideFlags = HideFlags.HideAndDontSave
            };
            DontDestroyOnLoad(host);

            FrameStepperStepRunner runner = host.AddComponent<FrameStepperStepRunner>();
            runner._completed = completed;
            runner.StartCoroutine(runner.CompleteNextFrame());
            return runner;
        }

        internal void Cancel()
        {
            _completed = null;
            if (this != null)
                Destroy(gameObject);
        }

        private IEnumerator CompleteNextFrame()
        {
            yield return null;

            Action completed = _completed;
            _completed = null;
            completed?.Invoke();
            Destroy(gameObject);
        }

        private void OnDestroy()
        {
            _completed = null;
        }
    }
}
