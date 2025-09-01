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
        [Header("UI")] [SerializeField] private GameObject m_UiCanvas = null;
        [SerializeField] private TMP_InputField m_InputField = null;
        [SerializeField] private Button m_SubmitButton = null;
        [SerializeField] private TMP_Text m_HelpText = null;
        [SerializeField] private bool m_PauseOnOpen = false;
        [SerializeField] private Toggle m_TreeViewSuggestionToggle;

        private float _pausedTimeScale;
        private DeveloperConsole _developerConsole;
        private ConsoleInputActions _inputActions;
        public bool IsTreeViewSuggestion => m_TreeViewSuggestionToggle.isOn;
        private GameObject _lastSelectedGameObject;


        internal DeveloperConsole DeveloperConsole
        {
            get
            {
                if (_developerConsole != null)
                    return _developerConsole;

                var allCommands = new List<ConsoleCommand>();

                allCommands.AddRange(m_Commands);

                // Add platform-specific commands
                foreach (var pc in m_PlatformCommands)
                {
                    if (IsCurrentPlatform(pc.platform))
                    {
                        allCommands.AddRange(pc.commands);
                        break;
                    }
                }

                return _developerConsole = new DeveloperConsole(m_Prefix, allCommands);
            }
        }

        bool IsCurrentPlatform(Platform platform)
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
            _pausedTimeScale = Time.timeScale;
            _inputActions = new ConsoleInputActions();
            _inputActions.Developer.ToggleConsole.performed += Toggle;
            _inputActions.Developer.Submit.performed += OnSubmit;
            _inputActions.Developer.HistoryNavigationUp.performed += GetNextCommandHistory;
            _inputActions.Developer.HistoryNavigationDown.performed += GetPrevCommandHistory;

            if (m_InputField != null)
                m_InputField.onValueChanged.AddListener(OnInputChanged);

            m_TreeViewSuggestionToggle.onValueChanged.AddListener(OnTreeViewSuggestion);
            OnTreeViewSuggestion(m_TreeViewSuggestionToggle.isOn);
        }

        private void OnTreeViewSuggestion(bool treeView)
        {
            SuggestionViewChangedEvent?.Invoke(treeView);
        }

        private void OnEnable()
        {
            _inputActions?.Developer.Enable();
        }

        private void OnDisable() => _inputActions?.Developer.Disable();

        private void Toggle(CallbackContext context)
        {
            if (m_UiCanvas.activeSelf)
            {
                if (m_InputField != null)
                    Time.timeScale = _pausedTimeScale;
                m_UiCanvas.SetActive(false);
                EventSystem.current.SetSelectedGameObject(null);
                EventSystem.current.SetSelectedGameObject(_lastSelectedGameObject);
            }
            else
            {
                if (m_PauseOnOpen)
                {
                    _pausedTimeScale = Time.timeScale;
                    Time.timeScale = 0;
                }

                m_UiCanvas.SetActive(true);
                _lastSelectedGameObject = EventSystem.current.currentSelectedGameObject;
                StartCoroutine(FocusInputFieldNextFrame());
            }
        }

        private IEnumerator FocusInputFieldNextFrame()
        {
            yield return null; // wait one frame
            m_InputField.ActivateInputField();
            m_InputField.Select();
        }

        public void ProcessCommand()
        {
            DeveloperConsole.ProcessCommand(m_InputField.text, this);
            m_InputField.text = string.Empty;
            SuggestionAppliedEvent?.Invoke();
        }

        public void DisplayHelpText(string helpText)
        {
            if (m_HelpText != null)
                m_HelpText.text = helpText;
        }


        private void OnInputChanged(string input)
        {
            InputChangedEvent?.Invoke(input);
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
    }
}