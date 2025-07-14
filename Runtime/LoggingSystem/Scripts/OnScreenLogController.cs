using System;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SAS
{
    public class OnScreenLogController : MonoBehaviour
    {
        [SerializeField] private Button m_ClearButton;
        [SerializeField] private Button m_ToggleButton;
        [SerializeField] private Slider m_LifetimeSlider;
        [SerializeField] private TextMeshProUGUI m_LifetimeLabel;
        [SerializeField] private TMP_InputField m_InputField;
        [SerializeField] private Toggle m_InfoToggle;
        [SerializeField] private Toggle m_WarningToggle;
        [SerializeField] private Toggle m_ErrorToggle;

        private OnScreenLogUI _onScreenLogUI;

        private void Awake()
        {
            _onScreenLogUI = GetComponentInParent<OnScreenLogUI>();
            m_ClearButton.onClick.AddListener(_onScreenLogUI.ClearLogs);
            m_ToggleButton.onClick.AddListener(() => gameObject.SetActive(!gameObject.activeSelf));
            m_LifetimeSlider.onValueChanged.AddListener(SetLifetime);
            SetLifetime(m_LifetimeSlider.value);
            m_InputField.onValueChanged.AddListener(OnFilterInputChanged);
            m_InfoToggle.onValueChanged.AddListener((state) => SetLogLevel(LogLevel.Info, state));
            m_WarningToggle.onValueChanged.AddListener((state) => SetLogLevel(LogLevel.Warning, state));
            m_ErrorToggle.onValueChanged.AddListener((state) => SetLogLevel(LogLevel.Error, state));
        }

        private void OnEnable()
        {
            m_InfoToggle.isOn = Debug.IsLogLevelEnabled(LogLevel.Info);
            m_WarningToggle.isOn = Debug.IsLogLevelEnabled(LogLevel.Warning);
            m_ErrorToggle.isOn = Debug.IsLogLevelEnabled(LogLevel.Error);
        }

        private void SetLifetime(float value)
        {
            _onScreenLogUI.SetLifetime(m_LifetimeSlider.value);
            if (m_LifetimeLabel != null)
                m_LifetimeLabel.text = value > 0 ? $"{value:F1}s" : "∞";
        }

        private void OnFilterInputChanged(string filter)
        {
            var tags = filter.Split("|").ToList();
            for (int i = tags.Count - 1; i >= 0; i--)
            {
                if (string.IsNullOrEmpty(tags[i]))
                    tags.RemoveAt(i);
            }

            Debug.SetAllowedTags(tags);
        }

        private void SetLogLevel(LogLevel logLevel, bool state)
        {
            Debug.SetLogLevel(logLevel, state);
        }
    }
}