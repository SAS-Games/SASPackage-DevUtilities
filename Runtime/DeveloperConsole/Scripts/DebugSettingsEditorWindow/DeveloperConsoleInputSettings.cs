using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace SAS.Utilities.DeveloperConsole
{
    [Serializable]
    public sealed class DeveloperConsoleInputSettings
    {
        internal const float DefaultKeyboardHoldDuration = 0f;
        internal const float DefaultGamepadHoldDuration = 0.4f;
        internal const int DefaultTouchscreenTouchCount = 3;
        internal const float DefaultTouchscreenHoldDuration = 1.5f;

        [SerializeField] private List<Key> m_KeyboardKeys = new() { Key.Backquote };
        [SerializeField, Min(0f)] private float m_KeyboardHoldDuration = DefaultKeyboardHoldDuration;
        [SerializeField] private List<GamepadButton> m_GamepadButtons = new()
        {
            GamepadButton.DpadDown,
            GamepadButton.LeftShoulder,
            GamepadButton.South
        };
        [SerializeField] private bool m_RequireGamepadTouchpad;
        [SerializeField, Min(0f)] private float m_GamepadHoldDuration = DefaultGamepadHoldDuration;
        [SerializeField] private bool m_EnableTouchscreenGesture;
        [SerializeField, Range(1, 10)] private int m_TouchscreenTouchCount = DefaultTouchscreenTouchCount;
        [SerializeField, Min(0f)] private float m_TouchscreenHoldDuration = DefaultTouchscreenHoldDuration;

        public IReadOnlyList<Key> KeyboardKeys => m_KeyboardKeys;
        public float KeyboardHoldDuration => Mathf.Max(0f, m_KeyboardHoldDuration);
        public IReadOnlyList<GamepadButton> GamepadButtons => m_GamepadButtons;
        public bool RequireGamepadTouchpad => m_RequireGamepadTouchpad;
        public float GamepadHoldDuration => Mathf.Max(0f, m_GamepadHoldDuration);
        public bool EnableTouchscreenGesture => m_EnableTouchscreenGesture;
        public int TouchscreenTouchCount => Mathf.Clamp(m_TouchscreenTouchCount, 1, 10);
        public float TouchscreenHoldDuration => Mathf.Max(0f, m_TouchscreenHoldDuration);

        internal DeveloperConsoleInputSettings Clone()
        {
            var clone = new DeveloperConsoleInputSettings();
            clone.CopyFrom(this);
            return clone;
        }

        internal void CopyFrom(DeveloperConsoleInputSettings source)
        {
            if (source == null)
            {
                ResetToDefaults();
                return;
            }

            m_KeyboardKeys = source.m_KeyboardKeys == null
                ? new List<Key>()
                : new List<Key>(source.m_KeyboardKeys);
            m_KeyboardHoldDuration = Mathf.Max(0f, source.m_KeyboardHoldDuration);
            m_GamepadButtons = source.m_GamepadButtons == null
                ? new List<GamepadButton>()
                : new List<GamepadButton>(source.m_GamepadButtons);
            m_RequireGamepadTouchpad = source.m_RequireGamepadTouchpad;
            m_GamepadHoldDuration = Mathf.Max(0f, source.m_GamepadHoldDuration);
            m_EnableTouchscreenGesture = source.m_EnableTouchscreenGesture;
            m_TouchscreenTouchCount = Mathf.Clamp(source.m_TouchscreenTouchCount, 1, 10);
            m_TouchscreenHoldDuration = Mathf.Max(0f, source.m_TouchscreenHoldDuration);
        }

        internal void ResetToDefaults()
        {
            m_KeyboardKeys = new List<Key> { Key.Backquote };
            m_KeyboardHoldDuration = DefaultKeyboardHoldDuration;
            m_GamepadButtons = new List<GamepadButton>
            {
                GamepadButton.DpadDown,
                GamepadButton.LeftShoulder,
                GamepadButton.South
            };
            m_RequireGamepadTouchpad = false;
            m_GamepadHoldDuration = DefaultGamepadHoldDuration;
            m_EnableTouchscreenGesture = false;
            m_TouchscreenTouchCount = DefaultTouchscreenTouchCount;
            m_TouchscreenHoldDuration = DefaultTouchscreenHoldDuration;
        }
    }
}
