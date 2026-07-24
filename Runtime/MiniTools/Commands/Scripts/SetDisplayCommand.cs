using UnityEngine;
using UnityEngine.SceneManagement;

namespace SAS.Utilities.DeveloperConsole
{
    [CreateAssetMenu(fileName = "New SetDisplay Command", menuName =  DeveloperConsole.CommandBasePath + "SetDisplay Command")]
    public class SetDisplayCommand : ConsoleCommand
    {
        public override string HelpText =>
            "Usage: SetCanvasDisplay <canvasName> <displayIndex>.\nExample: SetCanvasDisplay DebugCanvas 1.\n" +
            "Moves the specified UI Canvas to the given display.";

        public override bool Process(DeveloperConsoleBehaviour developerConsole, string command, string[] args)
        {
            if (args == null || args.Length < 2)
            {
                Debug.LogError("SetDisplay requires 2 arguments: canvasName and displayIndex.");
                return false;
            }

            string canvasName = string.Join(" ", args, 0, args.Length - 1);
            if (!int.TryParse(args[^1], out int displayIndex))
            {
                Debug.LogError("Invalid display index. Provide a number.");
                return false;
            }

            if (displayIndex < 0 || displayIndex >= Display.displays.Length)
            {
                Debug.LogError($"Display {displayIndex} not available. Total displays: {Display.displays.Length}");
                return false;
            }

            Canvas targetCanvas = FindCanvasByName(canvasName);
            if (targetCanvas == null)
            {
                Debug.LogError($"Canvas '{canvasName}' not found in loaded scenes.");
                return false;
            }

            // Activate only after all input and target validation has succeeded.
            Display.displays[displayIndex].Activate();

            // Set canvas to render on the target display
            targetCanvas.targetDisplay = displayIndex;
            Debug.Log($"Canvas '{canvasName}' is now displayed on Display {displayIndex}.");

            return true;
        }

        private static Canvas FindCanvasByName(string canvasName)
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
