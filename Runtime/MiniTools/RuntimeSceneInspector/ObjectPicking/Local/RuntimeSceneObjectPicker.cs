using System;
using System.Collections.Generic;
using HP.Utilities.RuntimeSceneInspector.Core;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Rendering;

namespace HP.Utilities.RuntimeSceneInspector
{
    internal enum RuntimeScenePickSource
    {
        Ui,
        Renderer,
        Collider3D,
        Collider2D
    }

    internal sealed class RuntimeScenePickCandidate
    {
        internal RuntimeObjectId ObjectId;
        internal string Name;
        internal string HierarchyPath;
        internal RuntimeScenePickSource Source;
    }

    /// <summary>
    /// Resolves a screen point to ordered GameObjects that are present in the runtime hierarchy.
    /// UI is ordered by EventSystem raycast order. Renderers are ordered by sorting layer/order and
    /// then camera distance, followed by collider-only objects. This remains runtime-player safe.
    /// </summary>
    internal sealed class RuntimeSceneObjectPicker
    {
        private sealed class RendererHit
        {
            internal Renderer Renderer;
            internal float Distance;
            internal int SortingLayerValue;
            internal int SortingOrder;
        }

        private readonly RuntimeSceneInspectorSettings _settings;
        private readonly IRuntimeSceneObjectResolver _resolver;
        private readonly List<RaycastResult> _uiResults = new();
        private readonly HashSet<long> _candidateIds = new();

        internal RuntimeSceneObjectPicker(RuntimeSceneInspectorSettings settings, IRuntimeSceneObjectResolver resolver)
        {
            _settings = settings;
            _resolver = resolver;
        }

        internal IReadOnlyList<RuntimeScenePickCandidate> GetCandidates(Vector2 screenPosition, out string message)
        {
            var candidates = new List<RuntimeScenePickCandidate>();
            _candidateIds.Clear();
            message = string.Empty;
            if (_resolver == null)
            {
                message = "Object picking is not available for this inspector connection.";
                return candidates;
            }

            if (_settings.PickUiObjects)
                AddUiCandidates(screenPosition, candidates);

            Camera[] cameras = Camera.allCameras;
            Array.Sort(cameras, CompareCameraDepthDescending);
            foreach (Camera camera in cameras)
            {
                if (!CanPickThrough(camera, screenPosition))
                    continue;

                Ray ray = camera.ScreenPointToRay(screenPosition);
                if (_settings.UseRendererBoundsFallback)
                    AddRendererCandidates(camera, ray, candidates);
                AddPhysicsCandidates(camera, ray, candidates);
            }

            if (candidates.Count == 0)
                message = "No inspectable object was found at that screen position.";
            return candidates;
        }

        private void AddUiCandidates(Vector2 screenPosition, List<RuntimeScenePickCandidate> candidates)
        {
            EventSystem eventSystem = EventSystem.current;
            if (eventSystem == null)
                return;

            _uiResults.Clear();
            var pointer = new PointerEventData(eventSystem) { position = screenPosition };
            eventSystem.RaycastAll(pointer, _uiResults);
            foreach (RaycastResult result in _uiResults)
                AddCandidate(result.gameObject, RuntimeScenePickSource.Ui, candidates);
        }

        private void AddRendererCandidates(Camera camera, Ray ray, List<RuntimeScenePickCandidate> candidates)
        {
            var hits = new List<RendererHit>();
            foreach (Renderer renderer in Resources.FindObjectsOfTypeAll<Renderer>())
            {
                if (!CanUseRenderer(renderer, camera) ||
                    !renderer.bounds.IntersectRay(ray, out float distance) || distance < 0f)
                    continue;

                ResolveSorting(renderer, out int sortingLayerValue, out int sortingOrder);
                hits.Add(new RendererHit
                {
                    Renderer = renderer,
                    Distance = distance,
                    SortingLayerValue = sortingLayerValue,
                    SortingOrder = sortingOrder
                });
            }

            hits.Sort(CompareRendererHits);
            foreach (RendererHit hit in hits)
                AddCandidate(hit.Renderer.gameObject, RuntimeScenePickSource.Renderer, candidates);
        }

