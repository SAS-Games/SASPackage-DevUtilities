using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace SAS.Utilities.DeveloperConsole
{
    public static class AnchorPreset
    {
        // Top row
        public static readonly Vector2 TopLeft = new Vector2(0f, 1f);
        public static readonly Vector2 TopCenter = new Vector2(0.5f, 1f);
        public static readonly Vector2 TopRight = new Vector2(1f, 1f);

        // Middle row
        public static readonly Vector2 MiddleLeft = new Vector2(0f, 0.5f);
        public static readonly Vector2 MiddleCenter = new Vector2(0.5f, 0.5f);
        public static readonly Vector2 MiddleRight = new Vector2(1f, 0.5f);

        // Bottom row
        public static readonly Vector2 BottomLeft = new Vector2(0f, 0f);
        public static readonly Vector2 BottomCenter = new Vector2(0.5f, 0f);
        public static readonly Vector2 BottomRight = new Vector2(1f, 0f);

        private static readonly Dictionary<string, Vector2> Presets =
            new Dictionary<string, Vector2>()
            {
                 { "topleft", TopLeft },
                 { "topcenter", TopCenter },
                 { "topright", TopRight },

                 { "middleleft", MiddleLeft },
                 { "middlecenter",MiddleCenter },
                 { "middleright", MiddleRight },

                 { "bottomleft", BottomLeft },
                 { "bottomcenter",BottomCenter },
                 { "bottomright", BottomRight },
            };

        public static bool TryGetAnchorValues(string key, out Vector2 pivot)
        {
            key = key.ToLower();
            return Presets.TryGetValue(key, out pivot);
        }
    }

    public static class ExtensionUtils
    {
        /// <summary>
        /// Returns the screen-space rectangle of a RectTransform.
        /// </summary>
        public static Rect GetScreenSpaceRect(this RectTransform rt, Camera cam = null)
        {
            Vector3[] worldCorners = new Vector3[4];
            rt.GetWorldCorners(worldCorners);

            // If no camera is provided and canvas is Overlay, no conversion needed
            if (cam == null)
            {
                cam = Camera.main;
            }

            Vector3 bl = RectTransformUtility.WorldToScreenPoint(cam, worldCorners[0]); // bottom-left
            Vector3 tr = RectTransformUtility.WorldToScreenPoint(cam, worldCorners[2]); // top-right

            return new Rect(bl, tr - bl);
        }
        public static Bounds GetWorldBounds(this RectTransform rt)
        {
            Vector3[] corners = new Vector3[4];
            rt.GetWorldCorners(corners);

            var bounds = new Bounds(corners[0], Vector3.zero);
            for (int i = 1; i < 4; i++)
                bounds.Encapsulate(corners[i]);

            return bounds;
        }

        /// <summary>
        /// Returns the Bounds of a RectTransform relative to a root transform.
        /// </summary>
        public static Bounds GetRelativeBounds(this RectTransform rt, Transform root)
        {
            Vector3[] corners = new Vector3[4];
            rt.GetWorldCorners(corners);

            for (int i = 0; i < 4; i++)
                corners[i] = root.InverseTransformPoint(corners[i]);

            var bounds = new Bounds(corners[0], Vector3.zero);
            for (int i = 1; i < 4; i++)
                bounds.Encapsulate(corners[i]);

            return bounds;
        }


        /// <summary>
        /// Sets the text of the TMP_InputField after a delay of N frames.
        /// Uses Awaitable on 2023+, otherwise falls back to a coroutine runner (Unity 2022-friendly).
        /// </summary>
        public static void SetDelayedText(this TMP_InputField inputField, string newText, int frameDelay = 1)
        {
#if UNITY_2023_1_OR_NEWER
            // Use Unity's Awaitable on newer versions
            _ = SetDelayedTextAsync_Awaitable(inputField, newText, frameDelay);
#else
            // Unity 2022 fallback via coroutine
            CoroutineRunner.Run(Delay());

            IEnumerator Delay()
            {
                for (int i = 0; i < frameDelay; i++)
                    yield return null;
                inputField.text = newText;
            }
#endif
        }

#if UNITY_2023_1_OR_NEWER
        private static async System.Threading.Tasks.Task SetDelayedTextAsync_Awaitable(TMP_InputField inputField, string newText, int frameDelay)
        {
            for (int i = 0; i < frameDelay; i++)
                await UnityEngine.Awaitable.NextFrameAsync();
            inputField.text = newText;
        }
#endif

        /// <summary>
        /// Align a RectTransform to screen edges like Anchor Presets, with optional padding.
        /// </summary>
        /// <param name="rectTransform"></param>
        /// <param name="anchorPreset"></param>
        /// <param name="paddingX"></param>
        /// <param name="paddingY"></param>
        public static void AlignToScreen(this RectTransform rectTransform, string anchorPreset, int paddingX = 0, int paddingY = 0)
        {
            if (AnchorPreset.TryGetAnchorValues(anchorPreset, out Vector2 anchor))
            {
                Vector2Int padding = new Vector2Int(paddingX, paddingY);
                rectTransform.AlignToScreen(anchor, padding);
            }
        }

        /// <summary>
        /// Align a RectTransform to screen edges like Anchor Presets, with optional padding.
        /// </summary>
        /// <param name="rectTransform"></param>
        /// <param name="pivot"></param>
        /// <param name="padding"></param>
        public static void AlignToScreen(this RectTransform rectTransform, Vector2 pivot, Vector2Int padding)
        {
            if (rectTransform == null)
                return;

            rectTransform.anchorMin = pivot;
            rectTransform.anchorMax = pivot;
            rectTransform.pivot = pivot;

            // Padding direction derived from pivot
            Vector2 offset = new Vector2(
                Mathf.Lerp(+padding.x, -padding.x, pivot.x),
                Mathf.Lerp(+padding.y, -padding.y, pivot.y)
            );

            rectTransform.anchoredPosition = offset;
        }

        /// <summary>
        /// Minimal hidden runner to start coroutines from static context.
        /// </summary>
        private sealed class CoroutineRunner : MonoBehaviour
        {
            private static CoroutineRunner _instance;

            public static void Run(IEnumerator routine)
            {
                if (_instance == null)
                {
                    var go = new GameObject("[ExtensionUtils CoroutineRunner]");
                    go.hideFlags = HideFlags.HideAndDontSave;
                    DontDestroyOnLoad(go);
                    _instance = go.AddComponent<CoroutineRunner>();
                }
                _instance.StartCoroutine(routine);
            }
        }

    }
}