using SAS.Utilities.Presentation;
using SAS.Utilities.RuntimeSceneInspector.Core;
using UnityEngine;

namespace SAS.Utilities.RuntimeSceneInspector
{
    [RuntimeSceneInspectorProtected]
    public sealed class RuntimeSceneInspectorHost : MonoBehaviour
    {
        public static RuntimeSceneInspectorHost Instance { get; private set; }
        public bool IsOpen => _controller?.IsOpen ?? false;
        public bool IsInspectorEnabled => enabled && _controller != null;
        public bool ConsumesGameplayInput => _controller?.ConsumesGameplayInput ?? false;

        private RuntimeSceneInspectorSettings _settings;
        private RuntimeSceneInspectorController _controller;
        private RuntimeSceneInspectorView _view;
        private bool _overlayRequested;

        internal void Initialize(RuntimeSceneInspectorSettings settings) => _settings = settings;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);
            _settings ??= RuntimeSceneInspectorSettings.LoadOrCreateDefaults();
            StartSubsystem();
            ApplyPresentationState();
        }

        private void OnDestroy()
        {
            StopSubsystem();
            if (Instance == this)
                Instance = null;
        }

        private void OnEnable()
        {
            DevUtilityPresentationRegistry.SuppressionChanged -= ApplyPresentationState;
            DevUtilityPresentationRegistry.SuppressionChanged += ApplyPresentationState;
            _controller?.SetInputEnabled(true);
            ApplyPresentationState();
        }

        private void OnDisable()
        {
            DevUtilityPresentationRegistry.SuppressionChanged -= ApplyPresentationState;
            _controller?.SetInputEnabled(false);
        }

        private void Update() => _controller?.Tick();

        private void OnGUI()
        {
            if (_controller == null || !_controller.IsOpen)
                return;

            _view ??= new RuntimeSceneInspectorView(_controller, _settings);
            _view.Draw(GetInstanceID());
        }

        public void SetInspectorEnabled(bool value)
        {
            if (value == IsInspectorEnabled)
                return;

            if (!value)
            {
                _overlayRequested = false;
                StopSubsystem();
                enabled = false;
                return;
            }

            enabled = true;
            StartSubsystem();
        }

        public void SetOverlayVisible(bool visible)
        {
            if (visible && !IsInspectorEnabled)
                SetInspectorEnabled(true);

            _overlayRequested = visible;
            ApplyPresentationState();
        }

        public static RuntimeSceneInspectorHost GetOrCreateEnabledHost()
        {
            if (Instance != null)
            {
                Instance.SetInspectorEnabled(true);
                return Instance;
            }

            RuntimeSceneInspectorSettings settings = RuntimeSceneInspectorSettings.LoadOrCreateDefaults();
            if (!settings.EnableInspector)
                return null;

            var hostObject = new GameObject("[Runtime Scene Inspector]") { hideFlags = HideFlags.DontSave };
            DontDestroyOnLoad(hostObject);
            var host = hostObject.AddComponent<RuntimeSceneInspectorHost>();
            host.Initialize(settings);
            return host;
        }

        private void StartSubsystem()
        {
            if (_controller != null)
                return;

            _controller = new RuntimeSceneInspectorController(_settings);
        }

        private void StopSubsystem()
        {
            _view?.Dispose();
            _view = null;
            _controller?.Dispose();
            _controller = null;
        }

        private void ApplyPresentationState()
        {
            _controller?.SetOpen(_overlayRequested && IsInspectorEnabled && DevUtilityPresentationRegistry.CanShowLocalUi);
        }
    }
}
