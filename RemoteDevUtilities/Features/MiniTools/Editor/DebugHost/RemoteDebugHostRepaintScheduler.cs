using UnityEditor;
using UnityEditorInternal;

namespace HP.Utilities.RemoteDevUtilities.Editor.DebugHost
{
    /// <summary>
    /// Coalesces live remote presentation repaints. Editor transport callbacks
    /// continue while Unity is unfocused, but a Play Mode Game view does not
    /// necessarily repaint until explicitly invalidated.
    /// </summary>
    internal static class RemoteDebugHostRepaintScheduler
    {
        private static bool _queued;

        internal static void Request()
        {
            EditorApplication.QueuePlayerLoopUpdate();
            if (_queued)
                return;

            _queued = true;
            EditorApplication.delayCall += Repaint;
        }

        private static void Repaint()
        {
            _queued = false;
            InternalEditorUtility.RepaintAllViews();
        }
    }
}
