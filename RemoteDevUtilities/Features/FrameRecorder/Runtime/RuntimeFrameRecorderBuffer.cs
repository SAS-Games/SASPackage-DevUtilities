using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Runtime.CompilerServices;
using System.Text;
using SAS.Utilities.RemoteDevUtilities.Protocol.FrameRecorder;
using SAS.Utilities.RemoteDevUtilities.Protocol.RuntimeSceneInspector;
using UnityEngine;

namespace SAS.Utilities.RemoteDevUtilities.FrameRecorder
{
    internal sealed class RuntimeRecordedFrameData
    {
        internal long RecordingId;
        internal int UnityFrame;
        internal double RealtimeSeconds;
        internal int Width;
        internal int Height;
        internal byte[] JpegBytes;
        internal RuntimeSceneGraphSectionData HierarchySection;
        internal RuntimeSceneGraphSectionData InspectorSection;
        internal RuntimeRecordedInspectorSectionData GranularInspectorSections;
        internal string HierarchySnapshotId;
        internal string InspectorSnapshotId;
        internal string[] InspectorPayloadSnapshotIds = Array.Empty<string>();
        internal RemoteRecordedInspectorManifest InspectorManifest;
        internal int HierarchyBytes;
        internal int InspectorBytes;

        internal long ReferencedSceneGraphBytes => (long)HierarchyBytes + InspectorBytes;

        internal RemoteRecordedFrameInfo ToInfo() => new()
        {
            UnityFrame = UnityFrame,
            RealtimeSeconds = RealtimeSeconds,
            Width = Width,
            Height = Height,
            ImageBytes = JpegBytes?.Length ?? 0,
            SceneGraphBytes = HierarchyBytes + InspectorBytes
        };
    }

    internal sealed class RuntimeRecordedInspectorSectionData
    {
        internal RuntimeSceneGraphSectionData ManifestSection;
        internal RuntimeSceneGraphSectionData[] PayloadSections =
            Array.Empty<RuntimeSceneGraphSectionData>();
        internal RemoteRecordedInspectorManifest Manifest;

        internal static RuntimeRecordedInspectorSectionData Create(
            RemoteRecordedSceneGraph graph)
        {
            RemoteObjectDetails[] inspections = graph?.Inspections ??
                                                Array.Empty<RemoteObjectDetails>();
            var references = new RemoteRecordedObjectSnapshotReference[inspections.Length];
            var payloads = new List<RuntimeSceneGraphSectionData>();
            for (int objectIndex = 0; objectIndex < inspections.Length; objectIndex++)
            {
                RemoteObjectDetails inspection = inspections[objectIndex];
                if (inspection == null)
                {
                    references[objectIndex] = new RemoteRecordedObjectSnapshotReference
                    {
                        IsNull = true
                    };
                    continue;
                }

                RuntimeSceneGraphSectionData header = RuntimeSceneGraphSectionData.Create(
                    new RemoteRecordedObjectHeader
                    {
                        Id = inspection.Id,
                        Name = inspection.Name,
                        Active = inspection.Active,
                        ActiveReadOnly = inspection.ActiveReadOnly,
                        Tag = inspection.Tag,
                        Layer = inspection.Layer,
                        LayerReadOnly = inspection.LayerReadOnly
                    });
                RuntimeSceneGraphSectionData material = RuntimeSceneGraphSectionData.Create(
                    new RemoteRecordedMaterialSnapshot
                    {
                        MaterialsAndShaders = inspection.MaterialsAndShaders
                    });
                RemoteComponentDescriptor[] components = inspection.Components ??
                                                         Array.Empty<RemoteComponentDescriptor>();
                var componentIds = new string[components.Length];
                payloads.Add(header);
                payloads.Add(material);
                for (int componentIndex = 0; componentIndex < components.Length; componentIndex++)
                {
                    RuntimeSceneGraphSectionData component =
                        RuntimeSceneGraphSectionData.CreateComponent(components[componentIndex]);
                    componentIds[componentIndex] = component.SnapshotId;
                    payloads.Add(component);
                }

                references[objectIndex] = new RemoteRecordedObjectSnapshotReference
                {
                    ObjectId = inspection.Id,
                    HeaderSnapshotId = header.SnapshotId,
                    MaterialSnapshotId = material.SnapshotId,
                    ComponentSnapshotIds = componentIds
                };
            }

            var manifest = new RemoteRecordedInspectorManifest
            {
                Objects = references,
                Error = graph?.Error
            };
            return new RuntimeRecordedInspectorSectionData
            {
                Manifest = manifest,
                ManifestSection = RuntimeSceneGraphSectionData.Create(manifest),
                PayloadSections = payloads.ToArray()
            };
        }
    }

