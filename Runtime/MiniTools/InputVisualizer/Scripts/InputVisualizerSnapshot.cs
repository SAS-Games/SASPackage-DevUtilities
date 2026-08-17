using System;
using System.Collections.Generic;
using SAS.DevUtilities;
using UnityEngine;
using UnityEngine.InputSystem;

namespace SAS.Utilities.DeveloperConsole.InputVisualizers
{
    public enum InputVisualizerDeviceKind
    {
        Gamepad,
        Mouse
    }

    public enum InputVisualizerValueKind
    {
        None,
        Scalar,
        Vector2
    }

    [Serializable]
    public struct InputVisualizerControlValue
    {
        public string ControlPath;
        public InputVisualizerValueKind ValueKind;
        public bool HasValue;
        public float ScalarValue;
        public Vector2 Vector2Value;
    }

    [Serializable]
    public struct InputVisualizerSnapshot : IMiniToolSnapshot
    {
        public string CurrentDeviceName;
        public InputVisualizerControlValue[] Controls;
    }

    [Serializable]
    public struct InputVisualizerSampleEvent : IMiniToolStreamEvent
    {
        public bool DeviceChanged;
        public string CurrentDeviceName;
        public InputVisualizerControlValue[] Controls;
    }

    /// <summary>
    /// Captures portable input-control values without depending on the local
    /// visualizer prefab. Full snapshots recover presentation state, while the
    /// change stream preserves short input transitions between snapshots.
    /// </summary>
    public sealed class InputVisualizerSnapshotCollector
    {
        private readonly struct ControlSpec
        {
            internal ControlSpec(string path, InputVisualizerValueKind valueKind)
            {
                Path = path;
                ValueKind = valueKind;
            }

            internal string Path { get; }
            internal InputVisualizerValueKind ValueKind { get; }
        }

        private static readonly ControlSpec[] GamepadControls =
        {
            Scalar("<DualShockGamepad>/touchpadButton"),
            Scalar("<Gamepad>/buttonNorth"),
            Scalar("<Gamepad>/select"),
            Scalar("<Gamepad>/leftTrigger"),
            Scalar("<Gamepad>/leftShoulder"),
            Scalar("<Gamepad>/dpad/up"),
            Scalar("<Gamepad>/leftStickPress"),
            Scalar("<Gamepad>/rightShoulder"),
            Vector("<Gamepad>/dpad"),
            Scalar("<Gamepad>/buttonWest"),
            Scalar("<Gamepad>/buttonEast"),
            Scalar("<Gamepad>/dpad/down"),
            Scalar("<Gamepad>/dpad/left"),
            Scalar("<Gamepad>/start"),
            Vector("<Gamepad>/rightStick"),
            Scalar("<Gamepad>/dpad/right"),
            Vector("<Gamepad>/leftStick"),
            Scalar("<Gamepad>/rightStickPress"),
            Scalar("<Gamepad>/rightTrigger"),
            Scalar("<Gamepad>/buttonSouth")
        };

        private static readonly ControlSpec[] MouseControls =
        {
            Vector("<Mouse>/scroll"),
            Scalar("<Mouse>/middleButton"),
            Scalar("<Mouse>/leftButton"),
            Vector("<Mouse>/position"),
            Vector("<Mouse>/delta"),
            Scalar("<Mouse>/rightButton"),
            Scalar("<Mouse>/backButton"),
            Scalar("<Mouse>/forwardButton")
        };

        private readonly InputVisualizerDeviceKind _deviceKind;
        private readonly ControlSpec[] _specs;
        private readonly InputControl[] _controls;
        private readonly InputVisualizerControlValue[] _lastValues;
        private readonly List<InputVisualizerControlValue> _changes = new();

        private InputDevice _device;
        private string _lastDeviceName = string.Empty;
        private bool _hasLastValues;

        public InputVisualizerSnapshotCollector(InputVisualizerDeviceKind deviceKind)
        {
            _deviceKind = deviceKind;
            _specs = deviceKind == InputVisualizerDeviceKind.Gamepad ? GamepadControls : MouseControls;
            _controls = new InputControl[_specs.Length];
            _lastValues = new InputVisualizerControlValue[_specs.Length];
        }

