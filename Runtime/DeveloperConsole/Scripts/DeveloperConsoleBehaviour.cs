using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using SAS.Utilities.Presentation;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using static UnityEngine.InputSystem.InputAction;

namespace SAS.Utilities.DeveloperConsole
{
    public class DeveloperConsoleBehaviour : MonoBehaviour
    {
        public enum Platform
        {
            WINDOWS,
            PS,
            IOS
        }

        [Serializable]
        public class PlatformCommand
        {
            public Platform platform;
            public ConsoleCommand[] commands;
        }

        public Action<string> InputChangedEvent;
        public event Action<string> HelpTextDisplayed;
        public event Action CommandsChanged;
        public Action<bool> SuggestionViewChangedEvent;
        public Action SuggestionAppliedEvent;

        [SerializeField] private string m_Prefix = string.Empty;
        [SerializeField] private ConsoleCommand[] m_Commands = new ConsoleCommand[0];
        [SerializeField] private PlatformCommand[] m_PlatformCommands;
        [SerializeField] private string[] m_CommandsToExecuteOnLoad;
        [Header("UI")] [SerializeField] private GameObject m_UiCanvas = null;
        [SerializeField] private TMP_InputField m_InputField = null;
        [SerializeField] private Button m_SubmitButton = null;
        [SerializeField] private TMP_Text m_HelpText = null;
        [SerializeField] private Toggle m_TreeViewSuggestionToggle;
        [SerializeField] private RectTransform m_CommandPanel = null;
        [SerializeField] private RectTransform[] m_SuggestionPanels = Array.Empty<RectTransform>();
        [SerializeField, Min(32f)] private float m_MaxHelpHeight = 260f;
        private bool PauseOnOpen => DebugSettings.PauseOnEnable;

        private float _pausedTimeScale;
        private float _compactCommandPanelHeight;
        private float _minimumHelpHeight;
        private float _helpBottomOffset;
        private float _helpTopPadding;
        private float[] _suggestionPanelGaps = Array.Empty<float>();
        private bool _helpLayoutInitialized;
        private bool _consoleVisibilityRequested;
        private DeveloperConsole _developerConsole;
        private IDeveloperConsoleCommandGateway _commandGateway;
        private ConsoleInputActions _inputActions;
        public bool IsTreeViewSuggestion => m_TreeViewSuggestionToggle.isOn;
        private GameObject _lastSelectedGameObject;
        public static DeveloperConsoleBehaviour Instance { get; private set; }

        internal DeveloperConsole DeveloperConsole
        {
            get
            {
                if (_developerConsole != null)
                    return _developerConsole;

                var allCommands = new List<ConsoleCommand>();

                if (m_Commands != null)
                {
                    foreach (var cmd in m_Commands)
                    {
                        if (cmd != null)
                            allCommands.Add(cmd);
                    }
                }

                if (m_PlatformCommands != null)
                {
                    foreach (var pc in m_PlatformCommands)
                    {
                        if (pc == null || pc.commands == null)
                            continue;

                        if (IsCurrentPlatform(pc.platform))
                        {
                            foreach (var cmd in pc.commands)
                            {
                                if (cmd != null)
                                    allCommands.Add(cmd);
                            }

                            break;
                        }
                    }
                }

                SetDeveloperConsole(new DeveloperConsole(m_Prefix, allCommands), false);
                return _developerConsole;
            }
        }

        public static bool IsCurrentPlatform(Platform platform)
        {
            switch (platform)
            {
                case Platform.WINDOWS:
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
                    return true;
#else
                    return false;
#endif
                case Platform.PS:
#if UNITY_PS4 || UNITY_PS5
                    return true;
#else
                    return false;
#endif

                case Platform.IOS:
#if UNITY_IOS
                    return true;
#else
                    return false;
#endif
                default:
                    return false;
            }
        }


        private void Awake()
        {
            if (Instance != null)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            _consoleVisibilityRequested = m_UiCanvas != null && m_UiCanvas.activeSelf;
            _pausedTimeScale = Time.timeScale;
            _inputActions = new ConsoleInputActions();
            _inputActions.Developer.ToggleConsole.performed += Toggle;
            _inputActions.Developer.Submit.performed += OnSubmit;
            _inputActions.Developer.HighlightInput.canceled += FocusInput;
            _inputActions.Developer.HistoryNavigationUp.performed += GetNextCommandHistory;
            _inputActions.Developer.HistoryNavigationDown.performed += GetPrevCommandHistory;

            if (m_InputField != null)
                m_InputField.onValueChanged.AddListener(OnInputChanged);

            InitializeHelpLayout();
            m_TreeViewSuggestionToggle.onValueChanged.AddListener(OnTreeViewSuggestion);
            OnTreeViewSuggestion(m_TreeViewSuggestionToggle.isOn);

            foreach (var commands in m_CommandsToExecuteOnLoad)
                DeveloperConsole.ProcessCommand(commands, this, out _);

            ExecuteCommandsFromCommandLine();

            OnPresentationSuppressionChanged();

            DontDestroyOnLoad(this.gameObject);
        }

