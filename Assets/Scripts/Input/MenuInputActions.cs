using System;
using UnityEngine.InputSystem;

namespace RealisticCarRacing.Controls
{
    /// <summary>
    /// Menu-level actions. Moving between and pressing buttons is handled by the UI input module
    /// (arrows/WASD/D-pad/stick to move, Enter/A to press, pointer/touch to click); these cover Back and Pause.
    /// </summary>
    public sealed class MenuInputActions : IDisposable
    {
        public InputActionMap Map { get; }
        public InputAction Back { get; }
        public InputAction Pause { get; }

        public MenuInputActions()
        {
            Map = new InputActionMap("Menu");

            Back = Map.AddAction("Back", InputActionType.Button);
            Back.AddBinding("<Keyboard>/escape");
            Back.AddBinding("<Keyboard>/backspace");
            Back.AddBinding("<Gamepad>/buttonEast");

            Pause = Map.AddAction("Pause", InputActionType.Button);
            Pause.AddBinding("<Keyboard>/escape");
            Pause.AddBinding("<Gamepad>/start");
        }

        public void Enable() => Map.Enable();
        public void Disable() => Map.Disable();
        public void Dispose() => Map.Dispose();
    }
}
