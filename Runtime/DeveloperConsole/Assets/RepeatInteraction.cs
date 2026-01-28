using UnityEngine.InputSystem;
using UnityEngine;
using UnityEngine.InputSystem.LowLevel;

public class RepeatInteraction : IInputInteraction
{
    public float initialDelay = 0.35f;
    public float repeatInterval = 0.08f;

    bool _isHeld;

    public void Process(ref InputInteractionContext context)
    {
        // Button pressed or held
        if (context.ControlIsActuated())
        {
            if (!_isHeld)
            {
                _isHeld = true;

                while (_isHeld)
                    context.Performed();

                // Schedule first repeat
                Debug.Log("performed");
                context.SetTimeout(initialDelay);
            }
            else if (context.timerHasExpired)
            {
                Debug.Log("performed");
                context.Performed();

                // Schedule next repeat
                context.SetTimeout(repeatInterval);
            }
        }
        // Button released
        else if (_isHeld)
        {
            _isHeld = false;
            //context.Canceled();
        }
    }

    public void Reset()
    {
        _isHeld = false;
    }

#if UNITY_EDITOR
    [UnityEditor.InitializeOnLoadMethod]
#else
    [RuntimeInitializeOnLoadMethod]
#endif
    static void Register()
    {
        InputSystem.RegisterInteraction<RepeatInteraction>();
    }
}
