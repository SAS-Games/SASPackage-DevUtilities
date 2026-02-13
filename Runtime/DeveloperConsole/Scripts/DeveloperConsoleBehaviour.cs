using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
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
        private bool PauseOnOpen => DebugSettings.PauseOnEnable;

        private float _pausedTimeScale;
        private DeveloperConsole _developerConsole;
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

                return _developerConsole = new DeveloperConsole(m_Prefix, allCommands);
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
            _pausedTimeScale = Time.timeScale;
            _inputActions = new ConsoleInputActions();
            _inputActions.Developer.ToggleConsole.performed += Toggle;
            _inputActions.Developer.Submit.performed += OnSubmit;
            _inputActions.Developer.HighlightInput.canceled += FocusInput;
            _inputActions.Developer.HistoryNavigationUp.performed += GetNextCommandHistory;
            _inputActions.Developer.HistoryNavigationDown.performed += GetPrevCommandHistory;

            if (m_InputField != null)
                m_InputField.onValueChanged.AddListener(OnInputChanged);

            m_TreeViewSuggestionToggle.onValueChanged.AddListener(OnTreeViewSuggestion);
            OnTreeViewSuggestion(m_TreeViewSuggestionToggle.isOn);

            foreach (var commands in m_CommandsToExecuteOnLoad)
                DeveloperConsole.ProcessCommand(commands, this, out _);

            ExecuteCommandsFromCommandLine();

            DontDestroyOnLoad(this.gameObject);
        }

        private void OnTreeViewSuggestion(bool treeView)
        {
            SuggestionViewChangedEvent?.Invoke(treeView);
        }

        private void OnEnable() => _inputActions?.Developer.Enable();

        private void OnDisable() => _inputActions?.Developer.Disable();

        private void Toggle(CallbackContext context)
        {
            if (m_UiCanvas.activeSelf)
            {
                if (m_InputField != null)
                    Time.timeScale = _pausedTimeScale;
                EventSystem.current.SetSelectedGameObject(null);
                EventSystem.current.SetSelectedGameObject(_lastSelectedGameObject);
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
                _lastSelectedGameObject = EventSystem.current.currentSelectedGameObject;
                EventSystem.current.SetSelectedGameObject(null);
#if UNITY_EDITOR || !UNITY_PS5
                StartCoroutine(FocusInputFieldNextFrame());
#endif
            }

            DisplayHelpText("");
        }

        private IEnumerator FocusInputFieldNextFrame()
        {
            yield return null; // wait one frame
            m_InputField.ActivateInputField();
            m_InputField.Select();
            SuggestionAppliedEvent?.Invoke();
        }

        private IEnumerator FocusSubmitNextFrame()
        {
            yield return null; // wait one frame
            m_SubmitButton.Select();
        }

        private IEnumerator ClearFocusNextFrame()
        {
            yield return null; // wait one frame
            EventSystem.current.SetSelectedGameObject(null);
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
            if (m_HelpText != null)
                m_HelpText.text = helpText;
        }


        private void OnInputChanged(string input)
        {
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
            yield return null;
            EventSystem.current.SetSelectedGameObject(null);
            EventSystem.current.SetSelectedGameObject(m_SubmitButton.gameObject);
        }

        private void OnSubmit(CallbackContext context)
        {
            if (!context.performed) return;

            if (m_InputField != null && m_InputField.isFocused)
            {
                m_SubmitButton.onClick.Invoke();
            }
        }

        private void FocusInput(CallbackContext context)
        {
            if (!context.performed) return;

            if (m_InputField != null)
            {
                EventSystem.current.SetSelectedGameObject(null);
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

    }
}