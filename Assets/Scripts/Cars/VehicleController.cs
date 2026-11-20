using System.Collections.Generic;
using RealisticCarRacing.Controls;
using UnityEngine;

namespace RealisticCarRacing.Vehicles
{
    /// <summary>
    /// Drives a Rigidbody car through four WheelColliders: engine, gearbox, brakes, handbrake,
    /// speed-sensitive Ackermann steering, anti-roll bars and simple aerodynamics.
    /// All motion comes from wheel torques and forces; the transform is only written directly
    /// by <see cref="ResetVehicle"/>.
    /// Driver commands come from an <see cref="IVehicleInputSource"/> on the same GameObject,
    /// or one assigned with <see cref="SetInputSource"/> (AI, network, tests).
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    [DisallowMultipleComponent]
    public sealed class VehicleController : MonoBehaviour
    {
        const float AirDensity = 1.225f;
        const float PedalThreshold = 0.1f;

        [SerializeField] VehicleSettings settings;
        [SerializeField] VehicleWheel[] wheels = new VehicleWheel[4];

        Rigidbody body;
        IVehicleInputSource inputSource;
        VehicleEngine engine;
        VehicleTransmission transmission;
        VehicleWheel frontLeft, frontRight, rearLeft, rearRight;
        int drivenWheelCount;
        float wheelbase;
        float trackWidth;
        float steerAngle;
        bool handbrakeGripApplied;

        public VehicleSettings Settings => settings;
        public Rigidbody Body => body;
        public IReadOnlyList<VehicleWheel> Wheels => wheels;
        public VehicleInputState CurrentInput { get; private set; }
        /// <summary>Brake pedal actually applied this physics step (0..1), after reverse-gear pedal mapping. Drives the brake lights.</summary>
        public float BrakeInput { get; private set; }
        /// <summary>Accelerator actually applied to the engine this step, 0..1 (in reverse the brake pedal drives).</summary>
        public float AppliedThrottle { get; private set; }
        /// <summary>Engine output this step in N·m (negative while engine braking; 0 with the clutch open).</summary>
        public float EngineTorque { get; private set; }
        public bool IsRevLimited => engine.IsRevLimited;

        /// <summary>Speed along the car's forward axis in m/s; negative when moving backwards.</summary>
        public float ForwardSpeed => Vector3.Dot(body.linearVelocity, transform.forward);
        public float SpeedKmh => body.linearVelocity.magnitude * 3.6f;
        public float EngineRpm => engine.Rpm;
        public int Gear => transmission.Gear;
        public string GearLabel => transmission.GearLabel;
        public bool IsShifting => transmission.IsShifting;
        public TransmissionMode TransmissionMode => transmission.Mode;
        /// <summary>Current steering rack angle in degrees (before Ackermann correction).</summary>
        public float SteerAngle => steerAngle;
        /// <summary>Distance between front and rear axles in metres.</summary>
        public float Wheelbase => wheelbase;

        /// <summary>For vehicles built in code: call while the GameObject is still inactive.</summary>
        public void Configure(VehicleSettings vehicleSettings, VehicleWheel[] vehicleWheels)
        {
            settings = vehicleSettings;
            wheels = vehicleWheels;
        }

        public void SetInputSource(IVehicleInputSource source) => inputSource = source;

        public IVehicleInputSource InputSource => inputSource;

        /// <summary>While true (e.g. during a race countdown) driver input is ignored and the car is held still.</summary>
        public bool InputLocked { get; set; }

        /// <summary>Where <see cref="ResetVehicle"/> puts the car, e.g. back on the racing line. Null = upright on the spot.</summary>
        public System.Func<Pose> RespawnPoseProvider { get; set; }

        /// <summary>Raised after <see cref="ResetVehicle"/> has moved the car.</summary>
        public event System.Action Respawned;

        public void SetTransmissionMode(TransmissionMode mode) => transmission.Mode = mode;

        public void ToggleTransmissionMode() =>
            SetTransmissionMode(transmission.Mode == TransmissionMode.Automatic ? TransmissionMode.Manual : TransmissionMode.Automatic);

        void Awake()
        {
            body = GetComponent<Rigidbody>();
            if (!FindWheels())
            {
                enabled = false;
                return;
            }

            if (inputSource == null)
                inputSource = GetComponent<IVehicleInputSource>();
            engine = new VehicleEngine(settings);
            transmission = new VehicleTransmission(settings);
            ApplySettings();
        }

