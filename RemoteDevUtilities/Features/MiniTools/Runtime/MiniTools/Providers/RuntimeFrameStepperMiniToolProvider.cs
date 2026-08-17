using SAS.DevUtilities;
using SAS.Utilities.RemoteDevUtilities.Protocol.MiniTools;
using UnityEngine;
using UnityEngine.Scripting;

namespace SAS.Utilities.RemoteDevUtilities.MiniTools.Providers
{
    /// <summary>
    /// Publishes FrameStepper state and applies its registered controls inside
    /// the connected Player. It does not create or depend on Player UI.
    /// </summary>
    [Preserve]
    internal sealed class RuntimeFrameStepperMiniToolProvider : MiniToolDataProvider<FrameStepperSnapshot>, IMiniToolFieldProvider
    {
        private readonly FrameStepperTimeController _timeController = new();

        public override RemoteMiniToolActionDescriptor[] GetActions()
        {
            return new[]
            {
                Action(FrameStepperActionIds.Toggle, "Play / Pause"),
                Action(FrameStepperActionIds.Step, "Step")
            };
        }

        public override void Start()
        {
            _timeController.Begin();
        }

        public override void Stop()
        {
            _timeController.Release();
        }

        public override void Tick()
        {
            _timeController.Tick();
        }

        public override bool TryGetSnapshot(out FrameStepperSnapshot snapshot)
        {
            snapshot = FrameStepperSnapshotCollector.Capture();
            return true;
        }

        public RemoteMiniToolField[] CaptureFields()
        {
            FrameStepperSnapshot snapshot = FrameStepperSnapshotCollector.Capture();
            return new[]
            {
                CreateField("state", "State", snapshot.IsPaused ? "Paused" : "Running"),
                CreateField("timeScale", "Time Scale", snapshot.TimeScale.ToString("0.###"))
            };
        }

        public override bool TryExecuteAction(string actionId, out string error)
        {
            switch (actionId)
            {
                case FrameStepperActionIds.Toggle:
                    if (Time.timeScale > 0f)
                        _timeController.Pause();
                    else
                        _timeController.Resume();
                    error = string.Empty;
                    return true;
                case FrameStepperActionIds.Step:
                    if (!_timeController.TryStep())
                    {
                        error = "Pause the Player before stepping a frame.";
                        return false;
                    }

                    error = string.Empty;
                    return true;
                default:
                    error = "The requested FrameStepper action is not available.";
                    return false;
            }
        }

        private static RemoteMiniToolActionDescriptor Action(string id, string displayName)
        {
            return new RemoteMiniToolActionDescriptor
            {
                Id = id,
                DisplayName = displayName
            };
        }
    }
}
