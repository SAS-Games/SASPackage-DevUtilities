using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;

[DisallowMultipleComponent]
public class EventSystemUpdater : MonoBehaviour
{
    private EventSystem _localEventSystem;
    private GameObject _previousEventSystemObj;
    private bool _previousEventSystemWasActive;

    private void OnEnable()
    {
        _localEventSystem = GetComponentInChildren<EventSystem>(true);

        var currentEventSystem = EventSystem.current;

        bool hasInputSystemModule = currentEventSystem != null &&
            currentEventSystem.TryGetComponent<InputSystemUIInputModule>(out _);

        if (currentEventSystem != null)
        {
            _previousEventSystemObj = currentEventSystem.gameObject;
            _previousEventSystemWasActive = currentEventSystem.gameObject.activeSelf;
        }

        if (!hasInputSystemModule)
        {
            if (_previousEventSystemObj != null)
                _previousEventSystemObj.SetActive(false);

            _localEventSystem.gameObject.SetActive(true);
        }
        else
            _localEventSystem.gameObject.SetActive(false);
    }

    private void OnDisable()
    {
        if (_localEventSystem != null)
            _localEventSystem.gameObject.SetActive(false);

        if (_previousEventSystemObj != null)
        {
            _previousEventSystemObj.SetActive(_previousEventSystemWasActive);
            _previousEventSystemObj = null;
        }
    }
}
