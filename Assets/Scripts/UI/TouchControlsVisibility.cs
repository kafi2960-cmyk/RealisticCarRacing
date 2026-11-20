using UnityEngine;
using UnityEngine.InputSystem;

namespace RealisticCarRacing.UI
{
    /// <summary>
    /// Shows the on-screen driving controls on touch devices and hides them elsewhere.
    /// While hidden, the on-screen controls are disabled and remove their virtual gamepad.
    /// </summary>
    public sealed class TouchControlsVisibility : MonoBehaviour
    {
        public enum Mode
        {
            Auto,
            AlwaysShow,
            AlwaysHide
        }

        [Tooltip("Auto shows the controls on mobile platforms or when a touchscreen is connected.")]
        [SerializeField] Mode mode = Mode.Auto;
        [SerializeField] GameObject controlsRoot;

        public void Configure(GameObject root, Mode visibilityMode)
        {
            controlsRoot = root;
            mode = visibilityMode;
            Refresh();
        }

        void OnEnable()
        {
            InputSystem.onDeviceChange += OnDeviceChange;
            Refresh();
        }

        void OnDisable() => InputSystem.onDeviceChange -= OnDeviceChange;

        void OnDeviceChange(InputDevice device, InputDeviceChange change)
        {
            if (device is Touchscreen)
                Refresh();
        }

        void Refresh()
        {
            if (controlsRoot == null)
                return;
            bool show = mode switch
            {
                Mode.AlwaysShow => true,
                Mode.AlwaysHide => false,
                _ => Application.isMobilePlatform || Touchscreen.current != null
            };
            controlsRoot.SetActive(show);
        }
    }
}
