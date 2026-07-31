using System.Text;
using SAS.DevUtilities;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// View-only Animator statistics renderer shared by the Player and Debug Host.
/// </summary>
///
[AddComponentMenu("Dev Utilities/AnimatorStats/View")]
public sealed class AnimatorStats : UIBehaviour, IMiniToolSnapshotView<AnimatorStatsSnapshot>
{
    [SerializeField] private Text m_Display;

    private readonly StringBuilder _builder = new(256);

    public void ApplySnapshot(in AnimatorStatsSnapshot snapshot)
    {
        if (m_Display == null)
            return;

        _builder.Clear();
        _builder.AppendLine("<color=#00FFFF><b>ANIMATORS</b></color>")
            .AppendLine("<color=#00FF00>Active:</color>")
            .Append("  Always: ")
            .Append(snapshot.ActiveAlways)
            .AppendLine()
            .Append("  CullUpdate: ")
            .Append(snapshot.ActiveCullUpdate)
            .AppendLine()
            .Append("  Cull: ")
            .Append(snapshot.ActiveCullCompletely)
            .AppendLine()
            .AppendLine("<color=#FF4444>Disabled:</color>")
            .Append("  Always: ")
            .Append(snapshot.DisabledAlways)
            .AppendLine()
            .Append("  CullUpdate: ")
            .Append(snapshot.DisabledCullUpdate)
            .AppendLine()
            .Append("  Cull: ")
            .Append(snapshot.DisabledCullCompletely)
            .AppendLine();

        if (snapshot.HasCpuTiming)
        {
            _builder.Append("<color=#FFA500>CPU:</color> ")
                .Append(snapshot.CpuTimeMs.ToString("F3"))
                .AppendLine(" ms");
        }

        m_Display.text = _builder.ToString();
    }
}
