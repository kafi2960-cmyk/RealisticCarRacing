using System;
using UnityEngine.InputSystem;

namespace RealisticCarRacing.Controls
{
    /// <summary>
    /// The "Vehicle" action map with keyboard and gamepad bindings. On-screen touch controls are bound
    /// to gamepad control paths (e.g. &lt;Gamepad&gt;/rightTrigger), so they reach these same actions.
    /// </summary>
    public sealed class VehicleInputActions : IDisposable
    {
        public InputActionMap Map { get; }
        public InputAction Throttle { get; }
        public InputAction Brake { get; }
        public InputAction Steer { get; }
        public InputAction Handbrake { get; }
        public InputAction ShiftUp { get; }
        public InputAction ShiftDown { get; }
        public InputAction ToggleTransmission { get; }
        public InputAction ResetVehicle { get; }

        public VehicleInputActions()
        {
            Map = new InputActionMap("Vehicle");

            Throttle = Map.AddAction("Throttle", InputActionType.Value, expectedControlLayout: "Axis");
            Throttle.AddBinding("<Keyboard>/w");
            Throttle.AddBinding("<Keyboard>/upArrow");
            Throttle.AddBinding("<Gamepad>/rightTrigger");

            Brake = Map.AddAction("Brake", InputActionType.Value, expectedControlLayout: "Axis");
            Brake.AddBinding("<Keyboard>/s");
            Brake.AddBinding("<Keyboard>/downArrow");
            Brake.AddBinding("<Gamepad>/leftTrigger");

            Steer = Map.AddAction("Steer", InputActionType.Value, expectedControlLayout: "Axis");
            Steer.AddCompositeBinding("1DAxis")
                .With("Negative", "<Keyboard>/a")
                .With("Positive", "<Keyboard>/d");
            Steer.AddCompositeBinding("1DAxis")
                .With("Negative", "<Keyboard>/leftArrow")
                .With("Positive", "<Keyboard>/rightArrow");
            Steer.AddBinding("<Gamepad>/leftStick/x");

            Handbrake = Map.AddAction("Handbrake", InputActionType.Button);
            Handbrake.AddBinding("<Keyboard>/space");
            Handbrake.AddBinding("<Gamepad>/buttonSouth");

            ShiftUp = Map.AddAction("ShiftUp", InputActionType.Button);
            ShiftUp.AddBinding("<Keyboard>/e");
            ShiftUp.AddBinding("<Gamepad>/rightShoulder");

            ShiftDown = Map.AddAction("ShiftDown", InputActionType.Button);
            ShiftDown.AddBinding("<Keyboard>/q");
            ShiftDown.AddBinding("<Gamepad>/leftShoulder");

            ToggleTransmission = Map.AddAction("ToggleTransmission", InputActionType.Button);
            ToggleTransmission.AddBinding("<Keyboard>/t");
            ToggleTransmission.AddBinding("<Gamepad>/buttonNorth");

            ResetVehicle = Map.AddAction("ResetVehicle", InputActionType.Button);
            ResetVehicle.AddBinding("<Keyboard>/r");
            ResetVehicle.AddBinding("<Gamepad>/select");
        }

        public void Enable() => Map.Enable();
        public void Disable() => Map.Disable();
        public void Dispose() => Map.Dispose();
    }
}
