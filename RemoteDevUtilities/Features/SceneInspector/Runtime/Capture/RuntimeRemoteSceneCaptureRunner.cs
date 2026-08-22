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

    /// <summary>
    /// Unity's Play Mode Game View can render the Editor selection outline into ScreenCapture
    /// results. Temporarily clearing the Editor selection keeps remote captures identical to the
    /// Player view. A selection made while capture is active is never overwritten on release.
    /// </summary>
    internal sealed class RuntimeEditorSelectionSuppressionLease
    {
#if UNITY_EDITOR
        private static int s_LeaseCount;
        private static UnityEngine.Object[] s_SavedSelection = Array.Empty<UnityEngine.Object>();
        private bool _acquired;
#endif

        internal void Acquire()
        {
#if UNITY_EDITOR
            if (_acquired)
                return;
            _acquired = true;
            if (s_LeaseCount++ > 0)
                return;

            s_SavedSelection = UnityEditor.Selection.objects ?? Array.Empty<UnityEngine.Object>();
            UnityEditor.Selection.objects = Array.Empty<UnityEngine.Object>();
#endif
        }

        internal void Release()
        {
#if UNITY_EDITOR
            if (!_acquired)
                return;
            _acquired = false;
            s_LeaseCount = Mathf.Max(0, s_LeaseCount - 1);
            if (s_LeaseCount != 0)
                return;

            if (UnityEditor.Selection.objects == null || UnityEditor.Selection.objects.Length == 0)
                UnityEditor.Selection.objects = s_SavedSelection;
            s_SavedSelection = Array.Empty<UnityEngine.Object>();
#endif
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
        private readonly RuntimeEditorSelectionSuppressionLease _selectionSuppression = new();
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
            _selectionSuppression.Acquire();

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
            _selectionSuppression.Release();

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
            finally
            {
                _selectionSuppression.Release();
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
                int sourceWidth = Mathf.Max(1, Screen.width);
                int sourceHeight = Mathf.Max(1, Screen.height);
                // CaptureScreenshotAsTexture produces gamma-shifted pixels in Linear color-space
                // Players on affected Unity versions. Reading the completed backbuffer at the
                // end of the frame preserves the same display-ready values the user sees.
                source = new Texture2D(sourceWidth, sourceHeight, TextureFormat.RGB24, false, false);
                RenderTexture.active = null;
                source.ReadPixels(new Rect(0f, 0f, sourceWidth, sourceHeight), 0, 0, false);
                source.Apply(false, false);
                if (source.width < 1 || source.height < 1)
                    throw new InvalidOperationException("The Player did not produce a readable screen capture.");

                int targetWidth = Mathf.Min(Mathf.Clamp(maximumWidth, 320, 1280), source.width);
                int targetHeight = Mathf.Max(1,
                    Mathf.RoundToInt(source.height * (targetWidth / (float)source.width)));
                Texture2D captured = source;
                if (targetWidth != source.width || targetHeight != source.height)
                {
                    temporary = RenderTexture.GetTemporary(targetWidth, targetHeight, 0,
                        RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
                    temporary.filterMode = FilterMode.Bilinear;
                    BlitDisplayPixels(source, temporary);
                    RenderTexture.active = temporary;
                    scaled = new Texture2D(targetWidth, targetHeight, TextureFormat.RGB24, false, false);
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

        /// <summary>
        /// Resizes display-ready color pixels without inheriting a stale sRGB-write state from
        /// the active render pipeline. In Linear projects, the sRGB source sampling and target
        /// write conversions cancel each other and preserve the visible framebuffer colors.
        /// </summary>
        internal static void BlitDisplayPixels(Texture source, RenderTexture destination,
            bool flipVertically = false)
        {
            bool previousSrgbWrite = GL.sRGBWrite;
            bool requiredSrgbWrite = QualitySettings.activeColorSpace == ColorSpace.Linear &&
                                     destination != null && destination.sRGB;
            try
            {
                if (previousSrgbWrite != requiredSrgbWrite)
                    GL.sRGBWrite = requiredSrgbWrite;

                if (flipVertically)
                {
                    Graphics.Blit(source, destination,
                        new Vector2(1f, -1f), new Vector2(0f, 1f));
                }
                else
                {
                    Graphics.Blit(source, destination);
                }
            }
            finally
            {
                if (GL.sRGBWrite != previousSrgbWrite)
                    GL.sRGBWrite = previousSrgbWrite;
            }
        }
    }
}
