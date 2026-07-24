using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace SAS.Utilities.DeveloperConsole
{
    /// <summary>
    /// Builds one-shot runtime reports inspired by Unreal Engine stat commands.
    /// Scene-rendering values are a CPU-side census, not a replacement for a frame capture.
    /// </summary>
    public static class RuntimeStatsReporter
    {
        private const double BytesPerMebibyte = 1024d * 1024d;

        public static string BuildMemoryReport()
        {
            var builder = new StringBuilder(512);
            builder.AppendLine("Stats.Memory")
                .AppendLine($"Managed used       : {ToMebibytes(GC.GetTotalMemory(false)):F2} MiB")
                .AppendLine($"Mono used          : {ToMebibytes(Profiler.GetMonoUsedSizeLong()):F2} MiB")
                .AppendLine($"Mono heap          : {ToMebibytes(Profiler.GetMonoHeapSizeLong()):F2} MiB")
                .AppendLine($"Total allocated    : {ToMebibytes(Profiler.GetTotalAllocatedMemoryLong()):F2} MiB")
                .AppendLine($"Total reserved     : {ToMebibytes(Profiler.GetTotalReservedMemoryLong()):F2} MiB")
                .AppendLine($"Reserved unused    : {ToMebibytes(Profiler.GetTotalUnusedReservedMemoryLong()):F2} MiB")
                .AppendLine($"System RAM         : {SystemInfo.systemMemorySize} MiB")
                .AppendLine($"Graphics memory    : {SystemInfo.graphicsMemorySize} MiB")
                .Append($"GC collections     : Gen0={GC.CollectionCount(0)}, Gen1={GC.CollectionCount(1)}, Gen2={GC.CollectionCount(2)}");
            return builder.ToString();
        }

        public static string BuildLevelsReport()
        {
            var builder = new StringBuilder(512);
            Scene activeScene = SceneManager.GetActiveScene();
            builder.AppendLine("Stats.Levels")
                .AppendLine($"Loaded scenes: {SceneManager.sceneCount}")
                .AppendLine($"Active scene : {(activeScene.IsValid() ? GetSceneName(activeScene) : "<none>")}");

            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                Scene scene = SceneManager.GetSceneAt(i);
                builder.Append("  [")
                    .Append(i)
                    .Append("] ")
                    .Append(GetSceneName(scene))
                    .Append(" | build=")
                    .Append(scene.buildIndex)
                    .Append(" | loaded=")
                    .Append(scene.isLoaded)
                    .Append(" | roots=")
                    .Append(scene.isLoaded ? scene.rootCount : 0);

                if (scene == activeScene)
                    builder.Append(" | ACTIVE");
                if (i + 1 < SceneManager.sceneCount)
                    builder.AppendLine();
            }

            return builder.ToString();
        }

        public static string BuildSceneRenderingReport()
        {
            Renderer[] renderers = UnityEngine.Object.FindObjectsByType<Renderer>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
            Light[] lights = UnityEngine.Object.FindObjectsByType<Light>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
            Camera[] cameras = UnityEngine.Object.FindObjectsByType<Camera>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
            LODGroup[] lodGroups = UnityEngine.Object.FindObjectsByType<LODGroup>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
            ParticleSystem[] particles = UnityEngine.Object.FindObjectsByType<ParticleSystem>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);

            int activeRenderers = 0;
            int visibleRenderers = 0;
            int shadowCasters = 0;
            var materials = new HashSet<Material>();

            foreach (Renderer renderer in renderers)
            {
                if (!renderer)
                    continue;

                if (renderer.enabled && renderer.gameObject.activeInHierarchy)
                    activeRenderers++;
                if (renderer.isVisible)
                    visibleRenderers++;
                if (renderer.enabled && renderer.gameObject.activeInHierarchy &&
                    renderer.shadowCastingMode != ShadowCastingMode.Off)
                    shadowCasters++;

                foreach (Material material in renderer.sharedMaterials)
                {
                    if (material)
                        materials.Add(material);
                }
            }

            int meshInstances = 0;
            ulong vertexInstances = 0;
            ulong triangleInstances = 0;
            CountMeshRenderers(ref meshInstances, ref vertexInstances, ref triangleInstances);
            CountSkinnedMeshRenderers(ref meshInstances, ref vertexInstances, ref triangleInstances);

            int enabledLights = 0;
            int shadowLights = 0;
            foreach (Light light in lights)
            {
                if (!light || !light.enabled || !light.gameObject.activeInHierarchy)
                    continue;

                enabledLights++;
                if (light.shadows != LightShadows.None)
                    shadowLights++;
            }

            int activeCameras = 0;
            foreach (Camera camera in cameras)
            {
                if (camera && camera.enabled && camera.gameObject.activeInHierarchy)
                    activeCameras++;
            }

            int aliveParticles = 0;
            foreach (ParticleSystem particle in particles)
            {
                if (particle && particle.gameObject.activeInHierarchy && particle.IsAlive(true))
                    aliveParticles++;
            }

            var builder = new StringBuilder(768);
            builder.AppendLine("Stats.SceneRendering (CPU-side census)")
                .AppendLine($"Renderers          : {renderers.Length} total, {activeRenderers} active, {visibleRenderers} visible")
                .AppendLine($"Shadow casters     : {shadowCasters}")
                .AppendLine($"Unique materials   : {materials.Count}")
                .AppendLine($"Mesh instances     : {meshInstances}")
                .AppendLine($"Vertex instances   : {vertexInstances}")
                .AppendLine($"Triangle instances : {triangleInstances}")
                .AppendLine($"Lights             : {lights.Length} total, {enabledLights} active, {shadowLights} with shadows")
                .AppendLine($"Cameras            : {cameras.Length} total, {activeCameras} active")
                .AppendLine($"LOD groups         : {lodGroups.Length}")
                .AppendLine($"Particle systems   : {particles.Length} total, {aliveParticles} alive")
                .Append("Note: geometry estimates do not account for camera culling, LOD selection, batching, procedural geometry, or render passes.");
            return builder.ToString();
        }

        private static void CountMeshRenderers(
            ref int meshInstances,
            ref ulong vertexInstances,
            ref ulong triangleInstances)
        {
            MeshFilter[] filters = UnityEngine.Object.FindObjectsByType<MeshFilter>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);

            foreach (MeshFilter filter in filters)
            {
                if (!filter || !filter.gameObject.activeInHierarchy ||
                    !filter.TryGetComponent(out MeshRenderer renderer) || !renderer.enabled)
                    continue;

                AddMesh(filter.sharedMesh, ref meshInstances, ref vertexInstances, ref triangleInstances);
            }
        }

        private static void CountSkinnedMeshRenderers(
            ref int meshInstances,
            ref ulong vertexInstances,
            ref ulong triangleInstances)
        {
            SkinnedMeshRenderer[] renderers = UnityEngine.Object.FindObjectsByType<SkinnedMeshRenderer>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);

            foreach (SkinnedMeshRenderer renderer in renderers)
            {
                if (!renderer || !renderer.enabled || !renderer.gameObject.activeInHierarchy)
                    continue;

                AddMesh(renderer.sharedMesh, ref meshInstances, ref vertexInstances, ref triangleInstances);
            }
        }

        private static void AddMesh(
            Mesh mesh,
            ref int meshInstances,
            ref ulong vertexInstances,
            ref ulong triangleInstances)
        {
            if (!mesh)
                return;

            meshInstances++;
            vertexInstances += (ulong)mesh.vertexCount;
            for (int subMesh = 0; subMesh < mesh.subMeshCount; subMesh++)
            {
                ulong indexCount = mesh.GetIndexCount(subMesh);
                switch (mesh.GetTopology(subMesh))
                {
                    case MeshTopology.Triangles:
                        triangleInstances += indexCount / 3UL;
                        break;
                    case MeshTopology.Quads:
                        triangleInstances += indexCount / 2UL;
                        break;
                }
            }
        }

        private static double ToMebibytes(long bytes) => bytes / BytesPerMebibyte;

        private static string GetSceneName(Scene scene) =>
            string.IsNullOrEmpty(scene.name) ? "<unnamed>" : scene.name;
    }
}
