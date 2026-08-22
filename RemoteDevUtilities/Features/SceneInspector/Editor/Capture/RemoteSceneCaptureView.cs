using System;
using SAS.Utilities.RemoteDevUtilities.Protocol.RuntimeSceneInspector.Capture;
using UnityEditor;
using UnityEngine;

namespace SAS.Utilities.RemoteDevUtilities.Editor.RuntimeSceneInspector.Capture
{
    internal sealed class RemoteSceneCaptureView : IDisposable
    {
        private static readonly string[] CaptureWidths = { "640", "960", "1280" };
        private static readonly int[] CaptureWidthValues = { 640, 960, 1280 };

        private RemoteRuntimeSceneInspectorClient _client;
        private Texture2D _texture;
        private long _textureCaptureId;
        private string _decodeError;
        private bool _freezeWhilePicking = true;
        private int _captureWidthIndex = 1;
        private Vector2 _lastPickUv = new(-1f, -1f);
        private int _sessionGeneration = int.MinValue;

        internal void Draw(RemoteRuntimeSceneInspectorClient client, float availableWidth)
        {
            if (_sessionGeneration != client.SessionGeneration)
            {
                _sessionGeneration = client.SessionGeneration;
                DestroyTexture();
                _decodeError = null;
                _lastPickUv = new Vector2(-1f, -1f);
            }
            _client = client;
            DrawToolbar(client, availableWidth);

            RemoteSceneCaptureResponse capture = client.Capture;
            if (client.IsCapturePending)
            {
                EditorGUILayout.HelpBox("Capturing the Player at the end of its current frame...", MessageType.Info);
                return;
            }

            if (capture == null)
            {
                DestroyTexture();
                EditorGUILayout.HelpBox("Capture the connected Player, then click the captured frame to inspect an object.", MessageType.None);
                return;
            }

            if (!string.IsNullOrEmpty(capture.Error))
            {
                DestroyTexture();
                EditorGUILayout.HelpBox(capture.Error, MessageType.Error);
                return;
            }

            EnsureTexture(capture);
            if (!string.IsNullOrEmpty(_decodeError))
            {
                EditorGUILayout.HelpBox(_decodeError, MessageType.Error);
                return;
            }

            DrawPreview(client, capture, availableWidth);
            DrawStatus(client, capture);
            DrawCandidateSelector(client);
        }

        public void Dispose()
        {
            ReleaseCapture();
            DestroyTexture();
            _client = null;
        }

        internal void ReleaseCapture()
        {
            _client?.ReleaseCapture();
        }

        private void DrawToolbar(RemoteRuntimeSceneInspectorClient client, float availableWidth)
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            using (new EditorGUI.DisabledScope(client.IsCapturePending || client.IsPickPending ||
                                                client.IsCaptureReleasePending))
            {
                if (GUILayout.Button("Capture Player View", EditorStyles.toolbarButton, GUILayout.Width(130f)))
                {
                    _lastPickUv = new Vector2(-1f, -1f);
                    client.RequestCapture(_freezeWhilePicking, CaptureWidthValues[_captureWidthIndex]);
                }
            }

            GUILayout.FlexibleSpace();
            using (new EditorGUI.DisabledScope(!client.CanReleaseCapture))
            {
                string releaseLabel = client.IsCapturePending ? "Cancel" : "Release";
                if (GUILayout.Button(releaseLabel, EditorStyles.toolbarButton, GUILayout.Width(58f)))
                    client.ReleaseCapture();
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal(availableWidth < 520f ? EditorStyles.toolbar : GUIStyle.none);
            _freezeWhilePicking = GUILayout.Toggle(_freezeWhilePicking, "Freeze while picking",
                availableWidth < 520f ? EditorStyles.toolbarButton : EditorStyles.miniButton,
                GUILayout.Width(125f));
            GUILayout.FlexibleSpace();
            GUILayout.Label("Width", EditorStyles.miniLabel, GUILayout.Width(36f));
            _captureWidthIndex = EditorGUILayout.Popup(_captureWidthIndex, CaptureWidths,
                availableWidth < 520f ? EditorStyles.toolbarPopup : EditorStyles.popup,
                GUILayout.Width(58f));
            EditorGUILayout.EndHorizontal();
        }

