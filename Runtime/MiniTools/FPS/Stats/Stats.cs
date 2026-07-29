using System.Text;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace SAS.DevUtilities.Stats
{
    /// <summary>
    /// View-only component for the Stats prefab.
    /// </summary>
    public sealed class Stats : UIBehaviour, IMiniToolSnapshotView<StatsSnapshot>
    {
        private const double BytesPerGibibyte = 1073741824d;

        [Header("Display")]
        [SerializeField] private Text m_Display;
        private readonly StringBuilder _builder = new StringBuilder(512);

        /// <summary>
        /// Displays the supplied snapshot without collecting or changing data.
        /// </summary>
        public void ApplySnapshot(in StatsSnapshot snapshot)
        {
            if (m_Display == null)
                return;

            _builder.Length = 0;

            AppendFps(snapshot.AverageFps);
            AppendAverageFrameTime(snapshot.AverageFrameTimeMs);
            AppendTargetFrameRate(snapshot.TargetFrameRate);
            AppendVSyncCount(snapshot.VSyncCount);
            AppendFrameTiming(snapshot);
            AppendMemory(snapshot);

            m_Display.text = _builder.ToString();
        }

        private void AppendFps(double averageFps)
        {
            Color fpsColor = GetFpsColor(averageFps);
            _builder.Append("<color=#").Append(ColorUtility.ToHtmlStringRGB(fpsColor)).Append(">FPS: ").Append(averageFps.ToString("F1")).Append("</color>\n");
        }

        private void AppendAverageFrameTime(double averageFrameTimeMs)
        {
            _builder.AppendFormat("Average Frame Time: {0:F2} ms\n", averageFrameTimeMs);
        }

        private void AppendTargetFrameRate(int targetFrameRate)
        {
            _builder.Append("Target FPS: ");

            if (targetFrameRate < 0)
                _builder.Append("Platform Default (-1)");
            else
                _builder.Append(targetFrameRate);

            _builder.Append('\n');
        }

        private void AppendVSyncCount(int vSyncCount)
        {
            _builder.AppendFormat("VSync Count: {0}\n", vSyncCount);
        }

        private void AppendFrameTiming(in StatsSnapshot snapshot)
        {
            if (!snapshot.HasFrameTiming)
            {
                _builder.Append("Detailed Frame Timing: unavailable\n");
                return;
            }

            _builder.AppendFormat("Latest CPU Frame: {0:F3} ms\n", snapshot.CpuFrameTimeMs);
            _builder.AppendFormat("Latest CPU Main Thread: {0:F3} ms\n", snapshot.CpuMainThreadFrameTimeMs);
            _builder.AppendFormat("Latest CPU Render Thread: {0:F3} ms\n", snapshot.CpuRenderThreadFrameTimeMs);
            _builder.AppendFormat("Latest CPU Present Wait: {0:F3} ms\n", snapshot.CpuPresentWaitTimeMs);
            _builder.AppendFormat("Latest GPU Frame: {0:F3} ms\n", snapshot.GpuFrameTimeMs);
        }

        private void AppendMemory(in StatsSnapshot snapshot)
        {
            _builder.AppendFormat("Allocated: {0:F3} GiB\n", snapshot.AllocatedMemoryBytes / BytesPerGibibyte);
            _builder.AppendFormat("Reserved: {0:F3} GiB\n", snapshot.ReservedMemoryBytes / BytesPerGibibyte);
            _builder.AppendFormat("Unused: {0:F3} GiB", snapshot.UnusedReservedMemoryBytes / BytesPerGibibyte);
        }

        private static Color GetFpsColor(double averageFps)
        {
            if (averageFps < 10d)
                return Color.red;

            if (averageFps < 30d)
                return Color.yellow;

            return Color.green;
        }
    }
}
