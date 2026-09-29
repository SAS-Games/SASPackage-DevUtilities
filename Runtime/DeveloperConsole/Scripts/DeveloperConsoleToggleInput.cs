using System.Collections.Generic;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.DualShock;
using UnityEngine.InputSystem.LowLevel;

namespace SAS.Utilities.DeveloperConsole
{
    internal sealed class DeveloperConsoleToggleInput
    {
        private ChordState _keyboardState;
        private ChordState _gamepadState;
        private ChordState _touchscreenState;

        internal bool WasTriggered(DeveloperConsoleInputSettings settings, float unscaledTime)
        {
            settings ??= new DeveloperConsoleInputSettings();

            bool keyboardTriggered = _keyboardState.Update(
                AreKeyboardKeysPressed(settings.KeyboardKeys),
                settings.KeyboardHoldDuration,
                unscaledTime);
            bool gamepadTriggered = _gamepadState.Update(
                AreGamepadControlsPressed(settings.GamepadButtons, settings.RequireGamepadTouchpad),
                settings.GamepadHoldDuration,
                unscaledTime);
            bool touchscreenTriggered = _touchscreenState.Update(
                HasRequiredTouches(settings.EnableTouchscreenGesture, settings.TouchscreenTouchCount),
                settings.TouchscreenHoldDuration,
                unscaledTime);

            return keyboardTriggered || gamepadTriggered || touchscreenTriggered;
        }

        internal void Reset()
        {
            _keyboardState.Reset();
            _gamepadState.Reset();
            _touchscreenState.Reset();
        }

        private static bool AreKeyboardKeysPressed(IReadOnlyList<Key> keys)
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null || keys == null || keys.Count == 0)
                return false;

            for (int i = 0; i < keys.Count; i++)
            {
                Key key = keys[i];
                if (key == Key.None || !keyboard[key].isPressed)
                    return false;
            }

            return true;
        }

        private static bool AreGamepadControlsPressed(IReadOnlyList<GamepadButton> buttons, bool requireTouchpad)
        {
            Gamepad gamepad = Gamepad.current;
            int buttonCount = buttons?.Count ?? 0;
            if (gamepad == null || buttonCount == 0 && !requireTouchpad)
                return false;

            for (int i = 0; i < buttonCount; i++)
            {
                if (!gamepad[buttons[i]].isPressed)
                    return false;
            }

            return !requireTouchpad ||
                   gamepad is DualShockGamepad dualShock && dualShock.touchpadButton.isPressed;
        }

        private static bool HasRequiredTouches(bool enabled, int requiredTouchCount)
        {
            Touchscreen touchscreen = Touchscreen.current;
            if (!enabled || touchscreen == null)
                return false;

            int pressedTouchCount = 0;
            for (int i = 0; i < touchscreen.touches.Count; i++)
            {
                if (touchscreen.touches[i].press.isPressed)
                    pressedTouchCount++;
            }

            return pressedTouchCount >= requiredTouchCount;
        }

        private struct ChordState
        {
            private bool _isHeld;
            private bool _hasTriggered;
            private float _heldSince;

            internal bool Update(bool isPressed, float holdDuration, float unscaledTime)
            {
                if (!isPressed)
                {
                    Reset();
                    return false;
                }

                if (!_isHeld)
                {
                    _isHeld = true;
                    _heldSince = unscaledTime;
                }

                if (_hasTriggered || unscaledTime - _heldSince < holdDuration)
                    return false;

                _hasTriggered = true;
                return true;
            }

            internal void Reset()
            {
                _isHeld = false;
                _hasTriggered = false;
                _heldSince = 0f;
            }
        }
    }
}
