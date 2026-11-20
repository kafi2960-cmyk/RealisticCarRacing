using System.Collections;
using NUnit.Framework;
using RealisticCarRacing.Controls;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.OnScreen;
using UnityEngine.TestTools;

namespace RealisticCarRacing.Tests
{
    /// <summary>Feeds simulated keyboard, gamepad and on-screen (touch) input through the Input System.</summary>
    public class PlayerVehicleInputTests : InputTestFixture
    {
        Keyboard keyboard;
        Gamepad gamepad;
        GameObject player;
        PlayerVehicleInput playerInput;

        public override void Setup()
        {
            base.Setup();
            keyboard = InputSystem.AddDevice<Keyboard>();
            gamepad = InputSystem.AddDevice<Gamepad>();
            player = new GameObject("Player");
            playerInput = player.AddComponent<PlayerVehicleInput>();
        }

        public override void TearDown()
        {
            Object.DestroyImmediate(player);
            base.TearDown();
        }

        [UnityTest]
        public IEnumerator Keyboard_PedalsRampUpAndRelease()
        {
            Press(keyboard.wKey);
            yield return null;
            // One Update has run with this frame's deltaTime at the default 4/s keyboard pedal rate.
            float expected = Mathf.Min(1f, 4f * Time.deltaTime);
            Assert.That(playerInput.ReadInput().Throttle, Is.EqualTo(expected).Within(0.01f), "keyboard throttle should ramp, not jump");

            yield return new WaitForSeconds(0.5f);
            Assert.That(playerInput.ReadInput().Throttle, Is.EqualTo(1f).Within(0.001f));

            Release(keyboard.wKey);
            // Keys share one bit-packed state block; a second key event queued in the same frame
            // would carry a stale snapshot and undo the release, so let the release land first.
            yield return null;
            Press(keyboard.sKey);
            yield return new WaitForSeconds(0.5f);
            var state = playerInput.ReadInput();
            Assert.That(state.Throttle, Is.EqualTo(0f).Within(0.001f));
            Assert.That(state.Brake, Is.EqualTo(1f).Within(0.001f));
        }

        [UnityTest]
        public IEnumerator Keyboard_SteersBothWaysAndRecentres()
        {
            Press(keyboard.dKey);
            yield return new WaitForSeconds(0.6f);
            Assert.That(playerInput.ReadInput().Steer, Is.EqualTo(1f).Within(0.001f));

            Release(keyboard.dKey);
            yield return null;
            Press(keyboard.leftArrowKey);
            yield return new WaitForSeconds(1f);
            Assert.That(playerInput.ReadInput().Steer, Is.EqualTo(-1f).Within(0.001f));

            Release(keyboard.leftArrowKey);
            yield return new WaitForSeconds(0.5f);
            Assert.That(playerInput.ReadInput().Steer, Is.EqualTo(0f).Within(0.001f));
        }

        [UnityTest]
        public IEnumerator Keyboard_HandbrakeShiftToggleAndReset()
        {
            Press(keyboard.spaceKey);
            yield return null;
            Assert.That(playerInput.ReadInput().Handbrake, Is.True);
            Release(keyboard.spaceKey);

            yield return null;
            PressAndRelease(keyboard.eKey);
            yield return null;
            PressAndRelease(keyboard.tKey);
            yield return null;
            var state = playerInput.ReadInput();
            Assert.That(state.ShiftUp, Is.True);
            Assert.That(state.ToggleTransmission, Is.True);
            Assert.That(playerInput.ReadInput().ShiftUp, Is.False, "one-shot requests are consumed by the read");

            PressAndRelease(keyboard.qKey);
            yield return null;
            PressAndRelease(keyboard.rKey);
            yield return null;
            state = playerInput.ReadInput();
            Assert.That(state.ShiftDown, Is.True);
            Assert.That(state.ResetVehicle, Is.True);
        }

        [UnityTest]
        public IEnumerator Gamepad_AnalogInputPassesStraightThrough()
        {
            Set(gamepad.rightTrigger, 0.6f);
            Set(gamepad.leftTrigger, 0.3f);
            Set(gamepad.leftStick, new Vector2(-1f, 0f));
            Press(gamepad.buttonSouth);
            yield return null;

            var state = playerInput.ReadInput();
            Assert.That(state.Throttle, Is.EqualTo(0.6f).Within(0.01f));
            Assert.That(state.Brake, Is.EqualTo(0.3f).Within(0.01f));
            Assert.That(state.Steer, Is.EqualTo(-1f).Within(0.01f));
            Assert.That(state.Handbrake, Is.True);

            PressAndRelease(gamepad.rightShoulder);
            PressAndRelease(gamepad.buttonNorth);
            PressAndRelease(gamepad.selectButton);
            yield return null;
            state = playerInput.ReadInput();
            Assert.That(state.ShiftUp && state.ToggleTransmission && state.ResetVehicle, Is.True);

            PressAndRelease(gamepad.leftShoulder);
            yield return null;
            Assert.That(playerInput.ReadInput().ShiftDown, Is.True);
        }

        [UnityTest]
        public IEnumerator Touch_OnScreenControlsDriveTheSameActions()
        {
            var touchUi = new GameObject("TouchControls");
            var gas = touchUi.AddComponent<OnScreenButton>();
            gas.controlPath = "<Gamepad>/rightTrigger";
            var handbrake = new GameObject("Handbrake").AddComponent<OnScreenButton>();
            handbrake.transform.SetParent(touchUi.transform);
            handbrake.controlPath = "<Gamepad>/buttonSouth";
            yield return null;

            gas.OnPointerDown(null);
            handbrake.OnPointerDown(null);
            InputSystem.Update();
            yield return null;
            var state = playerInput.ReadInput();
            Assert.That(state.Throttle, Is.EqualTo(1f).Within(0.01f));
            Assert.That(state.Handbrake, Is.True);

            gas.OnPointerUp(null);
            handbrake.OnPointerUp(null);
            InputSystem.Update();
            yield return null;
            state = playerInput.ReadInput();
            Assert.That(state.Throttle, Is.EqualTo(0f).Within(0.01f));
            Assert.That(state.Handbrake, Is.False);

            Object.DestroyImmediate(touchUi);
        }
    }
}
