using TMPro;
using UnityEngine;
using static UnityEngine.InputSystem.InputAction;

namespace SAS.Utilities.DeveloperConsole
{
    public class DeveloperConsoleBehaviour : MonoBehaviour
    {
        [SerializeField] private string m_Prefix = string.Empty;
        [SerializeField] private ConsoleCommand[] m_Commands = new ConsoleCommand[0];

        [Header("UI")]
        [SerializeField] private GameObject m_UiCanvas = null;
        [SerializeField] private TMP_InputField m_InputField = null;
        [SerializeField] private TMP_Text m_SuggestionsText = null;
        [SerializeField] private TMP_Text m_HelpText = null;
        [SerializeField] private bool m_PauseOnOpen = false;

        private float pausedTimeScale;
        private DeveloperConsole developerConsole;

        private DeveloperConsole DeveloperConsole
        {
            get
            {
                if (developerConsole != null) { return developerConsole; }
                return developerConsole = new DeveloperConsole(m_Prefix, m_Commands);
            }
        }

        private void Awake()
        {
            pausedTimeScale = Time.timeScale;

            if (m_InputField != null)
                m_InputField.onValueChanged.AddListener(OnInputChanged);
        }

        public void Toggle(CallbackContext context)
        {
            if (!context.action.triggered) { return; }

            if (m_UiCanvas.activeSelf)
            {
                if (m_InputField != null)
                    Time.timeScale = pausedTimeScale;
                m_UiCanvas.SetActive(false);
            }
            else
            {
                if (m_PauseOnOpen)
                {
                    pausedTimeScale = Time.timeScale;
                    Time.timeScale = 0;
                }
                m_UiCanvas.SetActive(true);
                m_InputField.ActivateInputField();
            }
        }

        public void ProcessCommand(string inputValue)
        {
            DeveloperConsole.ProcessCommand(inputValue, this);
            m_InputField.text = string.Empty;
            m_SuggestionsText.text = string.Empty;
        }
        public void DisplayHelpText(string helpText)
        {
            if (m_HelpText != null)
                m_HelpText.text = helpText;
        }


        private void OnInputChanged(string input)
        {
            if (string.IsNullOrEmpty(input))
            {
                m_SuggestionsText.text = string.Empty;
                return;
            }

            var suggestions = DeveloperConsole.GetCommandSuggestion(input);
            m_SuggestionsText.text = string.Join("\n", suggestions);
        }
    }
}
