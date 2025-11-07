using UnityEngine;

namespace SAS.Utilities.DeveloperConsole
{
    [CreateAssetMenu(fileName = "New Show FPS Command", menuName = "HP/DeveloperConsole/Commands/Show FPS Command PS")]
    public class ShowFPSCommandPS : ShowFPSCommand
    {
        protected override bool SetTargetFrameRate(string[] args)
        {
            if (args.Length < 1 || !int.TryParse(args[0], out int val))
                return false;

            Application.targetFrameRate = Mathf.Max(val, -1);
            if (Application.targetFrameRate < 0)
                QualitySettings.vSyncCount = 0;
            else if (Application.targetFrameRate <= 30)
                QualitySettings.vSyncCount = 2;
            else if (Application.targetFrameRate <= 60)
                QualitySettings.vSyncCount = 1;

            return true;
        }
    }
}