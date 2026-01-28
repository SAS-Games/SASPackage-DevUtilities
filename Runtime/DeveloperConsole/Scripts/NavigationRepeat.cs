using System;
using UnityEngine;
class NavigationRepeat
{
    readonly Action<float> _fire;
    readonly float _initialDelay;
    readonly float _repeatInterval;

    bool _held;
    float _direction;
    float _nextTime;

    public NavigationRepeat(Action<float> fire, float initialDelay = 0.35f, float repeatInterval = 0.08f)
    {
        _fire = fire;
        _initialDelay = initialDelay;
        _repeatInterval = repeatInterval;
    }

    public void Press(float direction)
    {
        _held = true;
        _direction = Mathf.Sign(direction);
        _nextTime = Time.unscaledTime + _initialDelay;

        _fire(_direction);
    }

    public void Release()
    {
        _held = false;
    }

    public void Update()
    {
        if (!_held)
            return;

        if (Time.unscaledTime >= _nextTime)
        {
            _nextTime += _repeatInterval;
            _fire(_direction);
        }
    }
}