        private void DrawPreview(RemoteRuntimeSceneInspectorClient client, RemoteSceneCaptureResponse capture, float availableWidth)
        {
            float previewWidth = Mathf.Max(200f, availableWidth - 18f);
            float previewHeight = Mathf.Clamp(previewWidth * capture.Height / Mathf.Max(1f, capture.Width), 180f, 520f);
            Rect viewport = GUILayoutUtility.GetRect(200f, previewHeight, GUILayout.ExpandWidth(true));
            GUI.Box(viewport, GUIContent.none, EditorStyles.helpBox);
            Rect imageRect = Fit(viewport, capture.Width, capture.Height, 4f);
            // The capture must not inherit a disabled/tinted GUI state from another workspace
            // control. Such a state looks like a translucent overlay over an otherwise clean JPEG.
            bool previousEnabled = GUI.enabled;
            Color previousColor = GUI.color;
            GUI.enabled = true;
            GUI.color = Color.white;
            GUI.DrawTexture(imageRect, _texture, ScaleMode.StretchToFill, false);
            GUI.color = previousColor;
            GUI.enabled = previousEnabled;

            if (_lastPickUv.x >= 0f && _lastPickUv.y >= 0f)
            {
                Vector2 marker = new(
                    Mathf.Lerp(imageRect.xMin, imageRect.xMax, _lastPickUv.x),
                    Mathf.Lerp(imageRect.yMax, imageRect.yMin, _lastPickUv.y));
                Color previous = GUI.color;
                GUI.color = new Color(0.2f, 0.8f, 1f, 0.95f);
                GUI.DrawTexture(new Rect(marker.x - 8f, marker.y, 16f, 1f), Texture2D.whiteTexture);
                GUI.DrawTexture(new Rect(marker.x, marker.y - 8f, 1f, 16f), Texture2D.whiteTexture);
                GUI.color = previous;
            }

            EditorGUIUtility.AddCursorRect(imageRect, MouseCursor.Link);
            Event current = Event.current;
            if (current.type == EventType.MouseDown && current.button == 0 && imageRect.Contains(current.mousePosition) &&
                client.IsCaptureActive && !client.IsPickPending)
            {
                float x = Mathf.InverseLerp(imageRect.xMin, imageRect.xMax, current.mousePosition.x);
                float y = 1f - Mathf.InverseLerp(imageRect.yMin, imageRect.yMax, current.mousePosition.y);
                _lastPickUv = new Vector2(x, y);
                client.Pick(capture.CaptureId, x, y);
                current.Use();
            }
        }

        private static void DrawStatus(RemoteRuntimeSceneInspectorClient client, RemoteSceneCaptureResponse capture)
        {
            if (client.IsPickPending)
            {
                EditorGUILayout.HelpBox("Resolving the selected pixel on the Player...", MessageType.Info);
                return;
            }

            if (client.LastPickResult != null && !string.IsNullOrEmpty(client.LastPickResult.Error))
            {
                EditorGUILayout.HelpBox(client.LastPickResult.Error, MessageType.Warning);
                return;
            }

            string freezeStatus = capture.PlayerFrozen ? "Player frozen while selecting" : "Player continues running";
            EditorGUILayout.LabelField($"Frame {capture.FrameCount}  •  {capture.Width}×{capture.Height}  •  {freezeStatus}",
                EditorStyles.centeredGreyMiniLabel);
        }

        private static void DrawCandidateSelector(RemoteRuntimeSceneInspectorClient client)
        {
            RemoteScenePickCandidate[] candidates = client.LastPickResult?.Candidates;
            if (candidates == null || candidates.Length == 0)
                return;

            int selectedIndex = 0;
            var labels = new string[candidates.Length];
            for (int i = 0; i < candidates.Length; i++)
            {
                RemoteScenePickCandidate candidate = candidates[i];
                labels[i] = $"{candidate.Name}  [{candidate.Source}]  {candidate.HierarchyPath}";
                if (candidate.ObjectId == client.LastPickedObjectId)
                    selectedIndex = i;
            }

            EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);
            EditorGUILayout.LabelField($"Objects at pixel ({candidates.Length})", GUILayout.Width(125f));
            int nextIndex = EditorGUILayout.Popup(selectedIndex, labels);
            using (new EditorGUI.DisabledScope(candidates.Length < 2))
            {
                if (GUILayout.Button("Previous", GUILayout.Width(62f)))
                    nextIndex = (selectedIndex - 1 + candidates.Length) % candidates.Length;
                if (GUILayout.Button("Next", GUILayout.Width(42f)))
                    nextIndex = (selectedIndex + 1) % candidates.Length;
            }
            EditorGUILayout.EndHorizontal();

            if (nextIndex != selectedIndex)
                client.SelectPickedObject(candidates[nextIndex].ObjectId);
        }

        private void EnsureTexture(RemoteSceneCaptureResponse capture)
        {
            if (_texture != null && _textureCaptureId == capture.CaptureId)
                return;

            DestroyTexture();
            _decodeError = null;
            try
            {
                byte[] bytes = Convert.FromBase64String(capture.ImageBase64 ?? string.Empty);
                _texture = new Texture2D(2, 2, TextureFormat.RGB24, false)
                {
                    hideFlags = HideFlags.HideAndDontSave,
                    name = $"Remote Player Capture {capture.CaptureId}"
                };
                if (!_texture.LoadImage(bytes, true))
                    throw new InvalidOperationException("Unity could not decode the Player JPEG.");
                _textureCaptureId = capture.CaptureId;
            }
            catch (Exception exception)
            {
                DestroyTexture();
                _decodeError = exception.GetType().Name + ": " + exception.Message;
            }
        }

        private void DestroyTexture()
        {
            if (_texture != null)
                UnityEngine.Object.DestroyImmediate(_texture);
            _texture = null;
            _textureCaptureId = 0;
        }

        private static Rect Fit(Rect container, float contentWidth, float contentHeight, float padding)
        {
            Rect inner = new(container.x + padding, container.y + padding,
                Mathf.Max(1f, container.width - padding * 2f), Mathf.Max(1f, container.height - padding * 2f));
            float scale = Mathf.Min(inner.width / Mathf.Max(1f, contentWidth), inner.height / Mathf.Max(1f, contentHeight));
            Vector2 size = new(contentWidth * scale, contentHeight * scale);
            return new Rect(inner.center - size * 0.5f, size);
        }
    }
}
