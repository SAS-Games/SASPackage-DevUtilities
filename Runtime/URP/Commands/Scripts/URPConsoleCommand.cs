#if UNITY_RENDER_PIPELINE_UNIVERSAL
using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace SAS.Utilities.DeveloperConsole
{
    [CreateAssetMenu(menuName = "SAS/DeveloperConsole/Commands/URP Console Command")]
    public class URPConsoleCommand : CompositeConsoleCommand
    {
        [SerializeField] private string m_HelpText = "Commands for modifying URP asset settings at runtime.";
        public override string HelpText => m_HelpText;

        private UniversalRenderPipelineAsset CurrentURPAsset
        {
            get
            {
                if (GraphicsSettings.defaultRenderPipeline is UniversalRenderPipelineAsset urp)
                    return urp;
                return null;
            }
        }

        protected override void CommandMethodRegistry()
        {
            Register("SetUpscalingFilter", SetUpscalingFilter);
            Register("SetRenderScale", SetRenderScale);
            Register("SetMSAA", SetMSAA);
            Register("SetHDR", SetHDR);
            Register("SetShadowDistance", SetShadowDistance);
            Register("SetShadowCascadeCount", SetShadowCascadeCount);
            Register("SetLOD", SetLOD);
            Register("SetColorGradingMode", SetColorGradingMode);
            Register("SetLUTSize", SetLUTSize);
            Register("VSync", SetVSync);
            Register("TextureQuality", SetTextureQuality);
        }


        private bool SetRenderScale(string[] args)
        {
            if (args.Length < 1 || !float.TryParse(args[0], out float val))
                return false;

            var urp = CurrentURPAsset;
            if (urp == null) return false;

            urp.renderScale = Mathf.Clamp(val, 0.1f, 2.0f);
            return true;
        }

        private bool SetUpscalingFilter(string[] args)
        {
            if (args.Length < 1)
                return false;

            var urp = CurrentURPAsset;
            if (urp == null)
                return false;

            if (Enum.TryParse<UpscalingFilterSelection>(args[0], true, out var filter))
            {
                urp.upscalingFilter = filter;
                return true;
            }
            else
                return false;
        }


        private bool SetMSAA(string[] args)
        {
            if (args.Length < 1 || !int.TryParse(args[0], out int val))
                return false;

            var urp = CurrentURPAsset;
            if (urp == null) return false;

            if (val != 0 && val != 2 && val != 4 && val != 8)
                return false;

            urp.msaaSampleCount = val;
            return true;
        }

        private bool SetHDR(string[] args)
        {
            if (args.Length < 1)
                return false;

            var urp = CurrentURPAsset;
            if (urp == null) return false;

            if (BoolUtil.TryParse(args[0], out bool value))
            {
                urp.supportsHDR = value;
                return true;
            }

            return false; // invalid input
        }

        private bool SetShadowDistance(string[] args)
        {
            if (args.Length < 1 || !float.TryParse(args[0], out float val))
                return false;

            var urp = CurrentURPAsset;
            if (urp == null) return false;

            urp.shadowDistance = Mathf.Max(0, val);
            return true;
        }

        private bool SetShadowCascadeCount(string[] args)
        {
            if (args.Length < 1 || !int.TryParse(args[0], out int count))
                return false;

            var urp = CurrentURPAsset;
            if (urp == null) return false;

            urp.shadowCascadeCount = Mathf.Clamp(count, 1, 4);
            return true;
        }


        private bool SetLOD(string[] args)
        {
            if (args.Length < 1 || !float.TryParse(args[0], out float val))
                return false;

            QualitySettings.lodBias = Mathf.Max(0.01f, val);
            return true;
        }


        private bool SetColorGradingMode(string[] args)
        {
            if (args.Length < 1 || !Enum.TryParse(args[0], true, out ColorGradingMode mode))
                return false;

            var urp = CurrentURPAsset;
            if (urp == null) return false;

            urp.colorGradingMode = mode;
            return true;
        }

        private bool SetLUTSize(string[] args)
        {
            if (args.Length < 1 || !int.TryParse(args[0], out int val))
                return false;

            var urp = CurrentURPAsset;
            if (urp == null) return false;

            // Valid LUT sizes are typically powers of two (16�65)
            if (val < 16 || val > 65)
                return false;

            urp.colorGradingLutSize = val;
            return true;
        }

        private bool SetVSync(string[] args)
        {
            if (args.Length < 1 || !int.TryParse(args[0], out int val))
                return false;
            QualitySettings.vSyncCount = Mathf.Clamp(val, 0, 2);
            return true;
        }

        private bool SetTextureQuality(string[] args)
        {
            if (args.Length < 1 || !int.TryParse(args[0], out int val))
                return false;
            QualitySettings.globalTextureMipmapLimit = Mathf.Clamp(val, 0, 3);
            return true;
        }
    }
}
#endif
