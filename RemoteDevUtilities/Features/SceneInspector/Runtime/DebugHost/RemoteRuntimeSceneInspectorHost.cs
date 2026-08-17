using HP.Utilities.RuntimeSceneInspector;
using UnityEngine;

namespace HP.Utilities.RemoteDevUtilities.DebugHost
{
    /// <summary>Renders remote scene inspector data in the Editor Debug Host.</summary>
    public sealed class RemoteRuntimeSceneInspectorHost : MonoBehaviour
    {
        private RuntimeSceneInspectorController _controller;
        private RuntimeSceneInspectorView _view;

        private void Awake()
        {
            if (RemoteDebugHostSession.RuntimeSceneInspector == null)
            {
                enabled = false;
                return;
            }

            RuntimeSceneInspectorSettings settings = RuntimeSceneInspectorSettings.LoadOrCreateDefaults();
            _controller = new RuntimeSceneInspectorController(settings, RemoteDebugHostSession.RuntimeSceneInspector, false);
            _controller.SetInputEnabled(true);
            _controller.SetOpen(RemoteDebugHostSession.RuntimeSceneInspectorPresentationVisible);
            RemoteDebugHostSession.RuntimeSceneInspectorPresentationVisibilityChanged += OnPresentationVisibilityChanged;
        }

        private void Update() => _controller?.Tick();

        private void OnGUI()
        {
            if (_controller == null || !_controller.IsOpen)
                return;

            _view ??= new RuntimeSceneInspectorView(_controller, RuntimeSceneInspectorSettings.LoadOrCreateDefaults());
            _view.Draw(GetInstanceID());
        }

        private void OnDestroy()
        {
            RemoteDebugHostSession.RuntimeSceneInspectorPresentationVisibilityChanged -= OnPresentationVisibilityChanged;
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
