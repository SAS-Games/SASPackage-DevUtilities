using NUnit.Framework;
using SAS.Utilities.RemoteDevUtilities.Editor.Client;
using SAS.Utilities.RemoteDevUtilities.Editor.RuntimeSceneInspector;
using SAS.Utilities.RemoteDevUtilities.Protocol;
using SAS.Utilities.RemoteDevUtilities.Protocol.RuntimeSceneInspector;
using SAS.Utilities.RemoteDevUtilities.Protocol.RuntimeSceneInspector.Capture;
using SAS.Utilities.RemoteDevUtilities.Protocol.Serialization;
using SAS.Utilities.RemoteDevUtilities.RuntimeSceneInspector.Capture;
using SAS.Utilities.RuntimeSceneInspector;
using SAS.Utilities.RuntimeSceneInspector.Core;
using UnityEngine;
using RemoteMessageTypes = SAS.Utilities.RemoteDevUtilities.Protocol.RuntimeSceneInspector.RemoteSceneInspectorMessageTypes;

namespace SAS.Utilities.RemoteDevUtilities.SceneInspector.Tests
{
    public sealed class RemoteSceneCaptureTests
    {
        [Test]
        public void CaptureResponse_RoundTripsImageAndFreezeState()
        {
            var response = new RemoteSceneCaptureResponse
            {
                CaptureId = 17,
                ImageBase64 = "AQIDBA==",
                Width = 960,
                Height = 540,
                FrameCount = 123,
                PlayerFrozen = true
            };

            byte[] bytes = RemoteProtocolSerializer.Serialize(RemoteMessageTypes.SceneInspectorCaptureResponse, 5,
                "runtime", response);

            Assert.That(RemoteProtocolSerializer.TryDeserializeEnvelope(bytes, out RemoteEnvelope envelope,
                out string error), Is.True, error);
            Assert.That(RemoteProtocolSerializer.TryDeserializePayload(envelope,
                out RemoteSceneCaptureResponse copy, out error), Is.True, error);
            Assert.That(copy.CaptureId, Is.EqualTo(17));
            Assert.That(copy.ImageBase64, Is.EqualTo("AQIDBA=="));
            Assert.That(copy.Width, Is.EqualTo(960));
            Assert.That(copy.Height, Is.EqualTo(540));
            Assert.That(copy.PlayerFrozen, Is.True);
        }

        [Test]
        public void PickRequest_RoundTripsNormalizedCoordinates()
        {
            var request = new RemoteScenePickRequest
            {
                CaptureId = 4,
                NormalizedX = 0.25f,
                NormalizedY = 0.75f
            };

            byte[] bytes = RemoteProtocolSerializer.Serialize(RemoteMessageTypes.SceneInspectorPickRequest, 6,
                "editor", request);

            Assert.That(RemoteProtocolSerializer.TryDeserializeEnvelope(bytes, out RemoteEnvelope envelope,
                out string error), Is.True, error);
            Assert.That(RemoteProtocolSerializer.TryDeserializePayload(envelope,
                out RemoteScenePickRequest copy, out error), Is.True, error);
            Assert.That(copy.CaptureId, Is.EqualTo(4));
            Assert.That(copy.NormalizedX, Is.EqualTo(0.25f));
            Assert.That(copy.NormalizedY, Is.EqualTo(0.75f));
        }

        [Test]
        public void RemoteHierarchy_SelectAndRevealSelectsPickedObject()
        {
            var hierarchy = new RemoteSceneInspectorHierarchyResponse
            {
                Revision = 1,
                Entries = new[]
                {
                    new RemoteHierarchyEntry { Id = 100, Kind = 0, Name = "Scene" },
                    new RemoteHierarchyEntry { Id = 10, ParentId = 100, Kind = 1, Name = "Parent" },
                    new RemoteHierarchyEntry { Id = 11, ParentId = 10, Kind = 1, Name = "Picked" }
                }
            };
            var view = new RemoteHierarchyView();

            Assert.That(view.SelectAndReveal(11, hierarchy), Is.True);
            Assert.That(view.SelectedObjectId, Is.EqualTo(11));
        }

        [Test]
        public void TimeScaleFreezeLease_RestoresPreviousValue()
        {
            float original = Time.timeScale;
            var lease = new RuntimeTimeScaleFreezeLease();
            try
            {
                Time.timeScale = 0.35f;
                lease.Acquire();
                Assert.That(Time.timeScale, Is.Zero);
                Assert.That(lease.IsAcquired, Is.True);

                lease.Release();
                Assert.That(Time.timeScale, Is.EqualTo(0.35f));
                Assert.That(lease.IsAcquired, Is.False);
            }
            finally
            {
                lease.Release();
                Time.timeScale = original;
            }
        }

