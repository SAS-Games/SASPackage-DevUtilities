using System;
using SAS.DevUtilities;
using UnityEngine;
using UnityEngine.Serialization;

namespace SAS.Utilities.DeveloperConsole.InputVisualizers
{
    public enum ScreenPosition
    {
        None,
        TopLeft,
        TopRight,
        BottomRight,
        BottomLeft
    }

    public class InputVisualizerHandler : MonoBehaviour, IMiniToolSnapshotView<InputVisualizerSnapshot>, IMiniToolStreamView<InputVisualizerSampleEvent>, IMiniToolLocalController
    {
        [SerializeField] private InputVisualizer m_rootVisualizer;

        [SerializeField, FormerlySerializedAs("_height")] private float m_height;

        [SerializeField, FormerlySerializedAs("_width")] private float m_width;

        [SerializeField, FormerlySerializedAs("_widthPadding")] private float m_widthPadding;

        [SerializeField, FormerlySerializedAs("_heightPadding")] private float m_heightPadding;

        private Vector2 _topLeft, _topRight, _bottomLeft, _bottomRight;
        private InputControlVisualizer[] _visualizers;

        private void OnEnable()
        {
            SetLocalInputEnabled(true);
        }

        private void OnDisable()
        {
            SetLocalInputEnabled(false);
        }

        public void ApplySnapshot(in InputVisualizerSnapshot snapshot)
        {
            SetLocalInputEnabled(false);
            ApplyDeviceName(snapshot.CurrentDeviceName);
            ApplyValues(snapshot.Controls);
        }

        public void ApplyEvents(InputVisualizerSampleEvent[] events, int droppedEventCount)
        {
            SetLocalInputEnabled(false);
            foreach (InputVisualizerSampleEvent sampleEvent in events ?? Array.Empty<InputVisualizerSampleEvent>())
            {
                if (sampleEvent.DeviceChanged)
                    ApplyDeviceName(sampleEvent.CurrentDeviceName);
                ApplyValues(sampleEvent.Controls);
            }
        }

        public void AnchorToScreenEdge(ScreenPosition screenPosition)
        {
            _topLeft = new Vector2(m_widthPadding, m_heightPadding);
            _topRight = new Vector2(Screen.width - m_width - m_widthPadding, m_heightPadding);
            _bottomRight = new Vector2(Screen.width - m_width - m_widthPadding, Screen.height - m_height - m_heightPadding);
            _bottomLeft = new Vector2(m_widthPadding, Screen.height - m_height - m_heightPadding);

            m_rootVisualizer.m_Rect.position = screenPosition switch
            {
                ScreenPosition.TopLeft => _topLeft,
                ScreenPosition.TopRight => _topRight,
                ScreenPosition.BottomLeft => _bottomLeft,
                ScreenPosition.BottomRight => _bottomRight,
                _ => Vector2.zero
            };
        }

        private void ApplyValues(InputVisualizerControlValue[] values)
        {
            ResolveVisualizers();
            double time = Time.realtimeSinceStartupAsDouble;
            foreach (InputVisualizerControlValue value in values ?? Array.Empty<InputVisualizerControlValue>())
            {
                foreach (InputControlVisualizer visualizer in _visualizers)
                    visualizer.ApplyRemoteValue(in value, time);
            }
        }

        private void ApplyDeviceName(string deviceName)
        {
            ResolveVisualizers();
            foreach (InputControlVisualizer visualizer in _visualizers)
                visualizer.ApplyRemoteDeviceName(deviceName);
        }

        private void SetLocalInputEnabled(bool enabled)
        {
            ResolveVisualizers();
            foreach (InputControlVisualizer visualizer in _visualizers)
                visualizer.SetLocalInputEnabled(enabled);
        }

        private void ResolveVisualizers()
        {
            _visualizers ??= GetComponentsInChildren<InputControlVisualizer>(true);
        }

#if UNITY_EDITOR
        private void Reset()
        {
            m_rootVisualizer ??= GetComponent<InputVisualizer>();
        }

        [ContextMenu("Anchor Top Left")] private void AnchorTopLeft() => AnchorToScreenEdge(ScreenPosition.TopLeft);
        [ContextMenu("Anchor Top Right")] private void AnchorTopRight() => AnchorToScreenEdge(ScreenPosition.TopRight);
        [ContextMenu("Anchor Bottom Right")] private void AnchorBottomRight() => AnchorToScreenEdge(ScreenPosition.BottomRight);
        [ContextMenu("Anchor Bottom Left")] private void AnchorBottomLeft() => AnchorToScreenEdge(ScreenPosition.BottomLeft);

#endif
    }
}
