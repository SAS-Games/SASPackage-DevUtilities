using System.Text;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class AnimatorStats : UIBehaviour
{
    [SerializeField] private Text m_Display;
    [SerializeField] private float m_UpdateInterval = 1f;

    private readonly StringBuilder _sb = new StringBuilder(256);
    private Animator[] _animators;

    private float _timer;
    private ProfilerRecorder _animUpdateRecorder;

    private int _activeAlways, _activeCullUpdate, _activeCull;
    private int _disabledAlways, _disabledCullUpdate, _disabledCull;

    protected override void OnEnable()
    {
        base.OnEnable();

        _animUpdateRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Animation, "Animators.Update");
        _animators = FindObjectsByType<Animator>(FindObjectsInactive.Include, FindObjectsSortMode.None);
    }

    protected override void OnDisable()
    {
        base.OnDisable();

        if (_animUpdateRecorder.Valid)
            _animUpdateRecorder.Dispose();
    }

    private void Update()
    {
        _timer -= Time.deltaTime;
        if (_timer > 0) return;
        _timer = m_UpdateInterval;
        RefreshAnimatorStats();
        BuildDisplay();
    }

    private void RefreshAnimatorStats()
    {
        _activeAlways = _activeCullUpdate = _activeCull = 0;
        _disabledAlways = _disabledCullUpdate = _disabledCull = 0;

        foreach (var a in _animators)
        {
            if (!a) continue;

            bool isActive = (a.enabled && a.gameObject.activeInHierarchy);

            switch (a.cullingMode)
            {
                case AnimatorCullingMode.AlwaysAnimate:
                    if (isActive) _activeAlways++; else _disabledAlways++;
                    break;

                case AnimatorCullingMode.CullUpdateTransforms:
                    if (isActive) _activeCullUpdate++; else _disabledCullUpdate++;
                    break;

                case AnimatorCullingMode.CullCompletely:
                    if (isActive) _activeCull++; else _disabledCull++;
                    break;
            }
        }
    }

    private void BuildDisplay()
    {
        _sb.Length = 0;

        _sb.AppendLine("<color=#00FFFF><b>ANIMATORS</b></color>");

        _sb.AppendLine("<color=#00FF00>Active:</color>");
        _sb.AppendFormat("  Always: {0}\n", _activeAlways);
        _sb.AppendFormat("  CullUpdate: {0}\n", _activeCullUpdate);
        _sb.AppendFormat("  Cull: {0}\n", _activeCull);

        _sb.AppendLine("<color=#FF4444>Disabled:</color>");
        _sb.AppendFormat("  Always: {0}\n", _disabledAlways);
        _sb.AppendFormat("  CullUpdate: {0}\n", _disabledCullUpdate);
        _sb.AppendFormat("  Cull: {0}\n", _disabledCull);

        if (_animUpdateRecorder.Valid && _animUpdateRecorder.IsRunning)
        {
            double ms = _animUpdateRecorder.LastValue * 1e-6;
            _sb.AppendFormat("<color=#FFA500>CPU:</color> {0:F3} ms\n", ms);
        }

        m_Display.text = _sb.ToString();
    }
}
