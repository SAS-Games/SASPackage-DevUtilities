using System.Text;
using SAS.DevUtilities;
using TMPro;
using UnityEngine;

/// <summary>
/// View-only renderer shared by the Player and Editor Debug Host.
/// </summary>
public sealed class GraphicsInfo :
    MonoBehaviour,
    IMiniToolSnapshotView<GraphicsInfoSnapshot>
{
    [SerializeField] private TMP_Text m_Display = default;

    public void ApplySnapshot(in GraphicsInfoSnapshot snapshot)
    {
        if (m_Display == null)
            return;

        var text = new StringBuilder(512);
        text.AppendLine("<b>Graphics Info</b>")
            .Append("GPU: ").AppendLine(snapshot.GraphicsDeviceName)
            .Append("VRAM: ").Append(snapshot.GraphicsMemorySizeMb)
            .AppendLine(" MB")
            .Append("API: ").AppendLine(snapshot.GraphicsApi)
            .Append("Quality: ").AppendLine(snapshot.QualityName)
            .Append("VSync: ").AppendLine(snapshot.VSyncCount.ToString())
            .Append("Shadows: ").AppendLine(snapshot.Shadows)
            .Append("LOD Bias: ").AppendLine(snapshot.LodBias.ToString())
            .Append("Target FPS: ")
            .AppendLine(
                snapshot.TargetFrameRate <= 0
                    ? "Platform Default"
                    : snapshot.TargetFrameRate.ToString());

        if (snapshot.HasRenderScale)
        {
            text.Append("Render Scale: ")
                .AppendLine(snapshot.RenderScale.ToString());
        }

        if (snapshot.Verbose)
        {
            text.AppendLine()
                .AppendLine("--- Extended Info ---")
                .Append("Render Resolution: ")
                .AppendLine(snapshot.RenderResolution)
                .Append("Screen Resolution: ")
                .AppendLine(snapshot.ScreenResolution)
                .Append("Mode: ")
                .AppendLine(snapshot.WindowMode)
                .Append("Anti-Aliasing: ")
                .AppendLine(snapshot.AntiAliasing)
                .Append("HDR: ")
                .AppendLine(snapshot.HdrEnabled.ToString())
                .Append("Anisotropic: ")
                .AppendLine(snapshot.AnisotropicFiltering);
        }

        m_Display.text = text.ToString();
    }
}
