using SAS.Utilities.RuntimeDebugger.Core;
using UnityEngine;

namespace SAS.Utilities.RuntimeDebugger
{
    [RuntimeDebuggerProtected]
    public sealed class RuntimeDebuggerHost : MonoBehaviour
    {
        public static RuntimeDebuggerHost Instance { get; private set; }
        public bool IsOpen => _controller?.IsOpen ?? false;
        public bool IsDebuggerEnabled => enabled && _controller != null;
        public bool ConsumesGameplayInput => _controller?.ConsumesGameplayInput ?? false;

        private RuntimeDebuggerSettings _settings;
        private RuntimeDebuggerController _controller;
        private RuntimeDebuggerView _view;

        internal void Initialize(RuntimeDebuggerSettings settings) => _settings = settings;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);
            _settings ??= RuntimeDebuggerSettings.LoadOrCreateDefaults();
            StartSubsystem();
        }

        private void OnDestroy()
        {
            StopSubsystem();
            if (Instance == this)
                Instance = null;
        }

        private void OnEnable() => _controller?.SetInputEnabled(true);
        private void OnDisable() => _controller?.SetInputEnabled(false);
        private void Update() => _controller?.Tick();

        private void OnGUI()
        {
            if (_controller == null || !_controller.IsOpen)
                return;

            _view ??= new RuntimeDebuggerView(_controller, _settings);
            _view.Draw(GetInstanceID());
        }

        public void SetDebuggerEnabled(bool value)
        {
            if (value == IsDebuggerEnabled)
                return;

            if (!value)
            {
                StopSubsystem();
                enabled = false;
                return;
            }

            enabled = true;
            StartSubsystem();
        }

        public void SetOverlayVisible(bool visible)
        {
            if (visible && !IsDebuggerEnabled)
                SetDebuggerEnabled(true);

            _controller?.SetOpen(visible && IsDebuggerEnabled);
        }

        public static RuntimeDebuggerHost GetOrCreateEnabledHost()
        {
            if (Instance != null)
            {
                Instance.SetDebuggerEnabled(true);
                return Instance;
            }

            RuntimeDebuggerSettings settings = RuntimeDebuggerSettings.LoadOrCreateDefaults();
            if (!settings.EnableDebugger)
                return null;

            var hostObject = new GameObject("[Runtime Debugger]") { hideFlags = HideFlags.DontSave };
            DontDestroyOnLoad(hostObject);
            var host = hostObject.AddComponent<RuntimeDebuggerHost>();
            host.Initialize(settings);
            return host;
        }

        private void StartSubsystem()
        {
            if (_controller != null)
                return;

            _controller = new RuntimeDebuggerController(_settings);
        }

        private void StopSubsystem()
        {
            _view?.Dispose();
            _view = null;
            _controller?.Dispose();
            _controller = null;
        }
    }
}
