using System;
using UnityEngine;

#if UNITY_RENDER_PIPELINE_HIGH_DEFINITION
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;
#endif

namespace SAS.Utilities.DeveloperConsole
{
    [CreateAssetMenu(menuName = DeveloperConsole.CommandBasePath + "HDRP Upscale Command")]
    public class HDRPQualityCommand : CompositeConsoleCommand
    {
        [SerializeField] private string m_HelpText = "Commands for modifying HDRP quality settings at runtime.";
        public override string HelpText => m_HelpText;

#if UNITY_RENDER_PIPELINE_HIGH_DEFINITION
        private HDRenderPipelineAsset CurrentHDRPAsset
        {
            get
            {
                if (GraphicsSettings.defaultRenderPipeline is HDRenderPipelineAsset hdrp)
                    return hdrp;
                return null;
            }
        }

        private bool SetUpscalingFilter(string[] args)
        {
            if (args.Length < 1)
                return false;

            var hdrp = CurrentHDRPAsset;
            if (hdrp == null)
                return false;

            if (Enum.TryParse<DynamicResUpscaleFilter>(args[0], true, out var filter))
            {
                if(args.Length > 1)
                {
                    for(int i = 1; i < args.Length; ++i)
                    {
                        Camera camera = GameObject.Find(args[i]).GetComponent<Camera>();
                        if(camera != null)
                        {
                            camera.allowDynamicResolution = true;
                            DynamicResolutionHandler.SetUpscaleFilter(camera, filter);
                        }
                    }
                }
                else
                {
                    Camera[] cameras = FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None);
                    foreach (Camera camera in cameras)
                    {
                        camera.allowDynamicResolution = true;
                        DynamicResolutionHandler.SetUpscaleFilter(camera, filter);
                    }
                }
                
                return true;
            }
            else
            {
                return false;
            }
        }

        private bool SetScreenPercentage(string[] args)
        {
            if (args.Length < 1)
                return false;

            var hdrp = CurrentHDRPAsset;
            if (hdrp == null)
                return false;

            float screenPercentage = Convert.ToSingle(args[0]);

            // Modify the dynamic resolution settings and re-assign
            var settings = CurrentHDRPAsset.currentPlatformRenderPipelineSettings;
            settings.dynamicResolutionSettings.forceResolution = true;
            settings.dynamicResolutionSettings.forcedPercentage = screenPercentage;
            CurrentHDRPAsset.currentPlatformRenderPipelineSettings = settings;

            return true;
        }

#else
        public override string Name => String.Empty;
        public override string[] Presets { get; } = new string[] { "" };
#endif
    }
}
