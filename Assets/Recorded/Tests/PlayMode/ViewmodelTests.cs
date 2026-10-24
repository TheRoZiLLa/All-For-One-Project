using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace Recorded.Tests
{
    public sealed class ViewmodelTests
    {
        private GameObject player, wall;
        private Camera worldCamera, overlayCamera;
        private FirstPersonViewmodel viewmodel;
        private Transform hands;
        private readonly Vector3 origin = new Vector3(1000f, 0f, 1000f);

        [SetUp]
        public void SetUp()
        {
            player = new GameObject("Viewmodel test player");
            player.SetActive(false);
            player.transform.position = origin;
            var body = player.AddComponent<CharacterController>();
            body.height = 1.8f;
            body.radius = 0.3f;
            body.center = Vector3.up * 0.9f;
            worldCamera = new GameObject("World camera", typeof(Camera)).GetComponent<Camera>();
            worldCamera.transform.SetParent(player.transform, false);
            worldCamera.transform.localPosition = Vector3.up * 1.65f;
            overlayCamera = new GameObject("Overlay camera", typeof(Camera)).GetComponent<Camera>();
            overlayCamera.transform.SetParent(worldCamera.transform, false);
            var holder = new GameObject("Viewmodel");
            holder.transform.SetParent(worldCamera.transform, false);
            hands = new GameObject("Hands").transform;
            hands.SetParent(holder.transform, false);
            hands.localPosition = new Vector3(0.3f, -0.25f, 0.6f);
            viewmodel = holder.AddComponent<FirstPersonViewmodel>();
            SetField("worldCamera", worldCamera);
            SetField("viewmodelCamera", overlayCamera);
            viewmodel.enabled = false;
            player.SetActive(true);
            Physics.SyncTransforms();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(player);
            if (wall != null) Object.DestroyImmediate(wall);
        }

        [Test]
        public void HandsAndOverlay_FollowPitchYawRollAndFovWithoutDelay()
        {
            Vector3 pose = hands.localPosition;
            worldCamera.transform.localRotation = Quaternion.Euler(35f, 80f, 7f);
            worldCamera.transform.localPosition += new Vector3(0.002f, 0.006f, 0f);
            worldCamera.fieldOfView = 92f;
            Tick(1f / 60f);
            Assert.That(Vector3.Distance(hands.position, worldCamera.transform.TransformPoint(pose)), Is.LessThan(0.001f));
            Assert.That(Vector3.Distance(overlayCamera.transform.position, worldCamera.transform.position), Is.LessThan(0.001f));
            Assert.That(Quaternion.Angle(overlayCamera.transform.rotation, worldCamera.transform.rotation), Is.LessThan(0.001f));
            Assert.That(overlayCamera.fieldOfView, Is.EqualTo(92f));
            Assert.That(viewmodel.WallProximity, Is.EqualTo(0f));
        }

        [TestCase(30)]
        [TestCase(60)]
        [TestCase(144)]
        public void NearbyWall_RetractsSmoothly_AndIgnoresOwnColliders(int fps)
        {
            wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.transform.position = origin + new Vector3(0f, 1.65f, 0.45f);
            wall.transform.localScale = new Vector3(3f, 3f, 0.1f);
            var ownCollider = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ownCollider.transform.SetParent(player.transform, false);
            ownCollider.transform.localPosition = new Vector3(0f, 1.65f, 0.2f);
            ownCollider.transform.localScale = Vector3.one * 0.05f;
            Physics.SyncTransforms();
            Tick(1f / fps);
            Assert.That(viewmodel.transform.localPosition.z, Is.LessThan(0f).And.GreaterThan(-0.05f));
            for (int i = 0; i < fps; i++) Tick(1f / fps);
            Assert.That(viewmodel.WallProximity, Is.GreaterThan(0.5f).And.LessThanOrEqualTo(1f));
            Assert.That(viewmodel.transform.localPosition.z, Is.LessThan(-0.04f).And.GreaterThanOrEqualTo(-0.081f));
            Assert.That(viewmodel.transform.localEulerAngles.x, Is.GreaterThan(5f).And.LessThan(10.1f));
            wall.transform.position += Vector3.forward * 10f;
            Physics.SyncTransforms();
            for (int i = 0; i < fps; i++) Tick(1f / fps);
            Assert.That(viewmodel.WallProximity, Is.LessThan(0.001f), "The player's own collider must not lower the weapon.");
            Assert.That(viewmodel.transform.localPosition.magnitude, Is.LessThan(0.001f));
            viewmodel.enabled = true;
            viewmodel.enabled = false;
            Assert.That(viewmodel.transform.localPosition, Is.EqualTo(Vector3.zero));
            Assert.That(Quaternion.Angle(viewmodel.transform.localRotation, Quaternion.identity), Is.LessThan(0.001f));
        }

        private void Tick(float dt) => typeof(FirstPersonViewmodel)
            .GetMethod("UpdateViewmodel", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(viewmodel, new object[] { dt });

        private void SetField(string name, object value) => typeof(FirstPersonViewmodel)
            .GetField(name, BindingFlags.NonPublic | BindingFlags.Instance).SetValue(viewmodel, value);
    }
}
