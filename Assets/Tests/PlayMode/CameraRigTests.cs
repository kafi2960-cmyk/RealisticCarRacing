using System.Collections;
using NUnit.Framework;
using RealisticCarRacing.Cameras;
using RealisticCarRacing.Controls;
using RealisticCarRacing.Vehicles;
using UnityEngine;
using UnityEngine.TestTools;

namespace RealisticCarRacing.Tests
{
    /// <summary>Chase / hood / cockpit behaviour of <see cref="VehicleCameraRig"/>, driven directly (no devices).</summary>
    public class CameraRigTests
    {
        sealed class ScriptedInput : IVehicleInputSource
        {
            public VehicleInputState State;
            public VehicleInputState ReadInput() => State;
        }

        GameObject ground;
        VehicleSettings settings;
        VehicleController car;
        ScriptedInput input;
        VehicleCameraRig rig;
        Camera cam;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.transform.localScale = new Vector3(100f, 1f, 100f);
            settings = ScriptableObject.CreateInstance<VehicleSettings>();
            car = TestVehicleFactory.Create(settings, new Vector3(0f, 0.5f, 0f), Quaternion.identity, addPlayerInput: false);
            input = new ScriptedInput();
            car.SetInputSource(input);
            cam = new GameObject("TestCamera").AddComponent<Camera>();
            rig = cam.gameObject.AddComponent<VehicleCameraRig>();
            rig.SetTarget(car.transform);
            yield return Simulate(1f);
            yield return null;
        }

        [TearDown]
        public void TearDown()
        {
            Object.Destroy(cam.gameObject);
            Object.Destroy(car.gameObject);
            Object.Destroy(ground);
            Object.Destroy(settings);
        }

        static IEnumerator Simulate(float seconds)
        {
            float end = Time.time + seconds;
            while (Time.time < end)
                yield return null;
        }

        static Vector3 Flat(Vector3 v) => Vector3.ProjectOnPlane(v, Vector3.up);

        float HorizontalDistance => Flat(car.transform.position - cam.transform.position).magnitude;

        [UnityTest]
        public IEnumerator Chase_SitsBehindAndAboveTheCar()
        {
            Assert.That(rig.View, Is.EqualTo(CameraView.Chase));
            Vector3 toCar = Flat(car.transform.position - cam.transform.position).normalized;
            Assert.That(Vector3.Dot(toCar, Flat(car.transform.forward).normalized), Is.GreaterThan(0.98f), "camera should be directly behind");
            Assert.That(HorizontalDistance, Is.EqualTo(rig.ChaseDistance).Within(0.3f));
            Assert.That(cam.transform.position.y, Is.GreaterThan(car.transform.position.y + 1f));
            yield break;
        }

        [UnityTest]
        public IEnumerator Chase_SwingsRoundSmoothlyWhenTheCarTurns()
        {
            var turned = Quaternion.Euler(0f, 90f, 0f);
            car.Body.rotation = turned;
            car.transform.rotation = turned;
            yield return null;
            yield return null;

            float early = Vector3.Angle(Flat(cam.transform.forward), Flat(car.transform.forward));
            Assert.That(early, Is.GreaterThan(45f), "chase camera should ease round, not snap");

            yield return Simulate(3f);
            float late = Vector3.Angle(Flat(cam.transform.forward), Flat(car.transform.forward));
            Assert.That(late, Is.LessThan(5f), "chase camera should end up behind the car again");
        }

        [UnityTest]
        public IEnumerator Chase_DistanceIsAdjustableAndClamped()
        {
            float start = rig.ChaseDistance;
            rig.AdjustDistance(3f);
            yield return Simulate(2f);
            Assert.That(HorizontalDistance, Is.EqualTo(start + 3f).Within(0.3f));

            rig.AdjustDistance(100f);
            Assert.That(rig.ChaseDistance, Is.EqualTo(rig.MaxChaseDistance));
            rig.AdjustDistance(-100f);
            Assert.That(rig.ChaseDistance, Is.EqualTo(rig.MinChaseDistance));
            yield return Simulate(2f);
            Assert.That(HorizontalDistance, Is.EqualTo(rig.MinChaseDistance).Within(0.3f));
        }

        [UnityTest]
        public IEnumerator Chase_StaysInFrontOfAWallBehindTheCar()
        {
            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.transform.position = car.transform.position - car.transform.forward * 4f + Vector3.up * 2f;
            wall.transform.localScale = new Vector3(10f, 6f, 0.5f);
            yield return null;
            yield return null;

            float wallFront = wall.transform.position.z + 0.25f;
            Assert.That(cam.transform.position.z, Is.GreaterThan(wallFront), "camera went through the wall");
            Assert.That(HorizontalDistance, Is.LessThan(4f));
            Object.Destroy(wall);
        }

        [UnityTest]
        public IEnumerator HoodAndCockpit_FollowTheirMountsLookingForward()
        {
            var mounts = car.GetComponent<VehicleCameraMounts>();
            foreach (var (view, mount) in new[] { (CameraView.Hood, mounts.Hood), (CameraView.Cockpit, mounts.Cockpit) })
            {
                rig.SetView(view);
                yield return null;
                Assert.That(Vector3.Distance(cam.transform.position, mount.position), Is.LessThan(0.01f), $"{view} position");
                Assert.That(Vector3.Angle(cam.transform.forward, car.transform.forward), Is.LessThan(1f), $"{view} direction");
                Assert.That(cam.nearClipPlane, Is.LessThan(0.1f), $"{view} near clip must not cut the car interior");
            }

            Assert.That(mounts.InteriorVisible, Is.True, "the cabin interior is drawn from the driver's seat");
            rig.SetView(CameraView.Chase);
            Assert.That(mounts.InteriorVisible, Is.False, "and skipped from outside (it can't be seen)");
            rig.SetView(CameraView.Cockpit);
            yield return null;

            // From the driver's seat the steering wheel is in view, low and ahead.
            var wheel = car.transform.Find("Interior/SteeringWheel");
            Vector3 viewport = cam.WorldToViewportPoint(wheel.position);
            Assert.That(viewport.z, Is.GreaterThan(cam.nearClipPlane));
            Assert.That(viewport.x, Is.InRange(0f, 1f));
            Assert.That(viewport.y, Is.InRange(0f, 0.5f));
        }

        [UnityTest]
        public IEnumerator CycleView_GoesChaseHoodCockpitAndBack()
        {
            var seen = new System.Collections.Generic.List<CameraView>();
            rig.ViewChanged += seen.Add;
            for (int i = 0; i < 3; i++)
            {
                rig.CycleView();
                yield return null;
            }
            Assert.That(seen, Is.EqualTo(new[] { CameraView.Hood, CameraView.Cockpit, CameraView.Chase }));
        }

        [UnityTest]
        public IEnumerator Cockpit_SteeringWheelTurnsWithTheFrontWheels()
        {
            var wheel = car.transform.Find("Interior/SteeringWheel");
            Quaternion rest = wheel.localRotation;
            // A quarter lock keeps the rim rotation under 180°, where Quaternion.Angle is unambiguous.
            input.State.Steer = 0.25f;
            yield return Simulate(1f);
            Assert.That(car.SteerAngle, Is.GreaterThan(7f));
            float expected = car.SteerAngle * 13f; // SteeringWheelVisual's default ratio
            Assert.That(Quaternion.Angle(rest, wheel.localRotation), Is.EqualTo(expected).Within(2f), "steering wheel should turn with the front wheels");
        }
    }
}
