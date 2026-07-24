using UnityEngine;

namespace SAS.Utilities.DeveloperConsole
{
    [CreateAssetMenu(fileName = "New Show FPS Command PS", menuName = DeveloperConsole.CommandBasePath + "Show FPS Command PS")]
    public class ShowFPSCommandPS : ShowFPSCommand
    {
        protected override bool SetTargetFrameRate(string[] args)
        {
#if !UNITY_EDITOR && !DEVELOPMENT_BUILD && !ENABLE_DEBUG
            return false;
#else
            if (!TryParseTargetFrameRate(args, out int targetFrameRate))
                return false;

            Application.targetFrameRate = targetFrameRate;
            
            if (targetFrameRate < 0)
                QualitySettings.vSyncCount = 0;
            else if (targetFrameRate <= 30)
                QualitySettings.vSyncCount = 2;
            else if (targetFrameRate <= 60)
                QualitySettings.vSyncCount = 1;

            return true;
#endif
        }
    }
}
