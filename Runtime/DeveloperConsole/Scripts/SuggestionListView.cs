using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SAS.Utilities.DeveloperConsole
{
    [RequireComponent(typeof(ScrollSnapper))]
    public class SuggestionListView : SuggestionView
    {
        [SerializeField] private RectTransform m_Container;
        [SerializeField] private GameObject m_SuggestionTemplate;

        private List<GameObject> _activeSuggestions = new();
        private ScrollSnapper _scrollSnapper;
        private CommandHistory _commandHistory;
        
        protected override void Awake()
        {
            base.Awake();
            m_SuggestionTemplate.SetActive(false);
            ClearSuggestions();
            _scrollSnapper = GetComponent<ScrollSnapper>();
        }

        private void Start() => OnSuggestionViewChanged(_developerConsoleUI.IsTreeViewSuggestion);

        private void ShowSuggestions(List<string> suggestions)
        {
            ClearSuggestions();
            if (suggestions == null || suggestions.Count == 0)
            {
                gameObject.SetActive(false);
                return;
            }

            gameObject.SetActive(true);
            foreach (var suggestion in suggestions)
            {
                var item = Instantiate(m_SuggestionTemplate, m_Container);
                item.SetActive(true);
                item.GetComponentInChildren<TMP_Text>().text = suggestion;
                item.GetComponent<Button>().onClick.AddListener(() => _developerConsoleUI.ApplySuggestion(suggestion));
                _activeSuggestions.Add(item);
            }

            if (_activeSuggestions.Count > 0)
                _scrollSnapper.FocusOn(_activeSuggestions[0].transform);
            _selectedIndex = -1;
        }

        protected override void Navigate(float direction)
        {
            if (_activeSuggestions.Count == 0) return;
            if (direction > 0)
                _selectedIndex = Mathf.Max(_selectedIndex - 1, 0);
            else if (direction < 0)
                _selectedIndex = Mathf.Min(_selectedIndex + 1, _activeSuggestions.Count - 1);
            HighlightSelection();
        }

        protected override void SelectCurrent()
        {
            if (_selectedIndex >= 0 && _selectedIndex < _activeSuggestions.Count)
            {
                string selected = _activeSuggestions[_selectedIndex].GetComponentInChildren<TMP_Text>().text;
                _developerConsoleUI.ApplySuggestion(selected);
            }
        }

        private void HighlightSelection()
        {
            for (int i = 0; i < _activeSuggestions.Count; i++)
            {
                var text = _activeSuggestions[i].GetComponentInChildren<TMP_Text>();
                text.color = (i == _selectedIndex) ? Color.yellow : Color.white;
                if (i == _selectedIndex)
                {
                    _scrollSnapper.FocusOn(_activeSuggestions[i].transform);
                    StartCoroutine(SelectGameObjectNextFrame(_activeSuggestions[i].GetComponentInChildren<Button>().gameObject));
                }
            }
        }

        protected override void ClearSuggestions()
        {
            foreach (var go in _activeSuggestions)
                Destroy(go);
            _activeSuggestions.Clear();
            _selectedIndex = -1;
        }

        private void OnInputChanged(string input)
        {
            if (string.IsNullOrEmpty(input))
            {
                ClearSuggestions();
                return;
            }

            ShowSuggestions(_developerConsoleUI.DeveloperConsole.GetCommandSuggestions(input));
        }

        protected override void OnSuggestionViewChanged(bool treeView)
        {
            if (!treeView)
            {
                _developerConsoleUI.InputChangedEvent += OnInputChanged;
                _developerConsoleUI.SuggestionAppliedEvent += ClearSuggestions;
                _commandHistory = _developerConsoleUI.DeveloperConsole.CommandHistory;
            }
            else
            {
                _developerConsoleUI.InputChangedEvent -= OnInputChanged;
                _developerConsoleUI.SuggestionAppliedEvent -= ClearSuggestions;
                ClearSuggestions();
            }

            gameObject.SetActive(!treeView);
        }
    }
}