    internal sealed class RuntimeSceneGraphSectionData
    {
        private static readonly ConditionalWeakTable<RemoteComponentDescriptor,
            RuntimeSceneGraphSectionData> ComponentSections = new();
        internal string SnapshotId;
        internal byte[] Utf8Bytes;

        internal static RuntimeSceneGraphSectionData CreateComponent(RemoteComponentDescriptor component) =>
            component == null
                ? Create<RemoteComponentDescriptor>(null)
                : ComponentSections.GetValue(component, Create);

        internal static RuntimeSceneGraphSectionData Create<T>(T value)
        {
            string json = JsonUtility.ToJson(value);
            byte[] bytes = Encoding.UTF8.GetBytes(json);
            byte[] identityBytes = Encoding.UTF8.GetBytes(
                (typeof(T).FullName ?? typeof(T).Name) + "\n" + json);
            byte[] digest;
            using (SHA256 sha = SHA256.Create())
                digest = sha.ComputeHash(identityBytes);
            var id = new StringBuilder(digest.Length * 2);
            for (int i = 0; i < digest.Length; i++)
                id.Append(digest[i].ToString("x2", CultureInfo.InvariantCulture));
            return new RuntimeSceneGraphSectionData { SnapshotId = id.ToString(), Utf8Bytes = bytes };
        }
    }

    internal sealed class RuntimeFrameRecorderBuffer
    {
        private readonly object _gate = new();
        private readonly SortedDictionary<int, RuntimeRecordedFrameData> _frames = new();
        private readonly Dictionary<string, SceneGraphBlob> _sceneGraphBlobs = new(StringComparer.Ordinal);
        private long _recordingId;
        private int _capacity;
        private long _storedImageBytes;
        private long _storedSceneGraphBytes;
        private long _referencedSceneGraphBytes;

        internal long RecordingId
        {
            get { lock (_gate) return _recordingId; }
        }

        internal int Capacity
        {
            get { lock (_gate) return _capacity; }
        }

        internal int Count
        {
            get { lock (_gate) return _frames.Count; }
        }

        internal long StoredBytes
        {
            get { lock (_gate) return _storedImageBytes + _storedSceneGraphBytes; }
        }

        internal long SceneGraphBytesSaved
        {
            get { lock (_gate) return Math.Max(0L, _referencedSceneGraphBytes - _storedSceneGraphBytes); }
        }

        internal void Reset(long recordingId, int capacity)
        {
            lock (_gate)
            {
                _recordingId = recordingId;
                _capacity = Math.Max(1, capacity);
                _frames.Clear();
                _sceneGraphBlobs.Clear();
                _storedImageBytes = 0L;
                _storedSceneGraphBytes = 0L;
                _referencedSceneGraphBytes = 0L;
            }
        }

        internal void Clear(long recordingId = 0)
        {
            lock (_gate)
            {
                if (recordingId != 0 && recordingId != _recordingId)
                    return;
                _recordingId = 0;
                _capacity = 0;
                _frames.Clear();
                _sceneGraphBlobs.Clear();
                _storedImageBytes = 0L;
                _storedSceneGraphBytes = 0L;
                _referencedSceneGraphBytes = 0L;
            }
        }

        internal bool Add(RuntimeRecordedFrameData frame)
        {
            if (frame == null)
                return false;

            lock (_gate)
            {
                if (_recordingId == 0 || frame.RecordingId != _recordingId)
                    return false;

                if (_frames.TryGetValue(frame.UnityFrame, out RuntimeRecordedFrameData replaced))
                    RemoveFrame(replaced);
                Intern(frame.HierarchySection, out frame.HierarchySnapshotId,
                    out frame.HierarchyBytes);
                if (frame.GranularInspectorSections != null)
                    InternGranularInspector(frame);
                else
                    Intern(frame.InspectorSection, out frame.InspectorSnapshotId,
                        out frame.InspectorBytes);
                frame.HierarchySection = null;
                frame.InspectorSection = null;
                frame.GranularInspectorSections = null;
                _frames[frame.UnityFrame] = frame;
                _storedImageBytes += frame.JpegBytes?.LongLength ?? 0L;
                _referencedSceneGraphBytes += frame.ReferencedSceneGraphBytes;
                while (_frames.Count > _capacity)
                {
                    int oldestKey = 0;
                    RuntimeRecordedFrameData oldest = null;
                    foreach (KeyValuePair<int, RuntimeRecordedFrameData> pair in _frames)
                    {
                        oldestKey = pair.Key;
                        oldest = pair.Value;
                        break;
                    }
                    if (oldest == null)
                        break;
                    _frames.Remove(oldestKey);
                    RemoveFrame(oldest);
                }
                return true;
            }
        }