        public void Reset()
        {
            _device = null;
            Array.Clear(_controls, 0, _controls.Length);
            Array.Clear(_lastValues, 0, _lastValues.Length);
            _lastDeviceName = string.Empty;
            _hasLastValues = false;
            _changes.Clear();
        }

        public InputVisualizerSnapshot Capture()
        {
            EnsureControls();
            var values = new InputVisualizerControlValue[_specs.Length];
            for (int i = 0; i < values.Length; i++)
                values[i] = ReadValue(i);

            return new InputVisualizerSnapshot
            {
                CurrentDeviceName = GetDeviceName(_device),
                Controls = values
            };
        }

        public bool TryCaptureChanges(out InputVisualizerSampleEvent sampleEvent)
        {
            EnsureControls();
            _changes.Clear();

            string deviceName = GetDeviceName(_device);
            bool deviceChanged = !_hasLastValues || !string.Equals(deviceName, _lastDeviceName, StringComparison.Ordinal);
            for (int i = 0; i < _specs.Length; i++)
            {
                InputVisualizerControlValue value = ReadValue(i);
                if (!_hasLastValues || !ValuesEqual(in value, in _lastValues[i]))
                    _changes.Add(value);
                _lastValues[i] = value;
            }

            _lastDeviceName = deviceName;
            _hasLastValues = true;
            if (!deviceChanged && _changes.Count == 0)
            {
                sampleEvent = default;
                return false;
            }

            sampleEvent = new InputVisualizerSampleEvent
            {
                DeviceChanged = deviceChanged,
                CurrentDeviceName = deviceName,
                Controls = _changes.ToArray()
            };
            return true;
        }

        private void EnsureControls()
        {
            InputDevice current = _deviceKind == InputVisualizerDeviceKind.Gamepad ? Gamepad.current : Mouse.current;
            if (_device == current)
                return;

            _device = current;
            Array.Clear(_controls, 0, _controls.Length);
            for (int i = 0; i < _specs.Length; i++)
                _controls[i] = FindControl(_specs[i].Path, current);
        }

        private InputVisualizerControlValue ReadValue(int index)
        {
            ControlSpec spec = _specs[index];
            InputControl control = _controls[index];
            var value = new InputVisualizerControlValue
            {
                ControlPath = spec.Path,
                ValueKind = spec.ValueKind,
                HasValue = control != null
            };

            if (control == null)
                return value;

            if (spec.ValueKind == InputVisualizerValueKind.Vector2 && control is InputControl<Vector2> vectorControl)
                value.Vector2Value = vectorControl.ReadValue();
            else if (spec.ValueKind == InputVisualizerValueKind.Scalar && control is InputControl<float> scalarControl)
                value.ScalarValue = scalarControl.ReadValue();
            else
                value.HasValue = false;

            return value;
        }

        private static InputControl FindControl(string path, InputDevice currentDevice)
        {
            if (currentDevice == null)
                return null;

            using (InputControlList<InputControl> candidates = InputSystem.FindControls(path))
            {
                foreach (InputControl candidate in candidates)
                {
                    if (candidate.device == currentDevice)
                        return candidate;
                }
            }

            return null;
        }

        private static bool ValuesEqual(in InputVisualizerControlValue left, in InputVisualizerControlValue right)
        {
            return left.HasValue == right.HasValue &&
                   left.ValueKind == right.ValueKind &&
                   left.ScalarValue.Equals(right.ScalarValue) &&
                   left.Vector2Value == right.Vector2Value;
        }

        private static string GetDeviceName(InputDevice device)
        {
            if (device == null)
                return string.Empty;
            return string.IsNullOrWhiteSpace(device.displayName) ? device.name : device.displayName;
        }

        private static ControlSpec Scalar(string path) => new(path, InputVisualizerValueKind.Scalar);
        private static ControlSpec Vector(string path) => new(path, InputVisualizerValueKind.Vector2);
    }
}