        bool FindWheels()
        {
            if (settings == null)
            {
                Debug.LogError($"{name}: VehicleController has no VehicleSettings assigned.", this);
                return false;
            }

            frontLeft = frontRight = rearLeft = rearRight = null;
            foreach (var wheel in wheels)
            {
                if (wheel?.wheelCollider == null)
                    continue;
                if (wheel.IsFront && wheel.isLeft) frontLeft = wheel;
                else if (wheel.IsFront) frontRight = wheel;
                else if (wheel.isLeft) rearLeft = wheel;
                else rearRight = wheel;
            }

            if (frontLeft == null || frontRight == null || rearLeft == null || rearRight == null)
            {
                Debug.LogError($"{name}: VehicleController needs one wheel for each corner (front/rear × left/right).", this);
                return false;
            }
            return true;
        }

        /// <summary>Pushes the settings asset onto the Rigidbody and WheelColliders. Call again after changing settings at runtime.</summary>
        public void ApplySettings()
        {
            body.mass = settings.mass;
            body.ResetCenterOfMass();
            body.centerOfMass += settings.centerOfMassOffset;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.collisionDetectionMode = settings.collisionDetection;
            body.linearDamping = 0f; // drag and rolling resistance are modelled explicitly
            body.angularDamping = 0.05f;

            drivenWheelCount = 0;
            foreach (var wheel in wheels)
            {
                var wc = wheel.wheelCollider;
                wc.mass = settings.wheelMass;
                wc.radius = settings.wheelRadius;
                wc.suspensionDistance = settings.suspensionDistance;
                wc.suspensionSpring = new JointSpring
                {
                    spring = settings.springRate,
                    damper = settings.damperRate,
                    targetPosition = settings.suspensionTargetPosition
                };
                wc.forwardFriction = settings.forwardFriction.ToCurve();
                wc.sidewaysFriction = settings.sidewaysFriction.ToCurve();
                if (IsDriven(wheel))
                    drivenWheelCount++;
            }
            handbrakeGripApplied = false;

            // Applies to every WheelCollider on this Rigidbody.
            frontLeft.wheelCollider.ConfigureVehicleSubsteps(
                settings.substepSpeedThreshold, settings.substepsBelowThreshold, settings.substepsAboveThreshold);

            Vector3 fl = transform.InverseTransformPoint(frontLeft.wheelCollider.transform.position);
            Vector3 fr = transform.InverseTransformPoint(frontRight.wheelCollider.transform.position);
            Vector3 rl = transform.InverseTransformPoint(rearLeft.wheelCollider.transform.position);
            wheelbase = Mathf.Abs(fl.z - rl.z);
            trackWidth = Mathf.Abs(fr.x - fl.x);
        }

        void FixedUpdate()
        {
            float dt = Time.fixedDeltaTime;
            // Locked: no pedals (so the automatic gearbox doesn't select reverse), handbrake on.
            var input = InputLocked ? new VehicleInputState { Handbrake = true }
                : inputSource != null ? inputSource.ReadInput().Clamped() : default;
            CurrentInput = input;

            if (input.ResetVehicle)
            {
                ResetVehicle();
                return;
            }
            if (input.ToggleTransmission)
                ToggleTransmissionMode();

            float forwardSpeed = ForwardSpeed;
            transmission.Tick(dt);
            SelectGear(input, forwardSpeed, out float throttle, out float brake);
            BrakeInput = brake;
            AppliedThrottle = throttle;

            if (transmission.Mode == TransmissionMode.Automatic)
                transmission.UpdateAutomatic(DrivetrainRpm());

            bool clutchEngaged = transmission.Gear != VehicleTransmission.Neutral && !transmission.IsShifting;
            float engineTorque = engine.Update(throttle, DrivetrainRpm(), clutchEngaged, transmission.IsShifting, dt);
            EngineTorque = engineTorque;
            float wheelTorque = engineTorque * transmission.OverallRatio * settings.drivetrainEfficiency / Mathf.Max(1, drivenWheelCount);

            if (throttle > 0f && body.IsSleeping())
                body.WakeUp(); // WheelCollider torque alone doesn't wake a sleeping body

            ApplySteering(input.Steer, forwardSpeed, dt);
            ApplyWheelTorques(wheelTorque, throttle, brake, input.Handbrake, forwardSpeed);
            ApplyAntiRoll(frontLeft, frontRight, settings.frontAntiRoll);
            ApplyAntiRoll(rearLeft, rearRight, settings.rearAntiRoll);
            ApplyResistanceForces(forwardSpeed);
        }

