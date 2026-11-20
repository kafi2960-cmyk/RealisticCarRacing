using RealisticCarRacing.Tracks;
using RealisticCarRacing.Vehicles;
using UnityEngine;

namespace RealisticCarRacing.Audio
{
    /// <summary>
    /// Tyre and brake sounds from what each wheel is doing (WheelCollider ground hits), not from the pedals alone:
    /// <list type="bullet">
    /// <item>squeal: sideways slip past the grip peak, or wheelspin, on asphalt and kerbs;</item>
    /// <item>lock-up: wheels sliding forwards under braking;</item>
    /// <item>drift: the rear axle sliding sideways at speed, held for a moment;</item>
    /// <item>brakes: pad friction while braking with the wheels still turning;</item>
    /// <item>rolling: one loop per surface (asphalt, kerb rumble, grass, gravel), mixed by how many wheels are on
    /// each and scaled with speed. Surfaces come from <see cref="TrackSurfaces"/> when the scene has it.</item>
    /// </list>
    /// </summary>
    public sealed class TireAudio : MonoBehaviour
    {
        const float SquealFromSlip = 0.18f, SquealFullSlip = 0.45f;
        const float LockFromSlip = 0.35f, LockFullSlip = 0.9f;
        const float DriftFromSlip = 0.25f, DriftFullSlip = 0.6f, DriftMinKmh = 30f;
        const float DriftHoldTime = 0.35f;

        VehicleAudioManager manager;
        VehicleController vehicle;
        VehicleAudioProfile profile;
        TrackSurfaces surfaces;
        AudioSource squeal, lockup, drift, brake;
        readonly AudioSource[] rolling = new AudioSource[4];
        readonly float[] surfaceShare = new float[4];
        int[] sampleHints;
        float driftTime;

        public float Squeal { get; private set; }
        public float Lockup { get; private set; }
        public float Drift { get; private set; }
        public float Brake { get; private set; }
        public int GroundedWheels { get; private set; }
        public float SurfaceShare(TrackSurface surface) => surfaceShare[(int)surface];

        public void Initialise(VehicleAudioManager owner)
        {
            manager = owner;
            vehicle = owner.Vehicle;
            profile = owner.Profile;
            surfaces = FindAnyObjectByType<TrackSurfaces>();
            sampleHints = new int[vehicle.Wheels.Count];
            for (int i = 0; i < sampleHints.Length; i++)
                sampleHints[i] = -1;

            var low = new Vector3(0f, 0.2f, 0f);
            squeal = manager.CreateLoop("Tyre squeal", AudioCategory.Tires, profile.tyreSqueal, low);
            lockup = manager.CreateLoop("Tyre lock-up", AudioCategory.Tires, profile.tyreLockup != null ? profile.tyreLockup : profile.tyreSqueal, low);
            drift = manager.CreateLoop("Drift", AudioCategory.Tires, profile.drift != null ? profile.drift : profile.tyreSqueal, new Vector3(0f, 0.2f, -1.3f));
            if (manager.IsPlayer)
            {
                brake = manager.CreateLoop("Brakes", AudioCategory.Tires, profile.brake, low);
                rolling[(int)TrackSurface.Asphalt] = manager.CreateLoop("Road", AudioCategory.Tires, profile.asphalt, low);
                rolling[(int)TrackSurface.Kerb] = manager.CreateLoop("Kerb", AudioCategory.Tires, profile.kerb, low);
                rolling[(int)TrackSurface.Grass] = manager.CreateLoop("Grass", AudioCategory.Tires, profile.grass, low);
                rolling[(int)TrackSurface.Gravel] = manager.CreateLoop("Gravel", AudioCategory.Tires, profile.gravel, low);
            }
        }

        void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f)
                return;
            float speedKmh = vehicle.SpeedKmh;
            float moving = Mathf.InverseLerp(5f, 25f, speedKmh);
            float squealTarget = 0f, lockTarget = 0f, rearSlide = 0f;
            int grounded = 0, rearGrounded = 0;
            for (int s = 0; s < surfaceShare.Length; s++)
                surfaceShare[s] = 0f;

