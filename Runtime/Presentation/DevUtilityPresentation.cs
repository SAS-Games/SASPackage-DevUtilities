using UnityEngine;

namespace HP.Utilities.Presentation
{
    [AddComponentMenu("Dev Utilities/Presentation")]
    [DisallowMultipleComponent]
    public sealed class DevUtilityPresentation : MonoBehaviour, IDevUtilityPresentation
    {
        [SerializeField] private GameObject m_PresentationRoot;

        private bool _initialized;
        private bool _registered;
        private bool _requestedVisible;
        private bool _suppressed;

        public bool RequestedVisible => _requestedVisible;
        public bool IsSuppressed => _suppressed;

        private GameObject PresentationRoot => m_PresentationRoot != null ? m_PresentationRoot : gameObject;

        private void Awake()
        {
            EnsureInitialized();
        }

        private void OnEnable()
        {
            EnsureInitialized();
        }

        private void OnDestroy()
        {
            if (_registered)
                DevUtilityPresentationRegistry.Unregister(this);
        }

        /// <summary>
        /// Records whether the owning tool wants its UI displayed. The actual
        /// GameObject remains hidden while a presentation policy suppresses
        /// local debug UI.
        /// </summary>
        public void SetRequestedVisible(bool visible)
        {
            EnsureInitialized();
            _requestedVisible = visible;
            ApplyVisibility();
        }

        public void SetSuppressed(bool suppressed)
        {
            EnsureStateInitialized();
            if (_suppressed == suppressed)
                return;

            _suppressed = suppressed;
            ApplyVisibility();
        }

        private void EnsureInitialized()
        {
            EnsureStateInitialized();
            _registered = true;
            DevUtilityPresentationRegistry.Register(this);
        }

        private void EnsureStateInitialized()
        {
            if (_initialized)
                return;

            GameObject root = PresentationRoot;
            _requestedVisible = root != null && root.activeSelf;
            _initialized = true;
        }

        private void ApplyVisibility()
        {
            GameObject root = PresentationRoot;
            if (root == null)
                return;

            bool visible = _requestedVisible && !_suppressed;
            if (root.activeSelf != visible)
                root.SetActive(visible);
        }
    }
}
