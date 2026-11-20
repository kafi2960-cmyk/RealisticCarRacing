using System.Collections;
using NUnit.Framework;
using RealisticCarRacing.Cameras;
using RealisticCarRacing.Controls;
using RealisticCarRacing.UI;
using RealisticCarRacing.Vehicles;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;

namespace RealisticCarRacing.Tests
{
    /// <summary>
    /// Drives the real car with simulated devices, one input method per test: keyboard, gamepad and
    /// touch (real touches on the on-screen controls). Every path goes through the Input System into
    /// <see cref="PlayerVehicleInput"/> and the same <see cref="VehicleController"/> physics.
    /// </summary>
    public class DrivingInputEndToEndTests : InputTestFixture
    {
        GameObject ground;
        VehicleSettings settings;
        VehicleController car;
        VehicleCameraRig rig;

        public override void Setup()
        {
            base.Setup();
            ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.transform.localScale = new Vector3(300f, 1f, 300f);
            settings = ScriptableObject.CreateInstance<VehicleSettings>();
            car = TestVehicleFactory.Create(settings, new Vector3(0f, 0.5f, 0f), Quaternion.identity, addPlayerInput: true);
            var cam = new GameObject("PlayerCamera").AddComponent<Camera>();
            rig = cam.gameObject.AddComponent<VehicleCameraRig>();
            rig.SetTarget(car.transform);
            cam.gameObject.AddComponent<PlayerCameraInput>();
        }

        public override void TearDown()
        {
            foreach (var root in new Object[] { rig.gameObject, car.gameObject, ground, settings })
                Object.DestroyImmediate(root);
            foreach (var canvas in Object.FindObjectsByType<Canvas>())
                Object.DestroyImmediate(canvas.gameObject);
            foreach (var eventSystem in Object.FindObjectsByType<EventSystem>())
                Object.DestroyImmediate(eventSystem.gameObject);
            base.TearDown();
        }

        static IEnumerator Simulate(float seconds)
        {
            float end = Time.fixedTime + seconds;
            while (Time.fixedTime < end)
                yield return new WaitForFixedUpdate();
            yield return null;
        }

        VehicleWheel RearWheel()
        {
            foreach (var wheel in car.Wheels)
            {
                if (!wheel.IsFront)
                    return wheel;
            }
            return null;
        }

        [UnityTest]
        public IEnumerator Keyboard_AcceleratesSteersBrakesReversesHandbrakesAndSwitchesCamera()
        {
            var keyboard = InputSystem.AddDevice<Keyboard>();
            yield return Simulate(1f);

            Press(keyboard.wKey);
            yield return Simulate(3f);
            Assert.That(car.ForwardSpeed, Is.GreaterThan(8f), "W accelerates");

            Press(keyboard.dKey);
            yield return Simulate(0.8f);
            Assert.That(car.SteerAngle, Is.GreaterThan(5f), "D steers right");
            Release(keyboard.dKey);
            yield return null;
            Press(keyboard.leftArrowKey);
            yield return Simulate(1f);
            Assert.That(car.SteerAngle, Is.LessThan(-5f), "Left arrow steers left");
            Release(keyboard.leftArrowKey);
            yield return null;
            Release(keyboard.wKey);
            yield return null;

            Press(keyboard.spaceKey);
            yield return Simulate(0.3f);
            Assert.That(car.CurrentInput.Handbrake, Is.True, "Space is the handbrake");
            Assert.That(RearWheel().wheelCollider.brakeTorque, Is.GreaterThanOrEqualTo(settings.handbrakeTorque));
            Release(keyboard.spaceKey);
            yield return null;

            Press(keyboard.sKey);
            yield return Simulate(7f);
            Assert.That(car.Gear, Is.EqualTo(VehicleTransmission.Reverse), "holding S at a stop selects reverse");
            Assert.That(car.ForwardSpeed, Is.LessThan(-1f), "S reverses");
            Release(keyboard.sKey);
            yield return null;

            Press(keyboard.upArrowKey);
            yield return Simulate(5f);
            Assert.That(car.ForwardSpeed, Is.GreaterThan(1f), "Up arrow brakes the reverse and drives forward again");
            Release(keyboard.upArrowKey);
            yield return null;

            var before = rig.View;
            PressAndRelease(keyboard.cKey);
            yield return null;
            yield return null;
            Assert.That(rig.View, Is.Not.EqualTo(before), "C changes camera");
        }

