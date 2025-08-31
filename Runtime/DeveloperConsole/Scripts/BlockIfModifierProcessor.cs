using UnityEngine.InputSystem;

#if UNITY_EDITOR
using UnityEditor;
#endif

public enum ModifierKey
{
    Ctrl,
    Shift,
    Alt,
    GamepadSouth,   // A (Xbox), X (PS)
    GamepadEast,    // B (Xbox), O (PS)
    GamepadWest,    // X (Xbox), Square (PS)
    GamepadNorth,   // Y (Xbox), Triangle (PS)
    GamepadL1,      // Left bumper
    GamepadR1,      // Right bumper
    GamepadL2,      // Left trigger
    GamepadR2       // Right trigger
}


public class BlockIfModifierProcessor : InputProcessor<float>
{
    public ModifierKey modifierKey = ModifierKey.Ctrl;

    public override float Process(float value, InputControl control)
    {
        bool block = false;

        // Keyboard modifiers
        if (Keyboard.current != null)
        {
            switch (modifierKey)
            {
                case ModifierKey.Ctrl: block = Keyboard.current.ctrlKey.isPressed; break;
                case ModifierKey.Shift: block = Keyboard.current.shiftKey.isPressed; break;
                case ModifierKey.Alt: block = Keyboard.current.altKey.isPressed; break;
            }
        }

        // Gamepad modifiers
        if (Gamepad.current != null)
        {
            switch (modifierKey)
            {
                case ModifierKey.GamepadSouth: block = Gamepad.current.buttonSouth.isPressed; break;
                case ModifierKey.GamepadEast: block = Gamepad.current.buttonEast.isPressed; break;
                case ModifierKey.GamepadWest: block = Gamepad.current.buttonWest.isPressed; break;
                case ModifierKey.GamepadNorth: block = Gamepad.current.buttonNorth.isPressed; break;
                case ModifierKey.GamepadL1: block = Gamepad.current.leftShoulder.isPressed; break;
                case ModifierKey.GamepadR1: block = Gamepad.current.rightShoulder.isPressed; break;
                case ModifierKey.GamepadL2: block = Gamepad.current.leftTrigger.isPressed; break;
                case ModifierKey.GamepadR2: block = Gamepad.current.rightTrigger.isPressed; break;
            }
        }

        return block ? 0f : value;
    }

}

#if UNITY_EDITOR
[InitializeOnLoad]
#endif
static class BlockIfModifierProcessorInitializer
{
    static BlockIfModifierProcessorInitializer()
    {
        InputSystem.RegisterProcessor<BlockIfModifierProcessor>();
    }
}
