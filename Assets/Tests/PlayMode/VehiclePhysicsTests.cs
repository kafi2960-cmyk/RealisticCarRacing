using System.Collections;
using NUnit.Framework;
using RealisticCarRacing.Controls;
using RealisticCarRacing.Vehicles;
using UnityEngine;
using UnityEngine.TestTools;

namespace RealisticCarRacing.Tests
{
    /// <summary>Drives the primitive test car on a flat plane with scripted input and checks its behaviour.</summary>
    public class VehiclePhysicsTests
    {
        sealed class ScriptedInput : IVehicleInputSource
        {
            public VehicleInputState State;

            public VehicleInputState ReadInput()
            {
                var state = State;
                State.ShiftUp = State.ShiftDown = State.ToggleTransmission = State.ResetVehicle = false;
                return state;
            }
        }

        GameObject ground;
        VehicleSettings settings;
        VehicleController car;
        ScriptedInput input;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.transform.localScale = new Vector3(300f, 1f, 300f);
            settings = ScriptableObject.CreateInstance<VehicleSettings>();
            car = TestVehicleFactory.Create(settings, new Vector3(0f, 0.5f, 0f), Quaternion.identity, addPlayerInput: false);
            input = new ScriptedInput();
            car.SetInputSource(input);
            yield return Simulate(1.5f); // settle onto the suspension
        }

        [TearDown]
        public void TearDown()
        {
            Object.Destroy(car.gameObject);
            Object.Destroy(ground);
            Object.Destroy(settings);
        }

        static IEnumerator Simulate(float seconds)
        {
            float end = Time.fixedTime + seconds;
            while (Time.fixedTime < end)
                yield return new WaitForFixedUpdate();
        }

        float Heading => Vector3.SignedAngle(Vector3.forward, Vector3.ProjectOnPlane(car.transform.forward, Vector3.up), Vector3.up);

        [UnityTest]
        public IEnumerator RestsOnSuspension_WithoutCreeping()
        {
            Vector3 start = car.transform.position;
            yield return Simulate(1f);

            foreach (var wheel in car.Wheels)
                Assert.That(wheel.wheelCollider.isGrounded, Is.True, $"{wheel.wheelCollider.name} should touch the ground");
            Assert.That(Vector3.Distance(start, car.transform.position), Is.LessThan(0.05f));
            Assert.That(Vector3.Dot(car.transform.up, Vector3.up), Is.GreaterThan(0.99f));
        }

        [UnityTest]
        public IEnumerator Throttle_AcceleratesForward()
        {
            input.State.Throttle = 1f;
            yield return Simulate(5f);

            Assert.That(car.ForwardSpeed, Is.GreaterThan(12f));
            Assert.That(car.transform.position.z, Is.GreaterThan(20f));
            Assert.That(Mathf.Abs(Heading), Is.LessThan(5f), "straight-line acceleration should stay straight");
        }

        [UnityTest]
        public IEnumerator Automatic_UpshiftsWhileAccelerating()
        {
            input.State.Throttle = 1f;
            yield return Simulate(10f);

            Assert.That(car.Gear, Is.GreaterThanOrEqualTo(3));
            Assert.That(car.EngineRpm, Is.LessThan(settings.redlineRpm));
        }

        [UnityTest]
        public IEnumerator Brake_StopsTheCar()
        {
            input.State.Throttle = 1f;
            yield return Simulate(4f);
            float speedBeforeBraking = car.ForwardSpeed;
            Assert.That(speedBeforeBraking, Is.GreaterThan(10f));

            input.State = new VehicleInputState { Brake = 1f };
            float elapsed = 0f;
            while (car.ForwardSpeed > 0.5f && elapsed < 5f)
            {
                yield return new WaitForFixedUpdate();
                elapsed += Time.fixedDeltaTime;
            }

            Assert.That(car.ForwardSpeed, Is.LessThanOrEqualTo(0.5f), $"still at {car.ForwardSpeed:0.0} m/s after {elapsed:0.0} s of braking");
            Assert.That(Mathf.Abs(Heading), Is.LessThan(5f), "straight-line braking should stay straight");
        }

        [UnityTest]
        public IEnumerator Automatic_HoldingBrakeAtRestReverses()
        {
            input.State.Brake = 1f;
            yield return Simulate(4f);

            Assert.That(car.Gear, Is.EqualTo(VehicleTransmission.Reverse));
            Assert.That(car.ForwardSpeed, Is.LessThan(-2f));

            // Throttle while reversing brakes, then selects drive once stopped.
            input.State = new VehicleInputState { Throttle = 1f };
            yield return Simulate(4f);
            Assert.That(car.Gear, Is.GreaterThanOrEqualTo(1));
            Assert.That(car.ForwardSpeed, Is.GreaterThan(1f));
        }

        [UnityTest]
        public IEnumerator Steering_TurnsTheCarBothWays()
        {
            input.State = new VehicleInputState { Throttle = 0.4f, Steer = 1f };
            yield return Simulate(4f);
            float rightHeading = Heading;
            Assert.That(rightHeading, Is.GreaterThan(45f), "positive steer should turn right");

            input.State = new VehicleInputState { Throttle = 0.4f, Steer = -1f };
            yield return Simulate(6f);
            Assert.That(Heading, Is.LessThan(rightHeading - 45f), "negative steer should turn left");
        }