        private void AddPhysicsCandidates(Camera camera, Ray ray, List<RuntimeScenePickCandidate> candidates)
        {
            float maximumDistance = Mathf.Max(camera.farClipPlane, 0f);
            QueryTriggerInteraction triggerInteraction = _settings.PickTriggerColliders
                ? QueryTriggerInteraction.Collide
                : QueryTriggerInteraction.Ignore;

            RaycastHit[] hits3D = Physics.RaycastAll(ray, maximumDistance, _settings.ObjectPickingLayerMask,
                triggerInteraction);
            Array.Sort(hits3D, (left, right) => left.distance.CompareTo(right.distance));
            foreach (RaycastHit hit in hits3D)
                AddCandidate(hit.collider != null ? hit.collider.gameObject : null,
                    RuntimeScenePickSource.Collider3D, candidates);

            RaycastHit2D[] hits2D = Physics2D.GetRayIntersectionAll(ray, maximumDistance,
                _settings.ObjectPickingLayerMask);
            Array.Sort(hits2D, Compare2DHits);
            foreach (RaycastHit2D hit in hits2D)
            {
                if (!_settings.PickTriggerColliders && hit.collider != null && hit.collider.isTrigger)
                    continue;
                AddCandidate(hit.collider != null ? hit.collider.gameObject : null,
                    RuntimeScenePickSource.Collider2D, candidates);
            }
        }

        private void AddCandidate(GameObject candidate, RuntimeScenePickSource source,
            List<RuntimeScenePickCandidate> candidates)
        {
            if (!IsAllowed(candidate) || !_resolver.TryGetObjectId(candidate, out RuntimeObjectId objectId) ||
                !_candidateIds.Add(objectId.Value))
                return;

            candidates.Add(new RuntimeScenePickCandidate
            {
                ObjectId = objectId,
                Name = candidate.name,
                HierarchyPath = BuildHierarchyPath(candidate.transform),
                Source = source
            });
        }

        private bool CanUseRenderer(Renderer renderer, Camera camera)
        {
            return renderer != null && renderer.enabled && renderer.gameObject.activeInHierarchy &&
                   renderer.gameObject.scene.IsValid() && renderer.gameObject.scene.isLoaded &&
                   (camera.cullingMask & (1 << renderer.gameObject.layer)) != 0 && IsAllowed(renderer.gameObject);
        }

        private bool IsAllowed(GameObject candidate)
        {
            return candidate != null &&
                   (_settings.ObjectPickingLayerMask & (1 << candidate.layer)) != 0 &&
                   candidate.GetComponentInParent<RuntimeSceneInspectorHost>() == null;
        }

        private static int CompareRendererHits(RendererHit left, RendererHit right)
        {
            int layer = right.SortingLayerValue.CompareTo(left.SortingLayerValue);
            if (layer != 0)
                return layer;
            int order = right.SortingOrder.CompareTo(left.SortingOrder);
            return order != 0 ? order : left.Distance.CompareTo(right.Distance);
        }

        private static int Compare2DHits(RaycastHit2D left, RaycastHit2D right)
        {
            Renderer leftRenderer = left.collider != null ? left.collider.GetComponent<Renderer>() : null;
            Renderer rightRenderer = right.collider != null ? right.collider.GetComponent<Renderer>() : null;
            ResolveSorting(leftRenderer, out int leftLayer, out int leftOrder);
            ResolveSorting(rightRenderer, out int rightLayer, out int rightOrder);
            int layer = rightLayer.CompareTo(leftLayer);
            if (layer != 0)
                return layer;
            int order = rightOrder.CompareTo(leftOrder);
            return order != 0 ? order : left.distance.CompareTo(right.distance);
        }

        private static void ResolveSorting(Renderer renderer, out int layerValue, out int order)
        {
            layerValue = 0;
            order = 0;
            if (renderer == null)
                return;

            SortingGroup group = renderer.GetComponentInParent<SortingGroup>();
            int layerId = group != null && group.enabled ? group.sortingLayerID : renderer.sortingLayerID;
            layerValue = SortingLayer.GetLayerValueFromID(layerId);
            order = group != null && group.enabled ? group.sortingOrder : renderer.sortingOrder;
        }

        private static string BuildHierarchyPath(Transform transform)
        {
            if (transform == null)
                return string.Empty;
            var names = new List<string>();
            for (Transform current = transform; current != null; current = current.parent)
                names.Add(current.name);
            names.Reverse();
            return string.Join("/", names);
        }

        private static bool CanPickThrough(Camera camera, Vector2 screenPosition)
        {
            return camera != null && camera.isActiveAndEnabled && camera.cameraType == CameraType.Game &&
                   camera.pixelRect.Contains(screenPosition);
        }

        private static int CompareCameraDepthDescending(Camera left, Camera right)
        {
            if (left == null)
                return right == null ? 0 : 1;
            if (right == null)
                return -1;
            return right.depth.CompareTo(left.depth);
        }
    }
}