        private void OnDestroy()
        {
            SetDeveloperConsole(null, false);
            if (Instance == this)
                Instance = null;
        }

        private void OnTreeViewSuggestion(bool treeView)
        {
            if (m_HelpText != null)
                m_HelpText.text = string.Empty;

            SuggestionViewChangedEvent?.Invoke(treeView);
            RefreshHelpLayout();
        }

        private void OnEnable()
        {
            DevUtilityPresentationRegistry.SuppressionChanged -= OnPresentationSuppressionChanged;
            DevUtilityPresentationRegistry.SuppressionChanged += OnPresentationSuppressionChanged;
            OnPresentationSuppressionChanged();
            if (DevUtilityPresentationRegistry.CanShowLocalUi)
                _inputActions?.Developer.Enable();
        }

        private void OnDisable()
        {
            DevUtilityPresentationRegistry.SuppressionChanged -= OnPresentationSuppressionChanged;
            _inputActions?.Developer.Disable();
        }

        private void Toggle(CallbackContext context)
        {
            if (m_UiCanvas == null)
                return;
            if (!DevUtilityPresentationRegistry.CanShowLocalUi)
                return;

            SetConsoleVisible(!_consoleVisibilityRequested);
        }

        private void ApplyConsoleVisibility()
        {
            if (m_UiCanvas == null)
                return;

            bool visible = _consoleVisibilityRequested && DevUtilityPresentationRegistry.CanShowLocalUi;
            if (m_UiCanvas.activeSelf == visible)
                return;

            if (!visible)
            {
                if (m_InputField != null)
                    Time.timeScale = _pausedTimeScale;

                EventSystem eventSystem = EventSystem.current;
                if (eventSystem != null)
                {
                    eventSystem.SetSelectedGameObject(null);
                    if (_lastSelectedGameObject != null && _lastSelectedGameObject.activeInHierarchy)
                        eventSystem.SetSelectedGameObject(_lastSelectedGameObject);
                }

                m_UiCanvas.SetActive(false);
            }
            else
            {
                if (PauseOnOpen)
                {
                    _pausedTimeScale = Time.timeScale;
                    Time.timeScale = 0;
                }

                m_UiCanvas.SetActive(true);
                EventSystem eventSystem = EventSystem.current;
                _lastSelectedGameObject = eventSystem != null ? eventSystem.currentSelectedGameObject : null;
                eventSystem?.SetSelectedGameObject(null);
#if UNITY_EDITOR || !UNITY_PS5
                StartCoroutine(FocusInputFieldNextFrame());
#endif
            }

            DisplayHelpText("");
        }

        private IEnumerator FocusInputFieldNextFrame()
        {
            EventSystem eventSystem = EventSystem.current;
            yield return null; // wait one frame

            if (m_InputField == null || !CanApplyDelayedFocus(eventSystem, m_InputField.gameObject))
                yield break;

            m_InputField.ActivateInputField();
            m_InputField.Select();
            SuggestionAppliedEvent?.Invoke();
        }

        private IEnumerator FocusSubmitNextFrame()
        {
            EventSystem eventSystem = EventSystem.current;
            yield return null; // wait one frame

            if (m_SubmitButton == null || !CanApplyDelayedFocus(eventSystem, m_SubmitButton.gameObject))
                yield break;

            m_SubmitButton.Select();
        }

        private IEnumerator ClearFocusNextFrame()
        {
            yield return null; // wait one frame
            EventSystem.current?.SetSelectedGameObject(null);
        }

        private bool CanApplyDelayedFocus(EventSystem eventSystem, GameObject selection = null)
        {
            if (m_UiCanvas == null || !m_UiCanvas.activeInHierarchy || eventSystem == null || !eventSystem.isActiveAndEnabled || EventSystem.current != eventSystem)
                return false;

            return selection == null || selection.activeInHierarchy;
        }

        public void ProcessCommand()
        {
            DeveloperConsole.ProcessCommand(m_InputField.text, this, out var close);
            m_InputField.text = string.Empty;
#if !UNITY_EDITOR && UNITY_PS5
            StartCoroutine(ClearFocusNextFrame());
#endif
            SuggestionAppliedEvent?.Invoke();
            if (close)
                Toggle(default);
        }