        [Test]
        public void Capture_RemainsActiveAfterSuccessfulPickAndAllowsCandidateSelection()
        {
            var session = new StubSession();
            var client = new RemoteRuntimeSceneInspectorClient(session);
            client.RequestCapture(true);
            long captureRequestId = session.LastRequestId;
            client.Handle(CreateEnvelope(RemoteMessageTypes.SceneInspectorCaptureResponse, captureRequestId,
                new RemoteSceneCaptureResponse
                {
                    CaptureId = 8,
                    ImageBase64 = "AQIDBA==",
                    Width = 2,
                    Height = 2,
                    PlayerFrozen = true
                }));

            client.Pick(8, 0.5f, 0.5f);
            long pickRequestId = session.LastRequestId;
            client.Handle(CreateEnvelope(RemoteMessageTypes.SceneInspectorPickResponse, pickRequestId,
                new RemoteScenePickResponse
                {
                    CaptureId = 8,
                    Found = true,
                    ObjectId = 21,
                    Candidates = new[]
                    {
                        new RemoteScenePickCandidate { ObjectId = 21, Name = "Helmet", Source = "Renderer" },
                        new RemoteScenePickCandidate { ObjectId = 22, Name = "Player", Source = "Collider3D" }
                    }
                }));

            Assert.That(client.IsCaptureActive, Is.True);
            Assert.That(client.Capture.PlayerFrozen, Is.True);
            Assert.That(client.LastPickedObjectId, Is.EqualTo(21));

            int previousRevision = client.PickRevision;
            client.SelectPickedObject(22);
            Assert.That(client.LastPickedObjectId, Is.EqualTo(22));
            Assert.That(client.PickRevision, Is.EqualTo(previousRevision + 1));
            Assert.That(client.IsCaptureActive, Is.True);
        }

        [Test]
        public void Picker_OrdersOverlapping2DRenderersBySortingOrder()
        {
            RuntimeSceneInspectorSettings settings = ScriptableObject.CreateInstance<RuntimeSceneInspectorSettings>();
            var cameraObject = new GameObject("Pick Test Camera");
            var backObject = new GameObject("Back Sprite") { layer = 31 };
            var frontObject = new GameObject("Front Sprite") { layer = 31 };
            var texture = new Texture2D(2, 2);
            Sprite sprite = Sprite.Create(texture, new Rect(0f, 0f, 2f, 2f), new Vector2(0.5f, 0.5f), 1f);
            try
            {
                Camera camera = cameraObject.AddComponent<Camera>();
                camera.transform.position = new Vector3(0f, 0f, -10f);
                camera.orthographic = true;
                camera.orthographicSize = 2f;
                camera.cullingMask = 1 << 31;
                camera.pixelRect = new Rect(0f, 0f, 100f, 100f);

                SpriteRenderer back = backObject.AddComponent<SpriteRenderer>();
                back.sprite = sprite;
                back.sortingOrder = 1;
                SpriteRenderer front = frontObject.AddComponent<SpriteRenderer>();
                front.sprite = sprite;
                front.sortingOrder = 10;

                using var service = new RuntimeSceneInspectorService(settings);
                service.RefreshHierarchy();
                var picker = new RuntimeSceneObjectPicker(settings, service);
                System.Collections.Generic.IReadOnlyList<RuntimeScenePickCandidate> candidates =
                    picker.GetCandidates(new Vector2(50f, 50f), out string error);

                Assert.That(candidates.Count, Is.GreaterThanOrEqualTo(2), error);
                Assert.That(candidates[0].Name, Is.EqualTo("Front Sprite"));
                Assert.That(candidates[1].Name, Is.EqualTo("Back Sprite"));
            }
            finally
            {
                Object.DestroyImmediate(cameraObject);
                Object.DestroyImmediate(backObject);
                Object.DestroyImmediate(frontObject);
                Object.DestroyImmediate(sprite);
                Object.DestroyImmediate(texture);
                Object.DestroyImmediate(settings);
            }
        }

        private static RemoteEnvelope CreateEnvelope<T>(string messageType, long requestId, T payload)
        {
            return new RemoteEnvelope
            {
                MessageType = messageType,
                RequestId = requestId,
                PayloadJson = JsonUtility.ToJson(payload)
            };
        }

        private sealed class StubSession : IRemoteEditorSession
        {
            private long _nextRequestId;
            internal long LastRequestId { get; private set; }
            public bool IsConnected => true;

            public long Send<T>(string messageType, T payload)
            {
                LastRequestId = ++_nextRequestId;
                return LastRequestId;
            }

            public void NotifyStateChanged()
            {
            }
        }
    }
}
