using System;
using UnityEngine;

namespace RealisticCarRacing.Vehicles
{
    public enum DriveType
    {
        FrontWheelDrive,
        RearWheelDrive,
        AllWheelDrive
    }

    public enum TransmissionMode
    {
        Automatic,
        Manual
    }

    /// <summary>Serializable mirror of <see cref="WheelFrictionCurve"/> so tyre grip can be tuned in an asset.</summary>
    [Serializable]
    public struct TireFriction
    {
        public float extremumSlip;
        public float extremumValue;
        public float asymptoteSlip;
        public float asymptoteValue;
        [Tooltip("Overall grip multiplier.")]
        public float stiffness;

        public WheelFrictionCurve ToCurve(float stiffnessMultiplier = 1f) => new WheelFrictionCurve
        {
            extremumSlip = extremumSlip,
            extremumValue = extremumValue,
            asymptoteSlip = asymptoteSlip,
            asymptoteValue = asymptoteValue,
            stiffness = stiffness * stiffnessMultiplier
        };
    }

    /// <summary>
    /// Every tunable number for one car model. Create one asset per car
    /// (Assets &gt; Create &gt; RealisticCarRacing &gt; Vehicle Settings) and assign it to a <see cref="VehicleController"/>.
    /// Units are SI: kilograms, metres, newtons, newton-metres, seconds.
    /// </summary>
    [CreateAssetMenu(fileName = "VehicleSettings", menuName = "RealisticCarRacing/Vehicle Settings")]
    public sealed class VehicleSettings : ScriptableObject
    {
        [Header("Body")]
        [Min(100f)] public float mass = 1350f;
        [Tooltip("Added to the collider-derived centre of mass. Lower values make the car harder to roll.")]
        public Vector3 centerOfMassOffset = new Vector3(0f, -0.3f, 0.05f);
        [Tooltip("Aerodynamic drag coefficient (Cd).")]
        public float dragCoefficient = 0.32f;
        [Tooltip("Frontal area in m².")]
        public float frontalArea = 2.1f;
        [Tooltip("Downforce in newtons per (m/s)², applied while any wheel is grounded.")]
        public float downforceCoefficient = 1f;
        [Tooltip("Rolling resistance coefficient (Crr).")]
        public float rollingResistance = 0.015f;
        public CollisionDetectionMode collisionDetection = CollisionDetectionMode.ContinuousSpeculative;

        [Header("Engine")]
        public float idleRpm = 900f;
        public float redlineRpm = 7000f;
        [Tooltip("Crankshaft torque (N·m) by engine RPM.")]
        public AnimationCurve torqueCurve = new AnimationCurve(
            new Keyframe(800f, 180f),
            new Keyframe(2500f, 250f),
            new Keyframe(4500f, 285f),
            new Keyframe(6000f, 270f),
            new Keyframe(7000f, 230f));
        [Tooltip("How quickly engine RPM follows its target (per second).")]
        public float rpmResponse = 10f;
        [Tooltip("RPM the slipping clutch lets the engine reach when pulling away.")]
        public float launchRpm = 3200f;
        [Tooltip("Engine braking torque at redline with the throttle closed (N·m).")]
        public float engineBrakeTorque = 70f;

        [Header("Transmission")]
        public TransmissionMode defaultTransmission = TransmissionMode.Automatic;
        public DriveType driveType = DriveType.RearWheelDrive;
        public float[] forwardGearRatios = { 3.6f, 2.19f, 1.41f, 1.12f, 0.92f, 0.76f };
        public float reverseGearRatio = 3.4f;
        public float finalDriveRatio = 3.9f;
        [Range(0.5f, 1f)] public float drivetrainEfficiency = 0.85f;
        [Tooltip("Seconds without drive while a gear change completes.")]
        public float shiftDuration = 0.2f;
        [Tooltip("Automatic gearbox: shift up above this drivetrain RPM.")]
        public float upshiftRpm = 6300f;
        [Tooltip("Automatic gearbox: shift down below this drivetrain RPM.")]
        public float downshiftRpm = 2800f;
        [Tooltip("Automatic gearbox: minimum seconds between shifts, to stop it hunting.")]
        public float minTimeBetweenShifts = 0.6f;
        [Tooltip("Below this speed (m/s) the gearbox may change between drive and reverse.")]
        public float directionChangeSpeed = 1f;

