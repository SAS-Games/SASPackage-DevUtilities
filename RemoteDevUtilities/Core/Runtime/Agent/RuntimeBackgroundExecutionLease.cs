using System;
using UnityEngine;

namespace HP.Utilities.RemoteDevUtilities.Agent
{
    internal sealed class RuntimeBackgroundExecutionLease : IDisposable
    {
        private bool _acquired;
        private bool _previousValue;

        public void Acquire(bool enabled)
        {
            if (!enabled || _acquired)
                return;

            _previousValue = Application.runInBackground;
            Application.runInBackground = true;
            _acquired = true;
        }

        public void Dispose()
        {
            if (!_acquired)
                return;

            Application.runInBackground = _previousValue;
            _acquired = false;
        }
    }
}
