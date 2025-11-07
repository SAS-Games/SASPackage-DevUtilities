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

        // Find the current active EventSystem in the scene
        var currentEventSystem = EventSystem.current;

        // Check if the current EventSystem is valid and uses InputSystemUIInputModule
        bool hasInputSystemModule = currentEventSystem != null &&
            currentEventSystem.TryGetComponent<InputSystemUIInputModule>(out _);

        // Store previous EventSystem state (if exists)
        if (currentEventSystem != null)
        {
            _previousEventSystemObj = currentEventSystem.gameObject;
            _previousEventSystemWasActive = currentEventSystem.gameObject.activeSelf;
        }

        // Enable our local EventSystem only if no valid InputSystem EventSystem is active
        if (!hasInputSystemModule)
        {
            if (_previousEventSystemObj != null)
                _previousEventSystemObj.SetActive(false);

            _localEventSystem.gameObject.SetActive(true);
        }
        else
        {
            // Another valid EventSystem already active; keep ours disabled
            _localEventSystem.gameObject.SetActive(false);
        }
    }

    private void OnDisable()
    {
        // Disable local EventSystem
        if (_localEventSystem != null)
            _localEventSystem.gameObject.SetActive(false);

        // Restore previous EventSystem state (if any)
        if (_previousEventSystemObj != null)
        {
            _previousEventSystemObj.SetActive(_previousEventSystemWasActive);
            _previousEventSystemObj = null;
        }
    }
}