            var wheels = vehicle.Wheels;
            for (int i = 0; i < wheels.Count; i++)
            {
                var wheel = wheels[i];
                if (!wheel.wheelCollider.GetGroundHit(out var hit))
                    continue;
                grounded++;
                var surface = surfaces != null ? surfaces.SurfaceAt(wheel.wheelCollider.transform.position, ref sampleHints[i]) : TrackSurface.Asphalt;
                surfaceShare[(int)surface] += 1f;
                bool hard = surface == TrackSurface.Asphalt || surface == TrackSurface.Kerb; // loose ground doesn't squeal
                float sideways = Mathf.Abs(hit.sidewaysSlip), forwards = Mathf.Abs(hit.forwardSlip);
                if (hard)
                {
                    squealTarget = Mathf.Max(squealTarget, Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(SquealFromSlip, SquealFullSlip, sideways)));
                    if (vehicle.BrakeInput > 0.2f)
                        lockTarget = Mathf.Max(lockTarget, Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(LockFromSlip, LockFullSlip, forwards)));
                    else // wheelspin
                        squealTarget = Mathf.Max(squealTarget, 0.8f * Mathf.InverseLerp(LockFromSlip, LockFullSlip, forwards));
                }
                if (!wheel.IsFront)
                {
                    rearGrounded++;
                    rearSlide += Mathf.InverseLerp(DriftFromSlip, DriftFullSlip, sideways);
                }
            }
            GroundedWheels = grounded;
            if (grounded > 0)
            {
                for (int s = 0; s < surfaceShare.Length; s++)
                    surfaceShare[s] /= grounded;
            }

            // A drift is a sustained rear slide at speed, not a twitch.
            rearSlide = rearGrounded > 0 ? rearSlide / rearGrounded : 0f;
            bool sliding = rearSlide > 0.3f && speedKmh > DriftMinKmh;
            driftTime = sliding ? driftTime + dt : 0f;
            float driftTarget = driftTime > DriftHoldTime ? Mathf.Clamp01(rearSlide) : 0f;

            float squealSpeed = Mathf.InverseLerp(8f, 40f, speedKmh);
            Squeal = Smooth(Squeal, squealTarget * squealSpeed * (1f - driftTarget * 0.6f), dt);
            Lockup = Smooth(Lockup, lockTarget * moving, dt);
            Drift = Smooth(Drift, driftTarget, dt);
            Brake = Smooth(Brake, vehicle.BrakeInput * Mathf.InverseLerp(3f, 60f, speedKmh) * (1f - Lockup), dt);

            Set(squeal, Squeal * 0.8f, 0.9f + Squeal * 0.2f);
            Set(lockup, Lockup * 0.9f, 1.05f);
            Set(drift, Drift * 0.85f, profile.drift != null ? 1f : 0.82f + speedKmh / 900f);
            Set(brake, Brake * 0.35f, 0.9f + speedKmh / 400f);
            float roll = Mathf.Pow(Mathf.InverseLerp(0f, 160f, speedKmh), 0.8f);
            for (int s = 0; s < rolling.Length; s++)
            {
                var source = rolling[s];
                if (source == null)
                    continue;
                float level = surfaceShare[s] * roll * (s == (int)TrackSurface.Kerb ? 0.9f : s == (int)TrackSurface.Asphalt ? 0.45f : 0.7f);
                source.volume = VehicleAudioManager.Ease(source.volume, level, 3f, 2f, dt);
                source.pitch = 0.8f + speedKmh / 300f;
            }
        }

        static float Smooth(float current, float target, float dt) => VehicleAudioManager.Ease(current, target, 8f, 3f, dt);

        static void Set(AudioSource source, float volume, float pitch)
        {
            if (source == null)
                return;
            source.volume = volume;
            source.pitch = pitch;
        }
    }
}
