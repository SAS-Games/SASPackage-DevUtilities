using UnityEngine;
using UnityEngine.SceneManagement;

namespace SAS.Utilities.DeveloperConsole
{
    [CreateAssetMenu(fileName = "New SetDisplay Command", menuName = "SAS/DeveloperConsole/Commands/SetDisplay Command")]
    public class SetDisplayCommand : ConsoleCommand
    {
        public override string HelpText =>
            "Usage: SetDisplay [canvasName] [displayIndex]. Example: SetDisplay DebugCanvas 1. " +
            "Moves the specified UI Canvas to the given display.";

        public override bool Process(DeveloperConsoleBehaviour developerConsole, string command, string[] args)
        {
            if (args == null || args.Length < 2)
            {
                Debug.LogError("SetDisplay requires 2 arguments: canvasName and displayIndex.");
                return false;
            }

            string canvasName = args[0];
            if (!int.TryParse(args[1], out int displayIndex))
            {
                Debug.LogError("Invalid display index. Provide a number.");
                return false;
            }

            if (displayIndex >= Display.displays.Length)
            {
                Debug.LogError($"Display {displayIndex} not available. Total displays: {Display.displays.Length}");
                return false;
            }

            // Activate the display if not main
            Display.displays[displayIndex].Activate();

            Canvas targetCanvas = FindCanvasByName(canvasName);
            if (targetCanvas == null)
            {
                Debug.LogError($"Canvas '{canvasName}' not found in loaded scenes.");
                return false;
            }

            // Set canvas to render on the target display
            targetCanvas.targetDisplay = displayIndex;
            Debug.Log($"Canvas '{canvasName}' is now displayed on Display {displayIndex}.");

            return true;
        }

        private Canvas FindCanvasByName(string canvasName)
        {
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                Scene scene = SceneManager.GetSceneAt(i);
                if (!scene.isLoaded) continue;

                foreach (GameObject root in scene.GetRootGameObjects())
                {
                    Canvas[] canvases = root.GetComponentsInChildren<Canvas>(true);
                    foreach (var canvas in canvases)
                    {
                        if (canvas.name.Equals(canvasName, System.StringComparison.OrdinalIgnoreCase))
                            return canvas;
                    }
                }
            }
            return null;
        }
    }
}