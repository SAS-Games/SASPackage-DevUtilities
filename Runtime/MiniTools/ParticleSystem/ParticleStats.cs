using System.Text;
using SAS.DevUtilities;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// View-only particle statistics renderer shared by the Player and Debug
/// Host.
/// </summary>
[DisallowMultipleComponent]
[AddComponentMenu("Dev Utilities/ParticleStats/View")]
public sealed class ParticleStats : MonoBehaviour, IMiniToolSnapshotView<ParticleStatsSnapshot>
{
    [SerializeField] private Text m_Display;

    private readonly StringBuilder _builder = new(256);

    public void ApplySnapshot(in ParticleStatsSnapshot snapshot)
    {
        if (m_Display == null)
            return;

        _builder.Clear();
        _builder.AppendLine("<color=#00FFFF><b>PARTICLES</b></color>").AppendLine("<color=#00FF00>Systems:</color>").Append("  Total: ").Append(snapshot.TotalSystems).AppendLine().Append("  Active: ").Append(snapshot.ActiveSystems).AppendLine().Append("  Alive: ").Append(snapshot.AliveSystems).AppendLine().AppendLine("<color=#FF4444>Disabled:</color>").Append("  Count: ").Append(snapshot.DisabledSystems).AppendLine().Append("<color=#00FFFF>Live Particles:</color> ").Append(snapshot.LiveParticles).AppendLine();

        if (snapshot.HasCpuTiming)
        {
            _builder.Append("<color=#FFA500>CPU:</color> ").Append(snapshot.CpuTimeMs.ToString("F3")).AppendLine(" ms");
        }

        m_Display.text = _builder.ToString();
    }
}