        [Header("Brakes")]
        [Tooltip("Total brake torque across all four wheels (N·m).")]
        public float maxBrakeTorque = 9000f;
        [Range(0f, 1f)] public float frontBrakeBias = 0.65f;
        [Tooltip("Brake torque added to each rear wheel by the handbrake (N·m).")]
        public float handbrakeTorque = 4000f;
        [Tooltip("Rear sideways grip multiplier while the handbrake is held.")]
        [Range(0.1f, 1f)] public float handbrakeRearGrip = 0.5f;
        [Tooltip("Brake torque applied at rest with no pedal input, so the car doesn't creep.")]
        public float holdBrakeTorque = 300f;

        [Header("Steering")]
        public float maxSteerAngle = 33f;
        [Tooltip("Steering lock at and above High Speed Steer Kmh.")]
        public float highSpeedSteerAngle = 8f;
        public float highSpeedSteerKmh = 160f;
        [Tooltip("Steering rack speed at the wheels (degrees per second).")]
        public float steerSpeed = 180f;
        [Tooltip("Turn the inner front wheel more than the outer one.")]
        public bool ackermannSteering = true;

        [Header("Suspension")]
        public float suspensionDistance = 0.2f;
        [Tooltip("Spring rate per wheel (N/m).")]
        public float springRate = 35000f;
        [Tooltip("Damper rate per wheel (N·s/m).")]
        public float damperRate = 4000f;
        [Range(0f, 1f)] public float suspensionTargetPosition = 0.5f;
        [Tooltip("Front anti-roll bar stiffness (N per unit of travel difference).")]
        public float frontAntiRoll = 7000f;
        public float rearAntiRoll = 5000f;

        [Header("Wheels and tyres")]
        public float wheelRadius = 0.34f;
        public float wheelMass = 20f;
        public TireFriction forwardFriction = new TireFriction
        {
            extremumSlip = 0.4f, extremumValue = 1f, asymptoteSlip = 0.8f, asymptoteValue = 0.5f, stiffness = 1.6f
        };
        public TireFriction sidewaysFriction = new TireFriction
        {
            extremumSlip = 0.25f, extremumValue = 1f, asymptoteSlip = 0.5f, asymptoteValue = 0.75f, stiffness = 1.5f
        };

        [Header("Simulation cost")]
        [Tooltip("WheelCollider sub-stepping speed threshold (m/s). Lower the step counts on low-end mobiles.")]
        public float substepSpeedThreshold = 5f;
        public int substepsBelowThreshold = 12;
        public int substepsAboveThreshold = 15;

        public int ForwardGearCount => forwardGearRatios.Length;

        void OnValidate()
        {
            idleRpm = Mathf.Max(300f, idleRpm);
            redlineRpm = Mathf.Max(idleRpm + 500f, redlineRpm);
            launchRpm = Mathf.Clamp(launchRpm, idleRpm, redlineRpm);
            upshiftRpm = Mathf.Clamp(upshiftRpm, idleRpm, redlineRpm);
            downshiftRpm = Mathf.Clamp(downshiftRpm, idleRpm, upshiftRpm);
            if (forwardGearRatios == null || forwardGearRatios.Length == 0)
                forwardGearRatios = new[] { 1f };
            substepsBelowThreshold = Mathf.Max(1, substepsBelowThreshold);
            substepsAboveThreshold = Mathf.Max(1, substepsAboveThreshold);
        }
    }
}