        [UnityTest]
        public IEnumerator Gamepad_AnalogSteeringAndTriggerPedals()
        {
            var gamepad = InputSystem.AddDevice<Gamepad>();
            yield return Simulate(1f);

            Set(gamepad.rightTrigger, 0.5f);
            yield return Simulate(0.5f);
            Assert.That(car.CurrentInput.Throttle, Is.EqualTo(0.5f).Within(0.02f), "half trigger = half throttle");
            Set(gamepad.rightTrigger, 1f);
            yield return Simulate(2.5f);
            Assert.That(car.ForwardSpeed, Is.GreaterThan(6f), "right trigger accelerates");

            Set(gamepad.leftStick, new Vector2(0.5f, 0f));
            yield return Simulate(1f);
            float halfSteer = car.CurrentInput.Steer, halfAngle = car.SteerAngle;
            Assert.That(halfSteer, Is.InRange(0.3f, 0.7f), "half stick gives partial steering");
            Assert.That(halfAngle, Is.EqualTo(halfSteer * car.GetSteerLimit(car.ForwardSpeed)).Within(1.5f));

            Set(gamepad.leftStick, new Vector2(1f, 0f));
            yield return Simulate(1f);
            Assert.That(car.CurrentInput.Steer, Is.EqualTo(1f).Within(0.02f));
            Assert.That(car.SteerAngle, Is.GreaterThan(halfAngle * 1.4f), "steering is proportional to the stick");

            Set(gamepad.leftStick, Vector2.zero);
            Set(gamepad.rightTrigger, 0f);
            Set(gamepad.leftTrigger, 1f);
            yield return Simulate(7f);
            Assert.That(car.Gear, Is.EqualTo(VehicleTransmission.Reverse), "left trigger brakes, then reverses");
            Assert.That(car.ForwardSpeed, Is.LessThan(-1f));
            Set(gamepad.leftTrigger, 0f);

            Press(gamepad.buttonSouth);
            yield return Simulate(0.3f);
            Assert.That(car.CurrentInput.Handbrake, Is.True, "A / Cross is the handbrake");
            Release(gamepad.buttonSouth);

            var before = rig.View;
            PressAndRelease(gamepad.rightStickButton);
            yield return null;
            yield return null;
            Assert.That(rig.View, Is.Not.EqualTo(before), "right-stick click changes camera");

            rig.SetView(CameraView.Chase);
            float distance = rig.ChaseDistance;
            Press(gamepad.dpad.up);
            yield return Simulate(0.4f);
            Release(gamepad.dpad.up);
            Assert.That(rig.ChaseDistance, Is.LessThan(distance - 1f), "D-pad up moves the chase camera closer");
        }

        [UnityTest]
        public IEnumerator Touch_OnScreenControlsDriveTheCar()
        {
            InputSystem.AddDevice<Touchscreen>();
            var ui = TouchControlsFactory.Create();
            yield return null;
            Canvas.ForceUpdateCanvases();
            Assert.That(ui.throttle.gameObject.activeInHierarchy, Is.True, "touch controls appear when a touchscreen is present");
            yield return Simulate(1f);

            Vector2 gas = Center(ui.throttle);
            BeginTouch(1, gas);
            yield return Simulate(3f);
            Assert.That(car.ForwardSpeed, Is.GreaterThan(6f), "GAS accelerates");

            // Second finger drags the steering knob to the right while the first holds GAS.
            Vector2 knob = Center(ui.steeringKnob);
            Vector2 steered = knob + Vector2.right * (140f * ui.canvas.scaleFactor);
            BeginTouch(2, knob);
            yield return null;
            MoveTouch(2, steered);
            yield return Simulate(0.8f);
            Assert.That(car.CurrentInput.Steer, Is.GreaterThan(0.6f), "dragging the knob right steers right");
            Assert.That(car.SteerAngle, Is.GreaterThan(3f));
            EndTouch(2, steered);
            EndTouch(1, gas);
            yield return null;

            Vector2 hand = Center(ui.handbrake);
            BeginTouch(3, hand);
            yield return Simulate(0.3f);
            Assert.That(car.CurrentInput.Handbrake, Is.True, "HAND applies the handbrake");
            EndTouch(3, hand);
            yield return null;

            Vector2 brake = Center(ui.brake);
            BeginTouch(4, brake);
            yield return Simulate(7f);
            Assert.That(car.Gear, Is.EqualTo(VehicleTransmission.Reverse), "holding BRAKE/REV at a stop selects reverse");
            Assert.That(car.ForwardSpeed, Is.LessThan(-1f));
            EndTouch(4, brake);
            yield return null;

            var before = rig.View;
            Vector2 cameraButton = Center(ui.cameraButton);
            BeginTouch(5, cameraButton);
            yield return null;
            yield return null;
            EndTouch(5, cameraButton);
            yield return null;
            Assert.That(rig.View, Is.Not.EqualTo(before), "CAM changes camera");
        }

        static Vector2 Center(RectTransform rect) =>
            RectTransformUtility.WorldToScreenPoint(null, rect.TransformPoint(rect.rect.center));
    }
}