        [UnityTest]
        public IEnumerator Steering_UsesAckermannAndSpeedSensitivity()
        {
            input.State.Steer = 1f;
            yield return Simulate(1f);

            var frontLeft = FindWheel(Axle.Front, isLeft: true).wheelCollider;
            var frontRight = FindWheel(Axle.Front, isLeft: false).wheelCollider;
            Assert.That(car.SteerAngle, Is.EqualTo(settings.maxSteerAngle).Within(0.5f));
            Assert.That(frontRight.steerAngle, Is.GreaterThan(frontLeft.steerAngle), "inner (right) wheel turns more in a right turn");

            input.State = new VehicleInputState { Throttle = 1f };
            yield return Simulate(8f);
            input.State.Steer = 1f;
            yield return Simulate(0.5f);
            Assert.That(car.SteerAngle, Is.LessThan(settings.maxSteerAngle - 5f), "steering lock should shrink at speed");
        }

        [UnityTest]
        public IEnumerator Handbrake_LocksRearWheels()
        {
            input.State.Throttle = 1f;
            yield return Simulate(3f);
            float speedBefore = car.ForwardSpeed;

            input.State = new VehicleInputState { Handbrake = true };
            yield return Simulate(0.5f);

            Assert.That(Mathf.Abs(FindWheel(Axle.Rear, true).wheelCollider.rpm), Is.LessThan(5f));
            Assert.That(Mathf.Abs(FindWheel(Axle.Rear, false).wheelCollider.rpm), Is.LessThan(5f));
            Assert.That(Mathf.Abs(FindWheel(Axle.Front, true).wheelCollider.rpm), Is.GreaterThan(50f), "front wheels keep rolling");
            Assert.That(car.ForwardSpeed, Is.LessThan(speedBefore));
        }

        [UnityTest]
        public IEnumerator Manual_ShiftsOnRequestAndReversesFromStandstill()
        {
            input.State.ToggleTransmission = true;
            yield return Simulate(0.1f);
            Assert.That(car.TransmissionMode, Is.EqualTo(TransmissionMode.Manual));
            Assert.That(car.Gear, Is.EqualTo(1));

            input.State.ShiftUp = true;
            yield return Simulate(0.1f);
            Assert.That(car.Gear, Is.EqualTo(2));

            // 2 -> 1 -> N -> R, one request per physics step.
            for (int i = 0; i < 3; i++)
            {
                input.State.ShiftDown = true;
                yield return new WaitForFixedUpdate();
                yield return new WaitForFixedUpdate();
            }
            Assert.That(car.Gear, Is.EqualTo(VehicleTransmission.Reverse));

            input.State.Throttle = 1f; // in manual, the throttle drives whichever gear is selected
            yield return Simulate(3f);
            Assert.That(car.ForwardSpeed, Is.LessThan(-2f));

            // Shifting up from reverse reaches neutral, but first gear is refused while rolling backwards.
            input.State = new VehicleInputState { ShiftUp = true };
            yield return new WaitForFixedUpdate();
            yield return new WaitForFixedUpdate();
            input.State.ShiftUp = true;
            yield return new WaitForFixedUpdate();
            yield return new WaitForFixedUpdate();
            Assert.That(car.Gear, Is.EqualTo(VehicleTransmission.Neutral));
        }

        [UnityTest]
        public IEnumerator WheelVisuals_FollowSteeringAndSpin()
        {
            var frontLeft = FindWheel(Axle.Front, isLeft: true);
            input.State.Steer = 1f;
            yield return Simulate(1f);
            yield return null; // let Update copy the pose onto the visuals

            float visualSteer = Vector3.SignedAngle(car.transform.forward, frontLeft.visual.forward, car.transform.up);
            Assert.That(visualSteer, Is.EqualTo(frontLeft.wheelCollider.steerAngle).Within(1f));

            input.State = new VehicleInputState { Throttle = 0.5f };
            yield return Simulate(1f);
            yield return null;
            var rear = FindWheel(Axle.Rear, isLeft: true).visual;
            Quaternion before = Quaternion.Inverse(car.transform.rotation) * rear.rotation;
            yield return Simulate(0.1f);
            yield return null;
            Quaternion after = Quaternion.Inverse(car.transform.rotation) * rear.rotation;
            Assert.That(Quaternion.Angle(before, after), Is.GreaterThan(5f), "rolling wheels should spin visibly");
        }

        [UnityTest]
        public IEnumerator Reset_PutsAnUpturnedCarBackOnItsWheels()
        {
            car.Body.rotation = Quaternion.Euler(0f, 30f, 180f);
            car.Body.position += Vector3.up * 2f;
            yield return Simulate(2f);
            Assert.That(Vector3.Dot(car.transform.up, Vector3.up), Is.LessThan(0f), "car should be lying on its roof");

            input.State.ResetVehicle = true;
            yield return Simulate(2f);
            Assert.That(Vector3.Dot(car.transform.up, Vector3.up), Is.GreaterThan(0.95f));
        }

        VehicleWheel FindWheel(Axle axle, bool isLeft)
        {
            foreach (var wheel in car.Wheels)
            {
                if (wheel.axle == axle && wheel.isLeft == isLeft)
                    return wheel;
            }
            return null;
        }
    }
}
