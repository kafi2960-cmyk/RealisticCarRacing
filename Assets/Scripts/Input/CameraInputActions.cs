using System;
using UnityEngine.InputSystem;

namespace RealisticCarRacing.Controls
{
    /// <summary>
    /// The "Camera" action map, kept separate from <see cref="VehicleInputActions"/> because cameras are not
    /// part of the vehicle simulation. The on-screen CAM button is bound to &lt;Gamepad&gt;/rightStickPress.
    /// </summary>
    public sealed class CameraInputActions : IDisposable
    {
        public InputActionMap Map { get; }
        public InputAction CycleView { get; }
        /// <summary>Held zoom, -1 (further) .. 1 (closer).</summary>
        public InputAction Zoom { get; }
        /// <summary>Mouse wheel; positive scrolls up (closer).</summary>
        public InputAction ZoomScroll { get; }
        /// <summary>Look around the car in chase view, -1 .. 1.</summary>
        public InputAction Orbit { get; }

        public CameraInputActions()
        {
            Map = new InputActionMap("Camera");

            CycleView = Map.AddAction("CycleView", InputActionType.Button);
            CycleView.AddBinding("<Keyboard>/c");
            CycleView.AddBinding("<Gamepad>/rightStickPress");

            Zoom = Map.AddAction("Zoom", InputActionType.Value, expectedControlLayout: "Axis");
            Zoom.AddCompositeBinding("1DAxis")
                .With("Negative", "<Keyboard>/minus")
                .With("Positive", "<Keyboard>/equals");
            Zoom.AddCompositeBinding("1DAxis")
                .With("Negative", "<Keyboard>/numpadMinus")
                .With("Positive", "<Keyboard>/numpadPlus");
            Zoom.AddCompositeBinding("1DAxis")
                .With("Negative", "<Gamepad>/dpad/down")
                .With("Positive", "<Gamepad>/dpad/up");

            ZoomScroll = Map.AddAction("ZoomScroll", InputActionType.Value, expectedControlLayout: "Axis");
            ZoomScroll.AddBinding("<Mouse>/scroll/y");

            Orbit = Map.AddAction("Orbit", InputActionType.Value, expectedControlLayout: "Axis");
            Orbit.AddBinding("<Gamepad>/rightStick/x");
        }

        public void Enable() => Map.Enable();
        public void Disable() => Map.Disable();
        public void Dispose() => Map.Dispose();
    }
}
