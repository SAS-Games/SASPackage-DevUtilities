using System.Text;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.UI;

public class ParticleStats : MonoBehaviour
{
    [SerializeField] private Text m_Display;
    [SerializeField] private float m_UpdateInterval = 1f;

    private readonly StringBuilder _sb = new StringBuilder(256);

    private ParticleSystem[] _particles;
    private ProfilerRecorder _psRecorder;

    private float _timer;
    private int _activeAlive, _disabled;

    private void OnEnable()
    {
        _psRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Particles,"ParticleSystem.Update");
        _particles = FindObjectsByType<ParticleSystem>(FindObjectsInactive.Include, FindObjectsSortMode.None);
    }

    private void OnDisable()
    {
        _psRecorder.Dispose();
    }

    private void Update()
    {
        _timer -= Time.deltaTime;
        if (_timer > 0) return;
        _timer = m_UpdateInterval;

        UpdateParticleStats();
        UpdateDisplay();
    }

    private void UpdateParticleStats()
    {
        _activeAlive = 0;
        _disabled = 0;

        foreach (var ps in _particles)
        {
            if (!ps) 
                continue;

            bool isEnabled = ps.gameObject.activeInHierarchy;

            if (isEnabled)
            {
                if (ps.IsAlive(false))
                    _activeAlive++;
            }
            else
                _disabled++;
        }
    }

    private void UpdateDisplay()
    {
        _sb.Length = 0;

        _sb.AppendLine("<color=#00FFFF><b>PARTICLES</b></color>");

        _sb.AppendLine("<color=#00FF00>Active:</color>");
        _sb.AppendFormat("  Count: {0}\n", _activeAlive);

        _sb.AppendLine("<color=#FF4444>Disabled:</color>");
        _sb.AppendFormat("  Count: {0}\n", _disabled);

        if (_psRecorder.Valid && _psRecorder.IsRunning)
        {
            float ms = _psRecorder.LastValue * 1e-6f;
            _sb.AppendFormat("<color=#FFA500>CPU:</color> {0:F3} ms\n", ms);
        }

        m_Display.text = _sb.ToString();
    }
}
