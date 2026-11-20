using RealisticCarRacing.Cameras;
using UnityEngine;

namespace RealisticCarRacing.Controls
{
    /// <summary>Connects <see cref="CameraInputActions"/> (keyboard, mouse, gamepad, touch CAM button) to a <see cref="VehicleCameraRig"/>.</summary>
    [RequireComponent(typeof(VehicleCameraRig))]
    public sealed class PlayerCameraInput : MonoBehaviour
    {
        [Tooltip("Metres per second while a zoom key or D-pad direction is held.")]
        [SerializeField] float zoomSpeed = 6f;
        [Tooltip("Metres per mouse-wheel notch.")]
        [SerializeField] float scrollStep = 0.75f;

        CameraInputActions actions;
        VehicleCameraRig rig;

        public CameraInputActions Actions => actions;

        void Awake()
        {
            rig = GetComponent<VehicleCameraRig>();
            actions = new CameraInputActions();
            actions.CycleView.performed += _ => rig.CycleView();
        }

        void OnEnable() => actions.Enable();

        void OnDisable() => actions.Disable();

        void OnDestroy() => actions.Dispose();

        void Update()
        {
            // Positive zoom input means "closer", i.e. a shorter chase distance.
            float held = actions.Zoom.ReadValue<float>();
            float scroll = Mathf.Clamp(actions.ZoomScroll.ReadValue<float>(), -2f, 2f);
            float change = -(held * zoomSpeed * Time.deltaTime + scroll * scrollStep);
            if (change != 0f)
                rig.AdjustDistance(change);
            rig.SetOrbitInput(actions.Orbit.ReadValue<float>());
        }
    }
}
