using System.Text;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace SAS.DevUtilities
{
    /// <summary>
    /// View-only component for the lightweight FPS presentation.
    /// </summary>
    [AddComponentMenu("Dev Utilities/FPS/view")]
    public sealed class FPS : UIBehaviour, IMiniToolSnapshotView<FPSSnapshot>
    {
        private const string ColorRed = "<color=#FF0000>";
        private const string ColorYellow = "<color=#FFFF00>";
        private const string ColorWhite = "<color=#FFFFFF>";
        private const string ColorGreen = "<color=#00FF00>";
        private const string ColorEnd = "</color>";

        [Header("Display")] [SerializeField] private Text m_Display;

        private readonly StringBuilder _builder = new StringBuilder(128);

        public void ApplySnapshot(in FPSSnapshot snapshot)
        {
            if (m_Display == null)
                return;

            _builder.Length = 0;

            AppendFps(snapshot.AverageFps);
            _builder.Append('\n');
            AppendFrameTime(snapshot);

            m_Display.text = _builder.ToString();
        }

        private void AppendFps(double averageFps)
        {
            _builder.Append(GetFpsColor(averageFps)).Append("FPS: ").Append(averageFps.ToString("F1")).Append(ColorEnd);
        }

        private void AppendFrameTime(in FPSSnapshot snapshot)
        {
            _builder.Append(snapshot.IsFrameTimeOverBudget ? ColorRed : ColorWhite).Append("Frame Time: ").Append(snapshot.AverageFrameTimeMs.ToString("F2")).Append(" ms").Append(ColorEnd);
        }

        private static string GetFpsColor(double averageFps)
        {
            if (averageFps < 10d)
                return ColorRed;

            if (averageFps < 30d)
                return ColorYellow;

            return ColorGreen;
        }
    }
}