        public void DisplayHelpText(string helpText)
        {
            HelpTextDisplayed?.Invoke(helpText ?? string.Empty);
            if (m_HelpText == null)
                return;

            m_HelpText.text = helpText ?? string.Empty;
            RefreshHelpLayout();
        }

        public void SetCommandGateway(IDeveloperConsoleCommandGateway gateway)
        {
            if (ReferenceEquals(_commandGateway, gateway))
                return;

            if (_commandGateway != null)
            {
                _commandGateway.CatalogChanged -= RebuildGatewayCommands;
                _commandGateway.CommandCompleted -= OnGatewayCommandCompleted;
            }

            _commandGateway = gateway;
            if (_commandGateway == null)
            {
                SetDeveloperConsole(null, true);
                return;
            }

            _commandGateway.CatalogChanged += RebuildGatewayCommands;
            _commandGateway.CommandCompleted += OnGatewayCommandCompleted;
            RebuildGatewayCommands();
        }

        public void SetConsoleVisible(bool visible)
        {
            _consoleVisibilityRequested = visible;
            ApplyConsoleVisibility();
        }

        private void InitializeHelpLayout()
        {
            if (m_CommandPanel == null && m_InputField != null)
                m_CommandPanel = m_InputField.transform.parent as RectTransform;
            if (m_HelpText == null || m_CommandPanel == null)
                return;

            RectTransform helpRect = m_HelpText.rectTransform;
            _compactCommandPanelHeight = m_CommandPanel.sizeDelta.y;
            _minimumHelpHeight = helpRect.sizeDelta.y;
            _helpBottomOffset = helpRect.anchoredPosition.y - _minimumHelpHeight * helpRect.pivot.y;
            _helpTopPadding = Mathf.Max(0f, _compactCommandPanelHeight - _helpBottomOffset - _minimumHelpHeight);

            m_SuggestionPanels ??= Array.Empty<RectTransform>();
            _suggestionPanelGaps = new float[m_SuggestionPanels.Length];
            for (int i = 0; i < m_SuggestionPanels.Length; i++)
            {
                RectTransform panel = m_SuggestionPanels[i];
                if (panel != null)
                    _suggestionPanelGaps[i] = panel.offsetMin.y - _compactCommandPanelHeight;
            }

            _helpLayoutInitialized = true;
        }

        private void RefreshHelpLayout()
        {
            if (!_helpLayoutInitialized)
                InitializeHelpLayout();
            if (!_helpLayoutInitialized)
                return;

            RectTransform helpRect = m_HelpText.rectTransform;
            bool showHelp = m_TreeViewSuggestionToggle == null || m_TreeViewSuggestionToggle.isOn;
            bool hasHelp = showHelp && !string.IsNullOrWhiteSpace(m_HelpText.text);
            float helpHeight = _minimumHelpHeight;
            if (hasHelp)
            {
                float availableWidth = Mathf.Max(1f, helpRect.rect.width);
                float preferredHeight = m_HelpText.GetPreferredValues(m_HelpText.text, availableWidth, Mathf.Infinity).y;
                helpHeight = Mathf.Clamp(Mathf.Ceil(preferredHeight), _minimumHelpHeight, Mathf.Max(_minimumHelpHeight, m_MaxHelpHeight));
            }

            Vector2 helpSize = helpRect.sizeDelta;
            helpSize.y = helpHeight;
            helpRect.sizeDelta = helpSize;

            Vector2 helpPosition = helpRect.anchoredPosition;
            helpPosition.y = _helpBottomOffset + helpHeight * helpRect.pivot.y;
            helpRect.anchoredPosition = helpPosition;

            float commandPanelHeight = Mathf.Max(_compactCommandPanelHeight, _helpBottomOffset + helpHeight + _helpTopPadding);
            Vector2 panelSize = m_CommandPanel.sizeDelta;
            panelSize.y = commandPanelHeight;
            m_CommandPanel.sizeDelta = panelSize;

            Vector2 panelPosition = m_CommandPanel.anchoredPosition;
            panelPosition.y = commandPanelHeight * m_CommandPanel.pivot.y;
            m_CommandPanel.anchoredPosition = panelPosition;

            for (int i = 0; i < m_SuggestionPanels.Length; i++)
            {
                RectTransform panel = m_SuggestionPanels[i];
                if (panel == null)
                    continue;

                Vector2 offsetMin = panel.offsetMin;
                offsetMin.y = commandPanelHeight + _suggestionPanelGaps[i];
                panel.offsetMin = offsetMin;
            }
        }


