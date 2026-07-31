using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace SAS.Utilities.RuntimeSceneInspector.Input
{
    public interface IRuntimeSceneInspectorInput
    {
        bool Toggle { get; }
        bool Cancel { get; }
        bool Confirm { get; }
        bool Tab { get; }
        bool ShiftTab { get; }
        bool Up { get; }
        bool Down { get; }
        bool Left { get; }
        bool Right { get; }
        bool Home { get; }
        bool End { get; }
        bool PageUp { get; }
        bool PageDown { get; }
        bool Search { get; }
        bool Space { get; }
        bool Refresh { get; }
    }

    public sealed class InputSystemRuntimeSceneInspectorInput : IRuntimeSceneInspectorInput, IDisposable
    {
        [Flags]
        private enum Signal
        {
            None = 0,
            Toggle = 1 << 0,
            Cancel = 1 << 1,
            Confirm = 1 << 2,
            Tab = 1 << 3,
            ShiftTab = 1 << 4,
            Up = 1 << 5,
            Down = 1 << 6,
            Left = 1 << 7,
            Right = 1 << 8,
            Home = 1 << 9,
            End = 1 << 10,
            PageUp = 1 << 11,
            PageDown = 1 << 12,
            Space = 1 << 13,
            ClearSearch = 1 << 14,
            Refresh = 1 << 15
        }

        private const Signal DirectionMask = Signal.Up | Signal.Down | Signal.Left | Signal.Right;

        private readonly RuntimeSceneInspectorSettings _settings;
        private readonly List<InputAction> _actions = new();
        private Signal _pendingSignals;
        private Signal _currentSignals;
        private Signal _heldDirections;
        private Signal _lastHeldDirection;
        private Vector2 _stick;
        private Vector2 _lastDirection;
        private float _nextRepeat;
        private bool _waitForNeutralDirection;
        private bool _leftStickClickHeld;
        private bool _rightStickClickHeld;
        private bool _shiftHeld;
        private bool _controlHeld;
        private bool _disposed;

        public InputSystemRuntimeSceneInspectorInput(RuntimeSceneInspectorSettings settings)
        {
            _settings = settings;

            AddButtonAction("Toggle", Signal.Toggle, "<Keyboard>/f1", "<Keyboard>/backquote");
            AddButtonAction("Cancel", Signal.Cancel, "<Keyboard>/escape", "<Gamepad>/buttonEast");
            AddButtonAction("Confirm", Signal.Confirm, "<Keyboard>/enter", "<Keyboard>/numpadEnter", "<Gamepad>/buttonSouth");
            AddButtonAction("NextPanel", Signal.Tab, "<Gamepad>/rightShoulder");
            AddButtonAction("PreviousPanel", Signal.ShiftTab, "<Gamepad>/leftShoulder");
            AddButtonAction("Home", Signal.Home, "<Keyboard>/home");
            AddButtonAction("End", Signal.End, "<Keyboard>/end");
            AddButtonAction("PageUp", Signal.PageUp, "<Keyboard>/pageUp", "<Gamepad>/leftTrigger");
            AddButtonAction("PageDown", Signal.PageDown, "<Keyboard>/pageDown", "<Gamepad>/rightTrigger");
            AddButtonAction("Space", Signal.Space, "<Keyboard>/space", "<Gamepad>/buttonWest");
            AddButtonAction("ClearSearch", Signal.ClearSearch, "<Gamepad>/buttonWest");
            AddButtonAction("Refresh", Signal.Refresh, "<Keyboard>/f5");

            AddDirectionAction("Up", Signal.Up, "<Keyboard>/upArrow", "<Gamepad>/dpad/up");
            AddDirectionAction("Down", Signal.Down, "<Keyboard>/downArrow", "<Gamepad>/dpad/down");
            AddDirectionAction("Left", Signal.Left, "<Keyboard>/leftArrow", "<Gamepad>/dpad/left");
            AddDirectionAction("Right", Signal.Right, "<Keyboard>/rightArrow", "<Gamepad>/dpad/right");
            AddStickAction();

            AddHeldButtonAction("ToggleLeftStick", value =>
            {
                _leftStickClickHeld = value;
                if (value && _rightStickClickHeld)
                    Queue(Signal.Toggle);
            }, "<Gamepad>/leftStickPress");
            AddHeldButtonAction("ToggleRightStick", value =>
            {
                _rightStickClickHeld = value;
                if (value && _leftStickClickHeld)
                    Queue(Signal.Toggle);
            }, "<Gamepad>/rightStickPress");
            AddHeldButtonAction("Shift", value => _shiftHeld = value, "<Keyboard>/leftShift", "<Keyboard>/rightShift");
            AddHeldButtonAction("Control", value => _controlHeld = value, "<Keyboard>/leftCtrl", "<Keyboard>/rightCtrl");
            AddHeldButtonAction("KeyboardTab", value =>
            {
                if (value)
                    Queue(_shiftHeld ? Signal.ShiftTab : Signal.Tab);
            }, "<Keyboard>/tab");
        }

        public bool Toggle => Has(Signal.Toggle);
        public bool Cancel => Has(Signal.Cancel);
        public bool Confirm => Has(Signal.Confirm);
        public bool Tab => Has(Signal.Tab);
        public bool ShiftTab => Has(Signal.ShiftTab);
        public bool Up => Has(Signal.Up);
        public bool Down => Has(Signal.Down);
        public bool Left => Has(Signal.Left);
        public bool Right => Has(Signal.Right);
        public bool Home => Has(Signal.Home);
        public bool End => Has(Signal.End);
        public bool PageUp => Has(Signal.PageUp);
        public bool PageDown => Has(Signal.PageDown);

        // Search shortcuts are handled from IMGUI so the triggering slash/Ctrl+F event can be consumed before
        // a newly focused TextField receives it.
        public bool Search => false;

        public bool Space => Has(Signal.Space);
        public bool ClearSearch => Has(Signal.ClearSearch);
        public bool Refresh => Has(Signal.Refresh);
        public bool LargeStepModifier => _shiftHeld;
        public bool SmallStepModifier => _controlHeld;

        public void Update()
        {
            _currentSignals = _pendingSignals;
            _pendingSignals = Signal.None;
            CollapsePendingDirections();
            UpdateDirectionalRepeat((_currentSignals & DirectionMask) != Signal.None);
        }

        public void SetEnabled(bool value)
        {
            if (_disposed)
                return;
            foreach (InputAction action in _actions)
            {
                if (value && !action.enabled)
                    action.Enable();
                else if (!value && action.enabled)
                    action.Disable();
            }

            if (value)
                return;
            _pendingSignals = Signal.None;
            _currentSignals = Signal.None;
            _heldDirections = Signal.None;
            _lastHeldDirection = Signal.None;
            _stick = Vector2.zero;
            _lastDirection = Vector2.zero;
            _waitForNeutralDirection = false;
            _leftStickClickHeld = false;
            _rightStickClickHeld = false;
            _shiftHeld = false;
            _controlHeld = false;
        }

        public void ResetNavigationUntilNeutral()
        {
            _pendingSignals &= ~DirectionMask;
            _currentSignals &= ~DirectionMask;
            _waitForNeutralDirection = ResolveHeldDirection() != Vector2.zero;
            _lastDirection = Vector2.zero;
            _nextRepeat = 0f;
        }

        public void Dispose()
        {
            if (_disposed)
                return;
            _disposed = true;
            foreach (InputAction action in _actions)
            {
                action.Disable();
                action.Dispose();
            }

            _actions.Clear();
            _pendingSignals = Signal.None;
            _currentSignals = Signal.None;
            _heldDirections = Signal.None;
            _stick = Vector2.zero;
        }

        private bool Has(Signal signal) => (_currentSignals & signal) != 0;
        private void Queue(Signal signal) => _pendingSignals |= signal;

        private void AddButtonAction(string name, Signal signal, params string[] bindings)
        {
            var action = new InputAction(name, InputActionType.Button);
            foreach (string binding in bindings)
                action.AddBinding(binding);
            action.performed += _ => Queue(signal);
            Enable(action);
        }

        private void AddHeldButtonAction(string name, Action<bool> changed, params string[] bindings)
        {
            var action = new InputAction(name, InputActionType.Button);
            foreach (string binding in bindings)
                action.AddBinding(binding);
            action.performed += _ => changed(true);
            action.canceled += _ => changed(false);
            Enable(action);
        }

        private void AddDirectionAction(string name, Signal direction, params string[] bindings)
        {
            var action = new InputAction(name, InputActionType.Button);
            foreach (string binding in bindings)
                action.AddBinding(binding);
            action.performed += _ => PressDirection(direction);
            action.canceled += _ => ReleaseDirection(direction);
            Enable(action);
        }

        private void AddStickAction()
        {
            var action = new InputAction("NavigateStick", InputActionType.Value, "<Gamepad>/leftStick");
            action.performed += context => SetStick(context.ReadValue<Vector2>());
            action.canceled += _ => SetStick(Vector2.zero);
            Enable(action);
        }

        private void Enable(InputAction action)
        {
            _actions.Add(action);
            action.Enable();
        }

        private void PressDirection(Signal direction)
        {
            _heldDirections |= direction;
            _lastHeldDirection = direction;
            Queue(direction);
        }

        private void ReleaseDirection(Signal direction)
        {
            _heldDirections &= ~direction;
            if (_lastHeldDirection == direction)
                _lastHeldDirection = FirstHeldDirection();
            ReleaseNeutralGateIfNeeded();
        }

        private void SetStick(Vector2 value)
        {
            Vector2 previousDirection = ResolveStickDirection(_stick);
            _stick = value;
            Vector2 nextDirection = ResolveStickDirection(_stick);
            if (!_waitForNeutralDirection && _heldDirections == Signal.None && nextDirection != Vector2.zero && nextDirection != previousDirection)
                Queue(ToSignal(nextDirection));
            ReleaseNeutralGateIfNeeded();
        }

        private void ReleaseNeutralGateIfNeeded()
        {
            if (!_waitForNeutralDirection || ResolveHeldDirection() != Vector2.zero)
                return;
            _waitForNeutralDirection = false;
            _lastDirection = Vector2.zero;
            _nextRepeat = 0f;
        }

        private void UpdateDirectionalRepeat(bool directionAlreadyQueued)
        {
            Vector2 direction = ResolveHeldDirection();
            if (_waitForNeutralDirection)
            {
                _currentSignals &= ~DirectionMask;
                if (direction == Vector2.zero)
                    ReleaseNeutralGateIfNeeded();
                return;
            }

            if (direction != _lastDirection)
            {
                _lastDirection = direction;
                if (!directionAlreadyQueued && direction != Vector2.zero)
                    _currentSignals |= ToSignal(direction);
                _nextRepeat = Time.unscaledTime + _settings.NavigationRepeatDelay;
                return;
            }

            if (directionAlreadyQueued)
                return;
            if (direction == Vector2.zero || Time.unscaledTime < _nextRepeat)
                return;
            _currentSignals |= ToSignal(direction);
            _nextRepeat = Time.unscaledTime + _settings.NavigationRepeatRate;
        }

        private void CollapsePendingDirections()
        {
            Signal directions = _currentSignals & DirectionMask;
            int directionBits = (int)directions;
            if (directionBits == 0 || (directionBits & (directionBits - 1)) == 0)
                return;
            Signal selected = (directions & _lastHeldDirection) != 0 ? _lastHeldDirection : FirstDirection(directions);
            _currentSignals = (_currentSignals & ~DirectionMask) | selected;
        }

        private Vector2 ResolveHeldDirection()
        {
            Signal digital = (_heldDirections & _lastHeldDirection) != 0 ? _lastHeldDirection : FirstHeldDirection();
            if (digital != Signal.None)
                return ToVector(digital);

            return ResolveStickDirection(_stick);
        }

        private Signal FirstHeldDirection() => FirstDirection(_heldDirections);

        private static Signal FirstDirection(Signal directions)
        {
            if ((directions & Signal.Up) != 0)
                return Signal.Up;
            if ((directions & Signal.Down) != 0)
                return Signal.Down;
            if ((directions & Signal.Left) != 0)
                return Signal.Left;
            if ((directions & Signal.Right) != 0)
                return Signal.Right;
            return Signal.None;
        }

        private Vector2 ResolveStickDirection(Vector2 value)
        {
            if (value.magnitude < _settings.ControllerDeadZone)
                return Vector2.zero;
            return Mathf.Abs(value.x) > Mathf.Abs(value.y) ? new Vector2(Mathf.Sign(value.x), 0f) : new Vector2(0f, Mathf.Sign(value.y));
        }

        private static Signal ToSignal(Vector2 direction)
        {
            if (direction == Vector2.up)
                return Signal.Up;
            if (direction == Vector2.down)
                return Signal.Down;
            if (direction == Vector2.left)
                return Signal.Left;
            return Signal.Right;
        }

        private static Vector2 ToVector(Signal direction)
        {
            if (direction == Signal.Up)
                return Vector2.up;
            if (direction == Signal.Down)
                return Vector2.down;
            if (direction == Signal.Left)
                return Vector2.left;
            if (direction == Signal.Right)
                return Vector2.right;
            return Vector2.zero;
        }
    }
}
