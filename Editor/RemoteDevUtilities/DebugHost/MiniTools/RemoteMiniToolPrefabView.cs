using System;
using System.Collections.Generic;
using System.Text;
using SAS.DevUtilities;
using SAS.Utilities.RemoteDevUtilities.MiniTools;
using SAS.Utilities.RemoteDevUtilities.Protocol.MiniTools;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace SAS.Utilities.RemoteDevUtilities.Editor.DebugHost.MiniTools
{
    internal sealed class RemoteMiniToolPrefabView : IDisposable
    {
        private readonly StringBuilder _builder = new(512);
        private readonly GameObject _instance;
        private readonly Text _legacyText;
        private readonly TMP_Text _tmpText;
        private readonly IRemoteMiniToolSnapshotView[] _snapshotViews =
            Array.Empty<IRemoteMiniToolSnapshotView>();
        private readonly IRemoteMiniToolStreamView[] _streamViews =
            Array.Empty<IRemoteMiniToolStreamView>();
        private readonly IRemoteMiniToolPresentation[] _customPresentations =
            Array.Empty<IRemoteMiniToolPresentation>();
        private readonly MiniToolActionRelay[] _actionRelays =
            Array.Empty<MiniToolActionRelay>();
        private readonly Action<string> _actionRequested;
        public string FailureReason { get; }

        public RemoteMiniToolPrefabView(
            RemoteMiniToolPrefabDefinition definition,
            int layoutIndex,
            Action<string> actionRequested)
        {
            _actionRequested = actionRequested;
            if (string.IsNullOrWhiteSpace(definition.AssetPath))
            {
                _instance = CreateGenericView(definition.ToolId);
                _legacyText = _instance.GetComponentInChildren<Text>(true);
                Object.DontDestroyOnLoad(_instance);
                InitializeCanvas(_instance, layoutIndex);
                ApplyLayout(layoutIndex);
                _instance.SetActive(true);
                return;
            }

            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(definition.AssetPath);
            if (prefab == null)
            {
                FailureReason = $"Prefab was not found at '{definition.AssetPath}'.";
                return;
            }

            _instance = Object.Instantiate(prefab);
            _instance.name = $"[Remote Mini Tool] {definition.ToolId}";
            Object.DontDestroyOnLoad(_instance);
            DisableLocalDataFlow(_instance);
            InitializeCanvas(_instance, layoutIndex);
            _legacyText = _instance.GetComponentInChildren<Text>(true);
            _tmpText = _instance.GetComponentInChildren<TMP_Text>(true);
            _snapshotViews =
                RemoteMiniToolSnapshotViewFactory.Find(_instance);
            _streamViews =
                RemoteMiniToolStreamViewFactory.Find(_instance);
            _customPresentations = FindCustomPresentations(_instance);
            _actionRelays =
                _instance.GetComponentsInChildren<MiniToolActionRelay>(true);
            foreach (MiniToolActionRelay relay in _actionRelays)
                relay.ActionRequested += OnActionRequested;
            if (_legacyText == null &&
                _tmpText == null &&
                _snapshotViews.Length == 0 &&
                _streamViews.Length == 0 &&
                _customPresentations.Length == 0)
            {
                FailureReason =
                    $"Prefab '{definition.AssetPath}' does not contain a supported Text component " +
                    $"or mini-tool view implementation.";
                return;
            }

            if (_legacyText != null)
                _legacyText.enabled = true;
            if (_tmpText != null)
                _tmpText.enabled = true;
            ApplyLayout(layoutIndex);
            _instance.SetActive(true);
        }

        public bool IsValid =>
            _instance != null &&
            (_legacyText != null ||
             _tmpText != null ||
             _snapshotViews.Length > 0 ||
             _streamViews.Length > 0 ||
             _customPresentations.Length > 0);

        public void Update(RemoteMiniToolDescriptor descriptor, RemoteMiniToolSample sample)
        {
            if (!IsValid)
                return;

            if (_snapshotViews.Length > 0)
            {
                foreach (IRemoteMiniToolSnapshotView snapshotView in _snapshotViews)
                {
                    try
                    {
                        if (snapshotView.TryApply(sample))
                            return;
                    }
                    catch (Exception exception)
                    {
                        Debug.LogException(exception);
                    }
                }

                // A contract-based prefab must only be rendered by its own
                // IMiniToolSnapshotView<TSnapshot>. Wait for a compatible typed sample
                // instead of replacing its UI with the generic field renderer.
                return;
            }

            if (_customPresentations.Length > 0)
            {
                foreach (IRemoteMiniToolPresentation presentation in
                         _customPresentations)
                {
                    try
                    {
                        presentation.ApplySample(descriptor, sample);
                    }
                    catch (Exception exception)
                    {
                        Debug.LogException(exception);
                    }
                }
                return;
            }

            _builder.Clear();
            _builder.Append("<b>").Append(descriptor?.DisplayName ?? sample?.ToolId ?? "Remote Mini Tool").AppendLine("</b>");

            foreach (RemoteMiniToolField field in sample?.Fields ?? Array.Empty<RemoteMiniToolField>())
            {
                _builder.Append(field.DisplayName ?? field.Name).Append(": ").Append(field.Value);
                if (!string.IsNullOrWhiteSpace(field.Unit))
                    _builder.Append(' ').Append(field.Unit);
                _builder.AppendLine();
            }

            if (sample != null)
                _builder.Append("Frame: ").Append(sample.Frame);

            if (_legacyText != null)
                _legacyText.text = _builder.ToString();
            if (_tmpText != null)
                _tmpText.text = _builder.ToString();
        }

        public void Dispose()
        {
            foreach (MiniToolActionRelay relay in _actionRelays)
            {
                if (relay != null)
                    relay.ActionRequested -= OnActionRequested;
            }

            if (_instance != null)
                Object.Destroy(_instance);
        }

        private void OnActionRequested(string actionId)
        {
            _actionRequested?.Invoke(actionId);
        }

        public void ApplyStream(
            RemoteMiniToolStreamBatch batch)
        {
            if (!IsValid || batch == null)
                return;

            foreach (IRemoteMiniToolStreamView streamView in
                     _streamViews)
            {
                try
                {
                    if (streamView.TryApply(batch))
                        return;
                }
                catch (Exception exception)
                {
                    Debug.LogException(exception);
                }
            }
        }

        private static void DisableLocalDataFlow(GameObject instance)
        {
            foreach (MonoBehaviour behaviour in
                     instance.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (RemoteMiniToolSnapshotViewFactory.IsSnapshotProvider(behaviour) ||
                    behaviour is IMiniToolLocalController)
                {
                    behaviour.enabled = false;
                }
            }

            // Legacy mini-tools have not adopted IMiniToolSnapshotProvider yet.
            Disable<ParticleStats>(instance);
        }

        private static void Disable<T>(GameObject instance) where T : Behaviour
        {
            foreach (T behaviour in instance.GetComponentsInChildren<T>(true))
                behaviour.enabled = false;
        }

        private static IRemoteMiniToolPresentation[] FindCustomPresentations(
            GameObject instance)
        {
            var presentations = new List<IRemoteMiniToolPresentation>();
            foreach (MonoBehaviour behaviour in
                     instance.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (behaviour is IRemoteMiniToolPresentation presentation)
                    presentations.Add(presentation);
            }

            return presentations.ToArray();
        }

        private static GameObject CreateGenericView(string toolId)
        {
            var root = new GameObject(
                $"[Remote Mini Tool] {toolId}",
                typeof(RectTransform),
                typeof(Canvas),
                typeof(CanvasScaler),
                typeof(GraphicRaycaster));
            var panelObject = new GameObject(
                "Panel",
                typeof(RectTransform),
                typeof(Image));
            panelObject.transform.SetParent(root.transform, false);
            var panel = (RectTransform)panelObject.transform;
            panel.sizeDelta = new Vector2(430f, 150f);
            panelObject.GetComponent<Image>().color =
                new Color(0.06f, 0.07f, 0.09f, 0.9f);

            var textObject = new GameObject(
                "Fields",
                typeof(RectTransform),
                typeof(Text));
            textObject.transform.SetParent(panel, false);
            var textTransform = (RectTransform)textObject.transform;
            textTransform.anchorMin = Vector2.zero;
            textTransform.anchorMax = Vector2.one;
            textTransform.offsetMin = new Vector2(10f, 8f);
            textTransform.offsetMax = new Vector2(-10f, -8f);

            Text text = textObject.GetComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>(
                "LegacyRuntime.ttf");
            text.fontSize = 14;
            text.alignment = TextAnchor.UpperLeft;
            text.color = Color.white;
            text.supportRichText = true;
            text.raycastTarget = false;
            return root;
        }

        private static void InitializeCanvas(GameObject instance, int layoutIndex)
        {
            instance.transform.localScale = Vector3.one;
            foreach (Canvas canvas in instance.GetComponentsInChildren<Canvas>(true))
            {
                canvas.enabled = true;
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.overrideSorting = true;
                canvas.sortingOrder = 20000 + layoutIndex;
                canvas.transform.localScale = Vector3.one;
            }
        }

        private void ApplyLayout(int layoutIndex)
        {
            RectTransform text = _legacyText != null
                ? _legacyText.rectTransform
                : _tmpText != null
                    ? _tmpText.rectTransform
                    : null;
            if (text == null)
                return;

            RectTransform display = text.parent as RectTransform ?? text;
            bool rightColumn = (layoutIndex & 1) != 0;
            int row = layoutIndex / 2;
            Vector2 anchor = rightColumn ? new Vector2(1f, 1f) : new Vector2(0f, 1f);
            display.anchorMin = anchor;
            display.anchorMax = anchor;
            display.pivot = anchor;
            display.anchoredPosition = new Vector2(
                rightColumn ? -16f : 16f,
                -16f - row * 150f);
        }
    }
}
