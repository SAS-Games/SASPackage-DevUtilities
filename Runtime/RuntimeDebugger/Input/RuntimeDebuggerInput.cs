using UnityEngine;
using UnityEngine.InputSystem;

namespace SAS.Utilities.RuntimeDebugger.Input
{
    public interface IRuntimeDebuggerInput
    {
        bool Toggle { get; } bool Cancel { get; } bool Confirm { get; } bool Tab { get; } bool ShiftTab { get; }
        bool Up { get; } bool Down { get; } bool Left { get; } bool Right { get; } bool Home { get; } bool End { get; }
        bool PageUp { get; } bool PageDown { get; } bool Search { get; } bool Space { get; } bool Refresh { get; }
    }

    public sealed class InputSystemRuntimeDebuggerInput : IRuntimeDebuggerInput
    {
        private readonly RuntimeDebuggerSettings _settings;
        private float _nextRepeat;
        private Vector2 _lastDirection;
        private Vector2 _direction;
        private bool _firstRepeat;

        public InputSystemRuntimeDebuggerInput(RuntimeDebuggerSettings settings) => _settings = settings;
        private Keyboard K => Keyboard.current;
        private Gamepad G => Gamepad.current;
        private bool Pressed(Key key) => K != null && K[key].wasPressedThisFrame;
        public bool Toggle => Pressed(UnityEngine.InputSystem.Key.F1) || Pressed(UnityEngine.InputSystem.Key.Backquote) || (G != null && G.leftStickButton.isPressed && G.rightStickButton.isPressed && (G.leftStickButton.wasPressedThisFrame || G.rightStickButton.wasPressedThisFrame));
        public bool Cancel => Pressed(UnityEngine.InputSystem.Key.Escape) || (G?.buttonEast.wasPressedThisFrame ?? false);
        public bool Confirm => Pressed(UnityEngine.InputSystem.Key.Enter) || Pressed(UnityEngine.InputSystem.Key.NumpadEnter) || (G?.buttonSouth.wasPressedThisFrame ?? false);
        public bool Tab => Pressed(UnityEngine.InputSystem.Key.Tab) || (G?.rightShoulder.wasPressedThisFrame ?? false);
        public bool ShiftTab => Pressed(UnityEngine.InputSystem.Key.Tab) && (K?.shiftKey.isPressed ?? false) || (G?.leftShoulder.wasPressedThisFrame ?? false);
        public bool Up => Pressed(UnityEngine.InputSystem.Key.UpArrow) || Repeated(Vector2.up);
        public bool Down => Pressed(UnityEngine.InputSystem.Key.DownArrow) || Repeated(Vector2.down);
        public bool Left => Pressed(UnityEngine.InputSystem.Key.LeftArrow) || Repeated(Vector2.left);
        public bool Right => Pressed(UnityEngine.InputSystem.Key.RightArrow) || Repeated(Vector2.right);
        public bool Home => Pressed(UnityEngine.InputSystem.Key.Home);
        public bool End => Pressed(UnityEngine.InputSystem.Key.End);
        public bool PageUp => Pressed(UnityEngine.InputSystem.Key.PageUp) || (G?.leftTrigger.wasPressedThisFrame ?? false);
        public bool PageDown => Pressed(UnityEngine.InputSystem.Key.PageDown) || (G?.rightTrigger.wasPressedThisFrame ?? false);
        public bool Search => Pressed(UnityEngine.InputSystem.Key.Slash) || (Pressed(UnityEngine.InputSystem.Key.F) && (K?.ctrlKey.isPressed ?? false));
        public bool Space => Pressed(UnityEngine.InputSystem.Key.Space);
        public bool Refresh => Pressed(UnityEngine.InputSystem.Key.F5);

        public void Update()
        {
            Vector2 stick = G?.leftStick.ReadValue() ?? Vector2.zero;
            Vector2 dpad = G?.dpad.ReadValue() ?? Vector2.zero;
            Vector2 value = dpad.sqrMagnitude > 0.1f ? dpad : stick;
            _direction = value.magnitude >= _settings.ControllerDeadZone ? (Mathf.Abs(value.x) > Mathf.Abs(value.y) ? new Vector2(Mathf.Sign(value.x), 0f) : new Vector2(0f, Mathf.Sign(value.y))) : Vector2.zero;
            if (_direction != _lastDirection) { _lastDirection = _direction; _nextRepeat = Time.unscaledTime; _firstRepeat = true; }
        }

        private bool Repeated(Vector2 direction)
        {
            if (_direction != direction || Time.unscaledTime < _nextRepeat) return false;
            _nextRepeat = Time.unscaledTime + (_firstRepeat ? _settings.NavigationRepeatDelay : _settings.NavigationRepeatRate);
            _firstRepeat = false;
            return true;
        }
    }
}