        internal bool TryGet(int unityFrame, out RuntimeRecordedFrameData frame)
        {
            lock (_gate)
                return _frames.TryGetValue(unityFrame, out frame);
        }

        internal bool TryGetSceneGraphBlob(string snapshotId, out byte[] bytes)
        {
            lock (_gate)
            {
                if (!string.IsNullOrEmpty(snapshotId) &&
                    _sceneGraphBlobs.TryGetValue(snapshotId, out SceneGraphBlob blob))
                {
                    bytes = blob.CompressedBytes;
                    return true;
                }

                bytes = null;
                return false;
            }
        }

        internal RemoteRecordedFrameInfo[] GetManifest()
        {
            lock (_gate)
            {
                var result = new RemoteRecordedFrameInfo[_frames.Count];
                int index = 0;
                foreach (RuntimeRecordedFrameData frame in _frames.Values)
                    result[index++] = frame.ToInfo();
                return result;
            }
        }

        internal bool TryGetInspectorManifest(string snapshotId,
            out RemoteRecordedInspectorManifest manifest)
        {
            lock (_gate)
            {
                foreach (RuntimeRecordedFrameData frame in _frames.Values)
                {
                    if (!string.Equals(frame.InspectorSnapshotId, snapshotId,
                            StringComparison.Ordinal) || frame.InspectorManifest == null)
                        continue;
                    manifest = frame.InspectorManifest;
                    return true;
                }

                manifest = null;
                return false;
            }
        }

        private void InternGranularInspector(RuntimeRecordedFrameData frame)
        {
            RuntimeRecordedInspectorSectionData granular = frame.GranularInspectorSections;
            frame.InspectorManifest = granular.Manifest;
            Intern(granular.ManifestSection, out frame.InspectorSnapshotId,
                out int manifestBytes);
            RuntimeSceneGraphSectionData[] payloads = granular.PayloadSections ??
                                                       Array.Empty<RuntimeSceneGraphSectionData>();
            frame.InspectorPayloadSnapshotIds = new string[payloads.Length];
            frame.InspectorBytes = manifestBytes;
            for (int i = 0; i < payloads.Length; i++)
            {
                Intern(payloads[i], out frame.InspectorPayloadSnapshotIds[i],
                    out int payloadBytes);
                frame.InspectorBytes += payloadBytes;
            }
        }

        private void Intern(RuntimeSceneGraphSectionData section, out string snapshotId,
            out int compressedBytes)
        {
            snapshotId = section?.SnapshotId;
            compressedBytes = 0;
            if (section == null || string.IsNullOrEmpty(snapshotId))
                return;

            if (_sceneGraphBlobs.TryGetValue(snapshotId, out SceneGraphBlob existing))
            {
                existing.ReferenceCount++;
                compressedBytes = existing.CompressedBytes.Length;
                return;
            }

            byte[] compressed = Compress(section.Utf8Bytes ?? Array.Empty<byte>());
            _sceneGraphBlobs.Add(snapshotId, new SceneGraphBlob
            {
                CompressedBytes = compressed,
                ReferenceCount = 1
            });
            compressedBytes = compressed.Length;
            _storedSceneGraphBytes += compressed.Length;
        }

        private void RemoveFrame(RuntimeRecordedFrameData frame)
        {
            _storedImageBytes -= frame.JpegBytes?.LongLength ?? 0L;
            _referencedSceneGraphBytes -= frame.ReferencedSceneGraphBytes;
            ReleaseBlob(frame.HierarchySnapshotId);
            ReleaseBlob(frame.InspectorSnapshotId);
            string[] payloadIds = frame.InspectorPayloadSnapshotIds ?? Array.Empty<string>();
            for (int i = 0; i < payloadIds.Length; i++)
                ReleaseBlob(payloadIds[i]);
        }

        private void ReleaseBlob(string snapshotId)
        {
            if (string.IsNullOrEmpty(snapshotId) ||
                !_sceneGraphBlobs.TryGetValue(snapshotId, out SceneGraphBlob blob))
                return;
            blob.ReferenceCount--;
            if (blob.ReferenceCount > 0)
                return;
            _storedSceneGraphBytes -= blob.CompressedBytes?.LongLength ?? 0L;
            _sceneGraphBlobs.Remove(snapshotId);
        }

        private static byte[] Compress(byte[] bytes)
        {
            using var output = new MemoryStream();
            using (var gzip = new GZipStream(output,
                       System.IO.Compression.CompressionLevel.Fastest, true))
                gzip.Write(bytes, 0, bytes.Length);
            return output.ToArray();
        }

        private sealed class SceneGraphBlob
        {
            internal byte[] CompressedBytes;
            internal int ReferenceCount;
        }
    }
}