        void Update()
        {
            // Runs after Rigidbody interpolation, so the wheels stay locked to the rendered body.
            foreach (var wheel in wheels)
                wheel.SyncVisual();
        }

        /// <summary>Handles gear requests and maps the pedals to throttle/brake for the current direction.</summary>
        void SelectGear(VehicleInputState input, float forwardSpeed, out float throttle, out float brake)
        {
            if (transmission.Mode == TransmissionMode.Manual)
            {
                if (input.ShiftUp)
                    transmission.ShiftUp(forwardSpeed);
                if (input.ShiftDown)
                    transmission.ShiftDown(forwardSpeed);
                throttle = input.Throttle;
                brake = input.Brake;
                return;
            }

            // Automatic: once (nearly) stopped, holding the brake selects reverse and holding the
            // throttle selects drive. While reversing the pedals swap roles, as in most racing games.
            bool nearlyStopped = Mathf.Abs(forwardSpeed) < settings.directionChangeSpeed;
            bool wantsReverse = input.Brake > PedalThreshold && input.Throttle < PedalThreshold;
            bool wantsDrive = input.Throttle > PedalThreshold && input.Brake < PedalThreshold;

            if (transmission.Gear == VehicleTransmission.Reverse)
            {
                if (nearlyStopped && wantsDrive)
                    transmission.TryShiftTo(1, forwardSpeed);
            }
            else if (nearlyStopped && wantsReverse)
            {
                transmission.TryShiftTo(VehicleTransmission.Reverse, forwardSpeed);
            }
            else if (transmission.Gear == VehicleTransmission.Neutral && wantsDrive)
            {
                transmission.TryShiftTo(1, forwardSpeed);
            }

            bool reversing = transmission.Gear == VehicleTransmission.Reverse;
            throttle = reversing ? input.Brake : input.Throttle;
            brake = reversing ? input.Throttle : input.Brake;
        }

        /// <summary>Engine speed implied by the driven wheels through the current gear.</summary>
        float DrivetrainRpm()
        {
            float wheelRpm = 0f;
            foreach (var wheel in wheels)
            {
                if (IsDriven(wheel))
                    wheelRpm += wheel.wheelCollider.rpm;
            }
            wheelRpm /= Mathf.Max(1, drivenWheelCount);
            return Mathf.Abs(wheelRpm * transmission.OverallRatio);
        }

        bool IsDriven(VehicleWheel wheel) => settings.driveType switch
        {
            DriveType.FrontWheelDrive => wheel.IsFront,
            DriveType.RearWheelDrive => !wheel.IsFront,
            _ => true
        };

        /// <summary>Steering lock in degrees at the given speed (it narrows as speed rises).</summary>
        public float GetSteerLimit(float forwardSpeed)
        {
            float speedFactor = Mathf.InverseLerp(0f, settings.highSpeedSteerKmh, Mathf.Abs(forwardSpeed) * 3.6f);
            return Mathf.Lerp(settings.maxSteerAngle, settings.highSpeedSteerAngle, speedFactor);
        }

        void ApplySteering(float steerInput, float forwardSpeed, float dt)
        {
            steerAngle = Mathf.MoveTowards(steerAngle, steerInput * GetSteerLimit(forwardSpeed), settings.steerSpeed * dt);

            frontLeft.wheelCollider.steerAngle = WheelSteerAngle(steerAngle, isLeft: true);
            frontRight.wheelCollider.steerAngle = WheelSteerAngle(steerAngle, isLeft: false);
        }

        /// <summary>Ackermann geometry: the inner wheel follows a tighter circle than the outer one.</summary>
        float WheelSteerAngle(float angle, bool isLeft)
        {
            if (!settings.ackermannSteering || Mathf.Abs(angle) < 0.01f || wheelbase <= 0f)
                return angle;

            float turnRadius = wheelbase / Mathf.Tan(Mathf.Abs(angle) * Mathf.Deg2Rad);
            bool isInner = angle > 0f != isLeft; // a right turn (positive angle) makes the right wheel the inner one
            float wheelRadius = turnRadius + (isInner ? -0.5f : 0.5f) * trackWidth;
            return Mathf.Sign(angle) * Mathf.Atan(wheelbase / wheelRadius) * Mathf.Rad2Deg;
        }

