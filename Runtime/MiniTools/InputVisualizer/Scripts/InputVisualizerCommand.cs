using UnityEngine;
using SAS.Utilities.Presentation;

namespace SAS.Utilities.DeveloperConsole.InputVisualizers
{
    [CreateAssetMenu(fileName = "New InputVisualizer Command", menuName = DeveloperConsole.CommandBasePath + "InputVisualizer")]
    public class InputVisualizerCommand : CompositeConsoleCommand
    {
        public override string HelpText => $"Pass args 0|1 to toggle; TL, TR, BR, BL to anchor to screen edge";

        private const string ERROR_MESSAGE = "Args not valid";

        [SerializeField] private GameObject m_gamepadVisualizerPrefab;
        [SerializeField] private GameObject m_MouseVisualizerPrefab;

        private GameObject _gamepadVisualizerInstance;
        private GameObject _mouseVisualizerInstance;
        private DevUtilityPresentation _gamepadPresentation;
        private DevUtilityPresentation _mousePresentation;

        protected bool ShowGamepadVisualizer(string[] args)
        {
            if (args == null || args.Length == 0)
            {
                Debug.LogError(ERROR_MESSAGE);
                return false;
            }

            bool isSuccessfullyToggled = ToggleVisualizer(shouldToggleParam: args[0], prefab: m_gamepadVisualizerPrefab, instance: ref _gamepadVisualizerInstance, presentation: ref _gamepadPresentation, isActivated: out bool isActivated);

            if (!isSuccessfullyToggled)
            {
                return false;
            }

            if (isActivated)
            {
                return AnchorVisualizer(_gamepadVisualizerInstance, args[1]);
            }

            return true;
        }

        protected bool ShowMouseVisualizer(string[] args)
        {
            if (args == null || args.Length == 0)
            {
                Debug.LogError(ERROR_MESSAGE);
                return false;
            }

            bool isSuccessfullyToggled = ToggleVisualizer(shouldToggleParam: args[0], prefab: m_MouseVisualizerPrefab, instance: ref _mouseVisualizerInstance, presentation: ref _mousePresentation, isActivated: out bool isActivated);

            if (!isSuccessfullyToggled)
            {
                return false;
            }

            if (isActivated)
            {
                return AnchorVisualizer(_mouseVisualizerInstance, args[1]);
            }

            return true;
        }

        protected bool ToggleVisualizer(string shouldToggleParam, GameObject prefab, ref GameObject instance, ref DevUtilityPresentation presentation, out bool isActivated)
        {
            if (!BoolUtil.TryParse(shouldToggleParam, out isActivated))
            {
                Debug.LogError("Pass either 0 or 1 to toggle visualizers");
                return false;
            }

            if (instance == null)
            {
                instance = Instantiate(prefab);
                presentation = instance.GetComponent<DevUtilityPresentation>();
            }

            if (presentation == null)
                return false;

            presentation.SetRequestedVisible(isActivated);

            return true;
        }

        protected bool AnchorVisualizer(GameObject target, string positionParam)
        {
            InputVisualizerHandler visualizerHandler = target.GetComponent<InputVisualizerHandler>();

            if (visualizerHandler == null)
            {
                Debug.LogError("The visualizer component is not found in the instance root");
                return false;
            }

            if (string.IsNullOrEmpty(positionParam))
                return false;

            positionParam = positionParam.Trim().ToUpper();

            ScreenPosition screenPositon = positionParam switch
            {
                "TL" => ScreenPosition.TopLeft,
                "TR" => ScreenPosition.TopRight,
                "BR" => ScreenPosition.BottomRight,
                "BL" => ScreenPosition.BottomLeft,
                _ => ScreenPosition.None
            };

            if (screenPositon == ScreenPosition.None)
            {
                Debug.LogError("Valid screen position not found in the args. Pass TL, TR, BR or BL");
                return false;
            }

            visualizerHandler.AnchorToScreenEdge(screenPositon);
            return true;
        }
    }
}
