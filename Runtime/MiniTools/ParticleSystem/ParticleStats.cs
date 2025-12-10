using System.Text;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.UI;

public class ParticleStats : MonoBehaviour
{
    [SerializeField] private Text m_Display;
    [SerializeField] private float m_UpdateInterval = 1f;

    private readonly StringBuilder sb = new StringBuilder(256);

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
        sb.Length = 0;

        sb.AppendLine("<color=#00FFFF><b>PARTICLES</b></color>");

        sb.AppendLine("<color=#00FF00>Active:</color>");
        sb.AppendFormat("  Count: {0}\n", _activeAlive);

        sb.AppendLine("<color=#FF4444>Disabled:</color>");
        sb.AppendFormat("  Count: {0}\n", _disabled);

        if (_psRecorder.Valid && _psRecorder.IsRunning)
        {
            sb.AppendLine();
            sb.AppendLine("<color=#FFA500>CPU:</color>");
            float ms = _psRecorder.LastValue * 1e-6f;
            sb.AppendFormat("  Update Cost: {0:F3} ms\n", ms);
        }

        m_Display.text = sb.ToString();
    }
}
