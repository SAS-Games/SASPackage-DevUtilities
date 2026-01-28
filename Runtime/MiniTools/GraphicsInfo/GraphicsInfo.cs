using TMPro;
using UnityEngine;
using UnityEngine.Rendering;
#if UNITY_RENDER_PIPELINE_UNIVERSAL
using UnityEngine.Rendering.Universal;
#endif

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

        // Quality Settings (shared)
        string quality = QualitySettings.names[QualitySettings.GetQualityLevel()];
        int vSync = QualitySettings.vSyncCount;
        float lodBias = QualitySettings.lodBias;

        // Application Target FPS
        int fpsTarget = Application.targetFrameRate;

        // Defaults (classic pipeline)
        string shadows = QualitySettings.shadows.ToString();
        string aa = QualitySettings.antiAliasing > 0 ? $"{QualitySettings.antiAliasing}x MSAA" : "None";
        bool hdr = Camera.main != null && Camera.main.allowHDR;
        float renderScale = -1f;
        string anisotropic = QualitySettings.anisotropicFiltering.ToString();

        // If URP is active, override with URP asset values
#if UNITY_RENDER_PIPELINE_UNIVERSAL

        if (GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset urp)
        {
            shadows = urp.supportsMainLightShadows ? "Enabled" : "Disabled";
            renderScale = urp.renderScale;
            aa = urp.msaaSampleCount > 1 ? $"{urp.msaaSampleCount}x MSAA" : "None";
            hdr = urp.supportsHDR;
        }
#endif

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
            string renderResolution = $"{Screen.width}x{Screen.height}";
            string screenResolution = $"{Screen.currentResolution.width}x{Screen.currentResolution.height} @ {Screen.currentResolution.refreshRateRatio}Hz";
            string fullscreen = Screen.fullScreen ? "Fullscreen" : "Windowed";

            info +=
                "\n--- Extended Info ---\n" +
                $"Render Resolution: {renderResolution}\n"+
                $"Screen Resolution: {screenResolution}\n" +
                $"Mode: {fullscreen}\n" +
                $"Anti-Aliasing: {aa}\n" +
                $"HDR: {hdr}\n" +
                $"Anisotropic: {anisotropic}\n";
        }
        return info;
    }

}
