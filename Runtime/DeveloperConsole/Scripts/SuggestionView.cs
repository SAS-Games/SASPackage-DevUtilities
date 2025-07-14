using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;

namespace SAS.Utilities.DeveloperConsole
{
    public abstract class SuggestionView : MonoBehaviour
    {
        protected ConsoleInputActions _inputActions;
        protected DeveloperConsoleBehaviour _developerConsoleUI;
        protected int _selectedIndex = -1;

        protected virtual void Awake()
        {
            _inputActions = new ConsoleInputActions();
            _inputActions.Developer.Navigate.performed += ctx => Navigate(ctx.ReadValue<Vector2>());
            _inputActions.Developer.AutoComplete.performed += _ => SelectCurrent();

            _developerConsoleUI = GetComponentInParent<DeveloperConsoleBehaviour>();
            _developerConsoleUI.SuggestionViewChangedEvent += OnSuggestionViewChanged;
        }

        protected virtual void OnEnable() => _inputActions.Developer.Enable();
        protected virtual void OnDisable()
        {
            _inputActions.Developer.Disable();
            ClearSuggestions();
        }

        protected virtual void OnDestroy() => _inputActions.Dispose();

        protected IEnumerator SelectGameObjectNextFrame(GameObject go)
        {
            yield return null;
            EventSystem.current.SetSelectedGameObject(null);
            EventSystem.current.SetSelectedGameObject(go);
        }

        protected abstract void Navigate(Vector2 direction);
        protected abstract void SelectCurrent();
        protected abstract void OnSuggestionViewChanged(bool treeView);
        protected abstract void ClearSuggestions();
    }
}