        private void OnInputChanged(string input)
        {
            if (!string.IsNullOrWhiteSpace(input) && m_HelpText != null && !string.IsNullOrEmpty(m_HelpText.text))
                DisplayHelpText(string.Empty);

            InputChangedEvent?.Invoke(input);
#if !UNITY_EDITOR && UNITY_PS5
            StartCoroutine(FocusSubmitNextFrame());
#endif
        }

        public void ApplySuggestion(string suggestion)
        {
            m_InputField.text = _developerConsole._prefix + suggestion + " ";
            m_InputField.caretPosition = m_InputField.text.Length;
            m_InputField.Select();
            StartCoroutine(SelectGameObjectNextFrame());
            SuggestionAppliedEvent?.Invoke();
        }

        private IEnumerator SelectGameObjectNextFrame()
        {
            EventSystem eventSystem = EventSystem.current;
            yield return null;

            if (m_SubmitButton == null || !CanApplyDelayedFocus(eventSystem, m_SubmitButton.gameObject))
                yield break;

            eventSystem.SetSelectedGameObject(null);
            eventSystem.SetSelectedGameObject(m_SubmitButton.gameObject);
        }

        private void OnSubmit(CallbackContext context)
        {
            if (!context.performed)
                return;

            if (m_InputField != null && m_InputField.isFocused)
            {
                m_SubmitButton.onClick.Invoke();
            }
        }

        private void FocusInput(CallbackContext context)
        {
            if (!context.performed)
                return;

            if (m_InputField != null && m_UiCanvas != null && m_UiCanvas.activeInHierarchy)
            {
                EventSystem.current?.SetSelectedGameObject(null);
                StartCoroutine(FocusInputFieldNextFrame());
            }
        }


        private void GetNextCommandHistory(CallbackContext context)
        {
            SetCommand(_developerConsole.CommandHistory.GetNext());
        }

        private void GetPrevCommandHistory(CallbackContext context)
        {
            SetCommand(_developerConsole.CommandHistory.GetPrevious());
        }

        private void SetCommand(string command)
        {
            m_InputField.SetDelayedText(command);
            StartCoroutine(SelectGameObjectNextFrame());
        }

        private void ExecuteCommandsFromCommandLine()
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length; i++)
            {
                if (args[i] == "-consoleCmd" && i + 1 < args.Length)
                {
                    string raw = args[i + 1];
                    string[] commands = raw.Split(';');

                    foreach (var cmd in commands)
                    {
                        if (!string.IsNullOrWhiteSpace(cmd))
                            DeveloperConsole.ProcessCommand(cmd.Trim(), this, out _);
                    }
                }
            }
        }

        private void RebuildGatewayCommands()
        {
            if (_commandGateway == null)
                return;

            var commands = new List<IConsoleCommand>();
            DeveloperConsoleCommandDescriptor[] descriptors = _commandGateway.Commands ?? Array.Empty<DeveloperConsoleCommandDescriptor>();
            foreach (DeveloperConsoleCommandDescriptor descriptor in descriptors)
            {
                if (descriptor != null && !string.IsNullOrWhiteSpace(descriptor.Name))
                    commands.Add(new GatewayConsoleCommandProxy(descriptor, _commandGateway));
            }

            SetDeveloperConsole(new DeveloperConsole(_commandGateway.Prefix ?? string.Empty, commands), true);
            InputChangedEvent?.Invoke(m_InputField != null ? m_InputField.text : string.Empty);
        }

        private void SetDeveloperConsole(DeveloperConsole console, bool notify)
        {
            if (ReferenceEquals(_developerConsole, console))
                return;

            if (_developerConsole != null)
                _developerConsole.CommandsChanged -= OnCommandsChanged;

            _developerConsole = console;

            if (_developerConsole != null)
                _developerConsole.CommandsChanged += OnCommandsChanged;

            if (notify)
                OnCommandsChanged();
        }

        private void OnCommandsChanged()
        {
            CommandsChanged?.Invoke();
        }

        private void OnGatewayCommandCompleted(DeveloperConsoleCommandResult response)
        {
            if (response == null)
                return;
            DisplayHelpText(response.Message ?? string.Empty);
        }

        private void OnPresentationSuppressionChanged()
        {
            if (DevUtilityPresentationRegistry.CanShowLocalUi)
            {
                if (isActiveAndEnabled)
                    _inputActions?.Developer.Enable();
                ApplyConsoleVisibility();
                return;
            }

            _inputActions?.Developer.Disable();
            ApplyConsoleVisibility();
        }
    }
}
