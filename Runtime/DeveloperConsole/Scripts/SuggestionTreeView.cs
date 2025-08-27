using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SAS.Utilities.DeveloperConsole
{
    public class SuggestionTreeView : SuggestionView
    {
        [SerializeField] private RectTransform m_BaseCommandContainer;
        [SerializeField] private GameObject m_BaseCommandTemplate;
        [SerializeField] private GameObject m_PresetTemplate;

        private List<GameObject> _activeCommandObjects = new();
        private List<GameObject> _navigableItems = new();
        private GameObject _currentlyExpanded;
        private GameObject _highlightedItem;

        private DeveloperConsole _developerConsole;

        protected override void Awake()
        {
            base.Awake();
            _developerConsole = _developerConsoleUI.DeveloperConsole;
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            ShowCommands();
            _developerConsoleUI.SuggestionAppliedEvent += ShowCommands;
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            _developerConsoleUI.SuggestionAppliedEvent -= ShowCommands;
        }

        private void ShowCommands()
        {
            ClearSuggestions();
            gameObject.SetActive(true);
            foreach (var command in _developerConsole.ConsoleCommands)
                CreateBaseCommandUI(command.Name);
            RebuildNavigableList();
        }

        protected override void Navigate(Vector2 direction)
        {
            if (!gameObject.activeInHierarchy || _navigableItems.Count == 0) return;

            if (direction.y > 0)
                _selectedIndex = (_selectedIndex - 1 + _navigableItems.Count) % _navigableItems.Count;
            else if (direction.y < 0)
                _selectedIndex = (_selectedIndex + 1) % _navigableItems.Count;

            HighlightSelection();
            GetComponentInParent<ScrollToSelection>().FocusOn(_highlightedItem.transform);
        }

        private void RebuildNavigableList()
        {
            _navigableItems.Clear();
            foreach (var baseItem in _activeCommandObjects)
            {
                _navigableItems.Add(baseItem);
                var presetContainer = baseItem.transform.Find("PresetContainer");
                if (presetContainer != null && presetContainer.gameObject.activeSelf)
                {
                    foreach (Transform child in presetContainer)
                    {
                        if (child.gameObject != null && child.gameObject.activeSelf)
                            _navigableItems.Add(child.gameObject);
                    }
                }
            }

            if (_selectedIndex >= _navigableItems.Count)
                _selectedIndex = _navigableItems.Count - 1;
        }

        private void HighlightSelection()
        {
            if (_highlightedItem != null)
                _highlightedItem.GetComponentInChildren<TMP_Text>().color = Color.white;

            if (_selectedIndex >= 0 && _selectedIndex < _navigableItems.Count)
            {
                _highlightedItem = _navigableItems[_selectedIndex];
                _highlightedItem.GetComponentInChildren<TMP_Text>().color = Color.yellow;
                StartCoroutine(SelectGameObjectNextFrame(_highlightedItem.GetComponentInChildren<Button>().gameObject));
            }
        }

        private void CreateBaseCommandUI(string baseCommand)
        {
            var baseItem = Instantiate(m_BaseCommandTemplate, m_BaseCommandContainer);
            baseItem.SetActive(true);
            var label = baseItem.GetComponentInChildren<TMP_Text>();
            label.text = baseCommand;
            var presetContainer = baseItem.transform.Find("PresetContainer").GetComponent<RectTransform>();
            presetContainer.gameObject.SetActive(false);
            baseItem.GetComponentInChildren<Button>().onClick
                .AddListener(() => OnCommandSelected(label, baseCommand, presetContainer));
            _activeCommandObjects.Add(baseItem);
            LayoutRebuilder.ForceRebuildLayoutImmediate(m_BaseCommandContainer);
        }

        private void OnCommandSelected(TMP_Text label, string baseCommand, RectTransform presetContainer)
        {
            if (_currentlyExpanded != null && _currentlyExpanded != presetContainer.gameObject)
                _currentlyExpanded.SetActive(false);

            // Toggle current one                                                   
            bool isActive = presetContainer.gameObject.activeSelf;
            presetContainer.gameObject.SetActive(!isActive);
            _currentlyExpanded = presetContainer.gameObject.activeSelf ? presetContainer.gameObject : null;

            if (_currentlyExpanded != null)
            {
                var suggestions = _developerConsole.GetCommandSuggestions(baseCommand);
                suggestions = suggestions.Where(s => s != baseCommand)
                    .ToList();

                CreatePresetUI(label.renderedWidth, presetContainer, suggestions);
            }

            RebuildNavigableList();
            LayoutRebuilder.ForceRebuildLayoutImmediate(m_BaseCommandContainer);
            GetComponentInParent<ScrollToSelection>().FocusOn(_highlightedItem.transform);
        }

        private void CreatePresetUI(float startPos, RectTransform container, List<string> suggestions)
        {
            foreach (Transform child in container)
            {
                child.gameObject.SetActive(false);
                Destroy(child.gameObject);
            }

            foreach (var suggestion in suggestions)
            {
                var presetItem = Instantiate(m_PresetTemplate, container);
                presetItem.SetActive(true);
                presetItem.GetComponentInChildren<TMP_Text>().text = suggestion;
                presetItem.GetComponent<Button>().onClick
                    .AddListener(() => _developerConsoleUI.ApplySuggestion(suggestion));
            }

            LayoutRebuilder.ForceRebuildLayoutImmediate(container);
            var layout = container.GetComponent<VerticalLayoutGroup>();
            if (layout != null)
                layout.padding.left = Mathf.RoundToInt(startPos);
        }

        protected override void SelectCurrent()
        {
            // You can customize how selecting a tree view item works
        }

        protected override void ClearSuggestions()
        {
            foreach (var go in _activeCommandObjects)
                Destroy(go);
            _activeCommandObjects.Clear();
            _currentlyExpanded = null;
        }

        protected override void OnSuggestionViewChanged(bool treeView)
        {
            if (!treeView)
                ClearSuggestions();
            gameObject.SetActive(treeView);
        }
    }
}