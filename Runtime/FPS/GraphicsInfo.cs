using TMPro;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class GraphicsInfo : MonoBehaviour
{
    [SerializeField] private TMP_Text m_Display = default;

    public void Show(bool status, bool verbose)
    {
        m_Display.text = Info(verbose);
        gameObject.SetActive(status);
    }

    private string Info(bool verbose)
    {
        // Graphics API & GPU Info
        string api = SystemInfo.graphicsDeviceType.ToString();
        string gpu = SystemInfo.graphicsDeviceName;
        int vram = SystemInfo.graphicsMemorySize;

        // Quality Settings
        string quality = QualitySettings.names[QualitySettings.GetQualityLevel()];
        int vSync = QualitySettings.vSyncCount;
        var shadows = QualitySettings.shadows;
        float lodBias = QualitySettings.lodBias;

        // Application Target FPS
        int fpsTarget = Application.targetFrameRate;

        // Render Scale (URP only)
        float renderScale = -1f;
        if (GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset urp)
            renderScale = urp.renderScale;

        // Build base summary
        string info =
            $"<b>Graphics Info</b>\n" +
            $"GPU: {gpu}\n" +
            $"VRAM: {vram} MB\n" +
            $"API: {api}\n" +
            $"Quality: {quality}\n" +
            $"VSync: {vSync}\n" +
            $"Shadows: {shadows}\n" +
            $"LOD Bias: {lodBias}\n" +
            $"Target FPS: {(fpsTarget <= 0 ? "Platform Default" : fpsTarget.ToString())}\n" +
            (renderScale > 0 ? $"Render Scale: {renderScale}\n" : "");

        // Add extended info if verbose mode
        if (verbose)
        {
            string resolution = $"{Screen.currentResolution.width}x{Screen.currentResolution.height} @ {Screen.currentResolution.refreshRateRatio}Hz";
            string fullscreen = Screen.fullScreen ? "Fullscreen" : "Windowed";

            string anisotropic = QualitySettings.anisotropicFiltering.ToString();
            string aa = QualitySettings.antiAliasing > 0 ? $"{QualitySettings.antiAliasing}x MSAA" : "None";
            bool hdr = Camera.main != null && Camera.main.allowHDR;

            info +=
                "\n--- Extended Info ---\n" +
                $"Resolution: {resolution}\n" +
                $"Mode: {fullscreen}\n" +
                $"Anti-Aliasing: {aa}\n" +
                $"HDR: {hdr}\n" +
                $"Anisotropic: {anisotropic}\n";
        }
        return info;
    }
}
