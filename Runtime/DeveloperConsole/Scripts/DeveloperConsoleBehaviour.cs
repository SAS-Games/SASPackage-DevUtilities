using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Serialization;
using UnityEngine.UI;
using static UnityEngine.InputSystem.InputAction;

namespace SAS.Utilities.DeveloperConsole
{
    public class DeveloperConsoleBehaviour : MonoBehaviour
    {
        public Action<string> InputChangedEvent;
        public Action<bool> SuggestionViewChangedEvent;
        public Action SuggestionAppliedEvent;

        [SerializeField] private string m_Prefix = string.Empty;
        [SerializeField] private ConsoleCommand[] m_Commands = new ConsoleCommand[0];
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
                {
                    return _developerConsole;
                }

                return _developerConsole = new DeveloperConsole(m_Prefix, m_Commands);
            }
        }

        private void Awake()
        {
            _pausedTimeScale = Time.timeScale;
            _inputActions = new ConsoleInputActions();
            _inputActions.Developer.ToggleConsole.performed += Toggle;
            _inputActions.Developer.Submit.performed += OnSubmit;
            _inputActions.Developer.HistoryNavigationUP.performed += GetNexCommandHistory;
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

        private void GetNexCommandHistory(CallbackContext context)
        {
            string command = _developerConsole.CommandHistory.GetNext();
            m_InputField.text = command;
            StartCoroutine(SelectGameObjectNextFrame());
        }

        private void GetPrevCommandHistory(CallbackContext context)
        {
            var command = _developerConsole.CommandHistory.GetPrevious();
            m_InputField.text = command;
            StartCoroutine(SelectGameObjectNextFrame());
        }
    }
}