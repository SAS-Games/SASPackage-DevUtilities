using SAS.Utilities.RuntimeDebugger;
using UnityEngine;

namespace SAS.Utilities.RemoteDevUtilities.DebugHost
{
    /// <summary>Renders remote debugger data in the Editor Debug Host.</summary>
    public sealed class RemoteRuntimeDebuggerHost : MonoBehaviour
    {
        private RuntimeDebuggerController _controller;
        private RuntimeDebuggerView _view;

        private void Awake()
        {
            if (RemoteDebugHostSession.RuntimeDebugger == null)
            {
                enabled = false;
                return;
            }

            RuntimeDebuggerSettings settings = RuntimeDebuggerSettings.LoadOrCreateDefaults();
            _controller = new RuntimeDebuggerController(settings, RemoteDebugHostSession.RuntimeDebugger, false);
            _controller.SetInputEnabled(true);
            _controller.SetOpen(RemoteDebugHostSession.RuntimeDebuggerPresentationVisible);
            RemoteDebugHostSession.RuntimeDebuggerPresentationVisibilityChanged += OnPresentationVisibilityChanged;
        }

        private void Update() => _controller?.Tick();

        private void OnGUI()
        {
            if (_controller == null || !_controller.IsOpen)
                return;

            _view ??= new RuntimeDebuggerView(_controller, RuntimeDebuggerSettings.LoadOrCreateDefaults());
            _view.Draw(GetInstanceID());
        }

        private void OnDestroy()
        {
            RemoteDebugHostSession.RuntimeDebuggerPresentationVisibilityChanged -= OnPresentationVisibilityChanged;
            _view?.Dispose();
            _view = null;
            _controller?.Dispose();
            _controller = null;
        }

        private void OnPresentationVisibilityChanged(bool visible)
        {
            _controller?.SetOpen(visible);
        }
    }
}