        void ApplyWheelTorques(float wheelTorque, float throttle, float brake, bool handbrake, float forwardSpeed)
        {
            float frontBrake = brake * settings.maxBrakeTorque * settings.frontBrakeBias * 0.5f;
            float rearBrake = brake * settings.maxBrakeTorque * (1f - settings.frontBrakeBias) * 0.5f;
            bool holdAtRest = throttle < 0.05f && brake < 0.05f && Mathf.Abs(forwardSpeed) < 0.3f;

            foreach (var wheel in wheels)
            {
                var wc = wheel.wheelCollider;
                wc.motorTorque = IsDriven(wheel) ? wheelTorque : 0f;

                float brakeTorque = wheel.IsFront ? frontBrake : rearBrake;
                if (handbrake && !wheel.IsFront)
                    brakeTorque += settings.handbrakeTorque;
                if (holdAtRest)
                    brakeTorque = Mathf.Max(brakeTorque, settings.holdBrakeTorque);
                wc.brakeTorque = brakeTorque;
            }

            // Locked rear wheels also lose lateral grip, which is what lets the handbrake rotate the car.
            if (handbrake != handbrakeGripApplied)
            {
                var sideways = settings.sidewaysFriction.ToCurve(handbrake ? settings.handbrakeRearGrip : 1f);
                rearLeft.wheelCollider.sidewaysFriction = sideways;
                rearRight.wheelCollider.sidewaysFriction = sideways;
                handbrakeGripApplied = handbrake;
            }
        }

        void ApplyAntiRoll(VehicleWheel left, VehicleWheel right, float stiffness)
        {
            bool leftGrounded = left.TryGetSuspensionTravel(out float leftTravel);
            bool rightGrounded = right.TryGetSuspensionTravel(out float rightTravel);
            float force = (leftTravel - rightTravel) * stiffness;

            Transform l = left.wheelCollider.transform;
            Transform r = right.wheelCollider.transform;
            if (leftGrounded)
                body.AddForceAtPosition(l.up * -force, l.position);
            if (rightGrounded)
                body.AddForceAtPosition(r.up * force, r.position);
        }

        void ApplyResistanceForces(float forwardSpeed)
        {
            Vector3 velocity = body.linearVelocity;
            float speed = velocity.magnitude;
            if (speed < 0.01f)
                return;

            Vector3 force = -velocity * (0.5f * AirDensity * settings.dragCoefficient * settings.frontalArea * speed);

            if (AnyWheelGrounded())
            {
                force -= transform.up * (settings.downforceCoefficient * speed * speed);
                if (Mathf.Abs(forwardSpeed) > 0.5f)
                    force -= transform.forward * (Mathf.Sign(forwardSpeed) * settings.rollingResistance * body.mass * -Physics.gravity.y);
            }

            body.AddForce(force);
        }

        bool AnyWheelGrounded()
        {
            foreach (var wheel in wheels)
            {
                if (wheel.wheelCollider.isGrounded)
                    return true;
            }
            return false;
        }

        /// <summary>
        /// Puts the car back on its wheels: at <see cref="RespawnPoseProvider"/>'s pose if set, otherwise one metre
        /// above its current position, keeping its heading.
        /// </summary>
        public void ResetVehicle()
        {
            Vector3 position;
            Quaternion rotation;
            if (RespawnPoseProvider != null)
            {
                Pose pose = RespawnPoseProvider();
                position = pose.position;
                rotation = pose.rotation;
            }
            else
            {
                Vector3 heading = Vector3.ProjectOnPlane(transform.forward, Vector3.up);
                if (heading.sqrMagnitude < 0.001f)
                    heading = Vector3.ProjectOnPlane(transform.up, Vector3.up);
                if (heading.sqrMagnitude < 0.001f)
                    heading = Vector3.forward;
                position = transform.position + Vector3.up;
                rotation = Quaternion.LookRotation(heading.normalized, Vector3.up);
            }

            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
            body.position = position;
            body.rotation = rotation;
            transform.SetPositionAndRotation(position, rotation);
            steerAngle = 0f;
            Respawned?.Invoke();
        }

        void OnDrawGizmosSelected()
        {
            if (body == null)
                return;
            Gizmos.color = Color.yellow;
            Gizmos.DrawSphere(body.worldCenterOfMass, 0.1f);
        }
    }
}
