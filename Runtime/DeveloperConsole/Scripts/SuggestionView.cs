using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;

namespace SAS.Utilities.DeveloperConsole
{
    public abstract class SuggestionView : MonoBehaviour
    {
        protected ConsoleInputActions _inputActions;
        protected DeveloperConsoleBehaviour _developerConsoleUI;
        NavigationRepeat _navRepeat;
        protected int _selectedIndex = -1;

        protected virtual void Awake()
        {
            _inputActions = new ConsoleInputActions();
            _navRepeat = new NavigationRepeat(Navigate);

            _inputActions.Developer.Navigate.performed += ctx => _navRepeat.Press(ctx.ReadValue<float>());
            _inputActions.Developer.Navigate.canceled += _ =>
            {
                _navRepeat.Release();
            };
            _inputActions.Developer.AutoComplete.performed += _ => SelectCurrent();

            _developerConsoleUI = GetComponentInParent<DeveloperConsoleBehaviour>();
            _developerConsoleUI.SuggestionViewChangedEvent += OnSuggestionViewChanged;
        }

        protected virtual void OnEnable() => _inputActions?.Developer.Enable();
        protected virtual void OnDisable()
        {
            _inputActions?.Developer.Disable();
            ClearSuggestions();
        }

        protected virtual void Update() => _navRepeat?.Update();

        protected virtual void OnDestroy() => _inputActions?.Dispose();

        protected IEnumerator SelectGameObjectNextFrame(GameObject go)
        {
            yield return null;
            EventSystem.current.SetSelectedGameObject(null);
            EventSystem.current.SetSelectedGameObject(go);
        }

        protected abstract void Navigate(float direction);
        protected abstract void SelectCurrent();
        protected abstract void OnSuggestionViewChanged(bool treeView);
        protected abstract void ClearSuggestions();
    }
}