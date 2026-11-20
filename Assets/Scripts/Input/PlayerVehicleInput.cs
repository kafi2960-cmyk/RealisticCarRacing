using UnityEngine;
using UnityEngine.InputSystem;

namespace RealisticCarRacing.Controls
{
    /// <summary>
    /// Reads keyboard, gamepad and on-screen touch controls through <see cref="VehicleInputActions"/>
    /// and hands them to the vehicle as a <see cref="VehicleInputState"/>.
    /// Keys are digital, so keyboard values are ramped to give progressive steering and pedals;
    /// analog triggers and sticks pass straight through.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PlayerVehicleInput : MonoBehaviour, IVehicleInputSource
    {
        [Header("Keyboard ramping (units per second)")]
        [SerializeField] float keyboardSteerRate = 2.5f;
        [SerializeField] float keyboardSteerReturnRate = 4f;
        [SerializeField] float keyboardPedalRate = 4f;
        [SerializeField] float keyboardPedalReleaseRate = 8f;

        VehicleInputActions actions;
        RampedAxis throttle;
        RampedAxis brake;
        RampedAxis steer;
        bool shiftUpRequested;
        bool shiftDownRequested;
        bool toggleTransmissionRequested;
        bool resetRequested;

        /// <summary>The live action map, e.g. for a rebinding menu.</summary>
        public VehicleInputActions Actions => actions;

        void Awake()
        {
            actions = new VehicleInputActions();
            throttle = new RampedAxis(actions.Throttle, keyboardPedalRate, keyboardPedalReleaseRate);
            brake = new RampedAxis(actions.Brake, keyboardPedalRate, keyboardPedalReleaseRate);
            steer = new RampedAxis(actions.Steer, keyboardSteerRate, keyboardSteerReturnRate);

            // Latched until the next ReadInput so a press between physics steps is never lost.
            actions.ShiftUp.performed += _ => shiftUpRequested = true;
            actions.ShiftDown.performed += _ => shiftDownRequested = true;
            actions.ToggleTransmission.performed += _ => toggleTransmissionRequested = true;
            actions.ResetVehicle.performed += _ => resetRequested = true;
        }

        void OnEnable() => actions.Enable();

        void OnDisable() => actions.Disable();

        void OnDestroy() => actions.Dispose();

        void Update()
        {
            float dt = Time.deltaTime;
            throttle.Update(dt);
            brake.Update(dt);
            steer.Update(dt);
        }

        public VehicleInputState ReadInput()
        {
            var state = new VehicleInputState
            {
                Throttle = throttle.Value,
                Brake = brake.Value,
                Steer = steer.Value,
                Handbrake = actions.Handbrake.IsPressed(),
                Horn = actions.Horn.IsPressed(),
                ShiftUp = shiftUpRequested,
                ShiftDown = shiftDownRequested,
                ToggleTransmission = toggleTransmissionRequested,
                ResetVehicle = resetRequested
            };
            shiftUpRequested = shiftDownRequested = toggleTransmissionRequested = resetRequested = false;
            return state;
        }

        /// <summary>Follows an axis action, ramping towards the target only while the input comes from a keyboard.</summary>
        sealed class RampedAxis
        {
            readonly InputAction action;
            readonly float riseRate;
            readonly float returnRate;
            bool lastInputWasKeyboard;

            public RampedAxis(InputAction action, float riseRate, float returnRate)
            {
                this.action = action;
                this.riseRate = riseRate;
                this.returnRate = returnRate;
            }

            public float Value { get; private set; }

            public void Update(float deltaTime)
            {
                float target = action.ReadValue<float>();
                var control = action.activeControl;
                if (control != null)
                    lastInputWasKeyboard = control.device is Keyboard;

                if (!lastInputWasKeyboard)
                {
                    Value = target;
                    return;
                }

                bool returning = Mathf.Abs(target) < Mathf.Abs(Value) || Mathf.Sign(target) != Mathf.Sign(Value);
                Value = Mathf.MoveTowards(Value, target, (returning ? returnRate : riseRate) * deltaTime);
            }
        }
    }
}
