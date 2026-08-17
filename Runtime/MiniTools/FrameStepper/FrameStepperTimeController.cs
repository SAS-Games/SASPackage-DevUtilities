using UnityEngine;

namespace HP.DevUtilities
{
    /// <summary>
    /// Owns FrameStepper's target-side time state. Local and remote adapters
    /// use the same implementation so pause, resume, step, and cleanup behave
    /// consistently.
    /// </summary>
    internal sealed class FrameStepperTimeController
    {
        private const float DefaultRunningTimeScale = 1f;

        private float _runningTimeScale = DefaultRunningTimeScale;
        private bool _ownsPause;
        private FrameStepperStepRunner _stepRunner;

        internal void Begin()
        {
            CancelStep();
            _ownsPause = false;
            if (Time.timeScale > 0f)
                _runningTimeScale = Time.timeScale;
        }

        internal void Tick()
        {
            if (!_ownsPause && Time.timeScale > 0f)
            {
                _runningTimeScale = Time.timeScale;
            }
        }

        internal void Pause()
        {
            CancelStep();
            if (Time.timeScale > 0f)
                _runningTimeScale = Time.timeScale;

            Time.timeScale = 0f;
            _ownsPause = true;
        }

        internal void Resume()
        {
            CancelStep();
            if (Time.timeScale <= 0f || _ownsPause)
                Time.timeScale = ValidRunningTimeScale();
            else
                _runningTimeScale = Time.timeScale;

            _ownsPause = false;
        }

        internal bool TryStep()
        {
            if (Time.timeScale > 0f || _stepRunner != null)
                return false;

            _ownsPause = true;
            Time.timeScale = ValidRunningTimeScale();
            _stepRunner = FrameStepperStepRunner.Begin(CompleteStep);
            return true;
        }

        internal void Release()
        {
            CancelStep();
            if (_ownsPause && Time.timeScale <= 0f)
                Time.timeScale = ValidRunningTimeScale();

            _ownsPause = false;
        }

        private void CompleteStep()
        {
            _stepRunner = null;
            Time.timeScale = 0f;
        }

        private void CancelStep()
        {
            if (_stepRunner == null)
                return;

            FrameStepperStepRunner runner = _stepRunner;
            _stepRunner = null;
            runner.Cancel();
        }

        private float ValidRunningTimeScale()
        {
            return _runningTimeScale > 0f ? _runningTimeScale : DefaultRunningTimeScale;
        }
    }
}
