using System;
using System.Collections;
using SAS.Utilities.RuntimeSceneInspector.Core;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Experimental.Rendering;

namespace SAS.Utilities.RemoteDevUtilities.RuntimeSceneInspector.Capture
{
    internal sealed class RuntimeTimeScaleFreezeLease
    {
        private float _savedTimeScale;

        internal bool IsAcquired { get; private set; }

        internal void Acquire()
        {
            if (IsAcquired)
                return;
            _savedTimeScale = Time.timeScale;
            Time.timeScale = 0f;
            IsAcquired = true;
        }

        internal void Release()
        {
            if (!IsAcquired)
                return;
            Time.timeScale = _savedTimeScale;
            IsAcquired = false;
        }
    }

    internal sealed class RuntimeRemoteSceneCaptureResult
    {
        internal long CaptureId;
        internal byte[] JpegBytes;
        internal int Width;
        internal int Height;
        internal int FrameCount;
        internal bool PlayerFrozen;
        internal string Error;
    }

    [RuntimeSceneInspectorProtected]
    internal sealed class RuntimeRemoteSceneCaptureRunner : MonoBehaviour
    {
        private const float ActiveCaptureTimeoutSeconds = 300f;
        private readonly RuntimeTimeScaleFreezeLease _timeScaleFreeze = new();
        private Coroutine _captureCoroutine;
        private long _activeCaptureId;
        private float _releaseAtRealtime;

        internal long ActiveCaptureId => _activeCaptureId;
        internal bool PlayerFrozen => _timeScaleFreeze.IsAcquired;

        internal void Capture(long captureId, int maximumWidth, int jpegQuality, bool freezeWhilePicking,
            Action<RuntimeRemoteSceneCaptureResult> completed)
        {
            Release();
            _activeCaptureId = captureId;
            _releaseAtRealtime = Time.realtimeSinceStartup + ActiveCaptureTimeoutSeconds;
            if (freezeWhilePicking)
                _timeScaleFreeze.Acquire();

            int width = Mathf.Clamp(maximumWidth, 320, 1280);
            int quality = Mathf.Clamp(jpegQuality, 35, 90);
            _captureCoroutine = StartCoroutine(CaptureAtEndOfFrame(captureId, width, quality, completed));
        }

        internal void Release(long captureId = 0)
        {
            if (captureId != 0 && captureId != _activeCaptureId)
                return;

            if (_captureCoroutine != null)
            {
                StopCoroutine(_captureCoroutine);
                _captureCoroutine = null;
            }

            _timeScaleFreeze.Release();

            _activeCaptureId = 0;
            _releaseAtRealtime = 0f;
        }

        private void Update()
        {
            if (_activeCaptureId != 0 && Time.realtimeSinceStartup >= _releaseAtRealtime)
                Release();
        }

        private IEnumerator CaptureAtEndOfFrame(long captureId, int maximumWidth, int jpegQuality,
            Action<RuntimeRemoteSceneCaptureResult> completed)
        {
            yield return new WaitForEndOfFrame();

            var result = new RuntimeRemoteSceneCaptureResult
            {
                CaptureId = captureId,
                FrameCount = Time.frameCount,
                PlayerFrozen = _timeScaleFreeze.IsAcquired
            };
            try
            {
                RuntimeRemoteScreenCaptureData capture = RuntimeRemoteScreenCapture.Capture(maximumWidth);
                result.Width = capture.Width;
                result.Height = capture.Height;
                result.JpegBytes = ImageConversion.EncodeArrayToJPG(capture.Pixels,
                    capture.GraphicsFormat, (uint)capture.Width, (uint)capture.Height, 0, jpegQuality);
                if (result.JpegBytes == null || result.JpegBytes.Length == 0)
                    throw new InvalidOperationException("The Player could not encode the screen capture.");
            }
            catch (Exception exception)
            {
                result.Error = exception.GetType().Name + ": " + exception.Message;
            }

            _captureCoroutine = null;
            if (_activeCaptureId == captureId)
                completed?.Invoke(result);
        }

        private void OnDisable() => Release();

        private void OnDestroy() => Release();
    }

    internal sealed class RuntimeRemoteScreenCaptureData
    {
        internal byte[] Pixels;
        internal GraphicsFormat GraphicsFormat;
        internal int Width;
        internal int Height;
    }

    /// <summary>
    /// Shared screen-capture primitive used by both one-shot Scene Capture and Frame Recorder.
    /// Unity texture access stays on the main thread; callers may encode the copied pixels later.
    /// </summary>
    internal static class RuntimeRemoteScreenCapture
    {
        internal static RuntimeRemoteScreenCaptureData Capture(int maximumWidth)
        {
            Texture2D source = null;
            Texture2D scaled = null;
            RenderTexture temporary = null;
            RenderTexture previousActive = RenderTexture.active;
            try
            {
                source = ScreenCapture.CaptureScreenshotAsTexture();
                if (source == null || source.width < 1 || source.height < 1)
                    throw new InvalidOperationException("The Player did not produce a readable screen capture.");

                int targetWidth = Mathf.Min(Mathf.Clamp(maximumWidth, 320, 1280), source.width);
                int targetHeight = Mathf.Max(1,
                    Mathf.RoundToInt(source.height * (targetWidth / (float)source.width)));
                Texture2D captured = source;
                if (targetWidth != source.width || targetHeight != source.height)
                {
                    temporary = RenderTexture.GetTemporary(targetWidth, targetHeight, 0,
                        RenderTextureFormat.ARGB32, RenderTextureReadWrite.Default);
                    temporary.filterMode = FilterMode.Bilinear;
                    Graphics.Blit(source, temporary);
                    RenderTexture.active = temporary;
                    scaled = new Texture2D(targetWidth, targetHeight, TextureFormat.RGB24, false);
                    scaled.ReadPixels(new Rect(0f, 0f, targetWidth, targetHeight), 0, 0, false);
                    scaled.Apply(false, false);
                    captured = scaled;
                }

                NativeArray<byte> rawPixels = captured.GetRawTextureData<byte>();
                return new RuntimeRemoteScreenCaptureData
                {
                    Pixels = rawPixels.ToArray(),
                    GraphicsFormat = captured.graphicsFormat,
                    Width = captured.width,
                    Height = captured.height
                };
            }
            finally
            {
                RenderTexture.active = previousActive;
                if (temporary != null)
                    RenderTexture.ReleaseTemporary(temporary);
                if (scaled != null)
                    UnityEngine.Object.Destroy(scaled);
                if (source != null)
                    UnityEngine.Object.Destroy(source);
            }
        }
    }
}
