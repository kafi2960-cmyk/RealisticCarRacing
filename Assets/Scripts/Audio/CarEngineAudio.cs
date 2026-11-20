using System.Collections.Generic;
using RealisticCarRacing.Vehicles;
using UnityEngine;

namespace RealisticCarRacing.Audio
{
    /// <summary>
    /// Engine and exhaust sound from the simulated engine. Steady-RPM recordings are cross-faded (equal power) by the
    /// engine's actual RPM and pitched to match it, so the note rises continuously instead of switching samples.
    /// On-throttle and coasting recordings are blended by engine load (the torque the engine is making against what
    /// it could make at this RPM), so accelerating sounds harder than lifting off at the same revs. Lifting off from
    /// high RPM fires a few exhaust crackles. Startup and shutdown play their recordings and fade the loops in or out.
    /// Levels are computed directly from the eased inputs (engine RPM, smoothed load, start/stop fade), so the blend
    /// is equal-power at every frame and nothing jumps.
    /// </summary>
    public sealed class CarEngineAudio : MonoBehaviour
    {
        const float LoadTime = 0.12f;                    // smoothing of the load estimate (s)
        const float RpmTime = 0.04f;                     // smoothing of RPM for the sound: softens shift edges
        const float InteriorCutoff = 2800f;              // Hz: the cockpit muffles the engine bay
        const float StartFade = 0.6f, StopFade = 0.5f;   // s

        VehicleAudioManager manager;
        VehicleController vehicle;
        VehicleAudioProfile profile;
        readonly List<(AudioSource source, VehicleAudioProfile.EngineLayer layer)> layers = new List<(AudioSource, VehicleAudioProfile.EngineLayer)>();
        readonly List<AudioLowPassFilter> interiorFilters = new List<AudioLowPassFilter>();
        float[] weights;
        AudioSource oneShots, exhaust;
        float master, masterTarget, startTimer = -1f;
        float smoothedLoad, loadVelocity, rpmVelocity, previousThrottle, popCooldown;
        int popsQueued;
        float nextPopTime, startupHold;

        public bool Running { get; private set; }
        public float Rpm { get; private set; }
        public float Load => smoothedLoad;
        public int LayerCount => layers.Count;
        public float LayerVolume(int index) => layers[index].source.volume;
        public float LayerPitch(int index) => layers[index].source.pitch;
        public int PopsPlayed { get; private set; }

        /// <summary>Overall engine level 0..1 (sum of layer volumes before the profile's master volume).</summary>
        public float Loudness { get; private set; }

        public void Initialise(VehicleAudioManager owner)
        {
            manager = owner;
            vehicle = owner.Vehicle;
            profile = owner.Profile;
            // Engine bay at the front; exhaust at the rear.
            foreach (var layer in profile.engineLayers)
            {
                var source = manager.CreateLoop($"Engine {layer.rpm:0} rpm", AudioCategory.Engine, layer.clip, new Vector3(0f, 0.6f, 1.4f));
                if (source == null)
                    continue;
                layers.Add((source, layer));
                if (manager.IsPlayer)
                {
                    var filter = source.gameObject.AddComponent<AudioLowPassFilter>();
                    filter.cutoffFrequency = 22000f;
                    interiorFilters.Add(filter);
                }
            }
            weights = new float[layers.Count];
            oneShots = manager.CreateOneShot("Engine one-shots", AudioCategory.Engine, new Vector3(0f, 0.6f, 1.4f));
            if (profile.exhaustPops.Length > 0)
                exhaust = manager.CreateOneShot("Exhaust", AudioCategory.Engine, new Vector3(0.4f, 0.3f, -2.2f));
        }

        /// <summary>Starts the engine: the startup recording, then the running sound fades in.</summary>
        public void StartEngine(float delay = 0f)
        {
            if (Running)
                return;
            Running = true;
            startTimer = delay;
            masterTarget = 0f;
        }

        /// <summary>Stops the engine: the running sound fades out under the shutdown recording.</summary>
        public void StopEngine()
        {
            if (!Running)
                return;
            Running = false;
            startTimer = -1f;
            masterTarget = 0f;
            if (profile.shutdown != null)
                oneShots.PlayOneShot(profile.shutdown, profile.engineVolume);
        }

        void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f)
                return;
            if (startTimer >= 0f)
            {
                startTimer -= dt;
                if (startTimer < 0f)
                {
                    if (profile.startup != null)
                        oneShots.PlayOneShot(profile.startup, profile.engineVolume);
                    masterTarget = 1f;
                    master = 0f;
                    // Hold the loops back until the startup recording has settled to its idle.
                    startupHold = profile.startup != null ? profile.startup.length * 0.5f : 0f;
                }
            }
            if (startupHold > 0f)
            {
                startupHold -= dt;
                masterTarget = startupHold > 0f ? 0f : 1f;
            }
            master = Mathf.MoveTowards(master, Running ? masterTarget : 0f, dt / (Running ? StartFade : StopFade));

            UpdateLoad(dt);
            Rpm = Rpm <= 0f ? vehicle.EngineRpm : Mathf.SmoothDamp(Rpm, vehicle.EngineRpm, ref rpmVelocity, RpmTime, Mathf.Infinity, dt);
            UpdateLayers(dt);
            UpdatePops(dt);

            if (interiorFilters.Count > 0)
            {
                float cutoff = manager.Interior ? InteriorCutoff : 22000f;
                foreach (var filter in interiorFilters)
                    filter.cutoffFrequency = Mathf.Lerp(filter.cutoffFrequency, cutoff, 1f - Mathf.Exp(-10f * dt));
            }
        }

        void UpdateLoad(float dt)
        {
            // Load: torque made / torque available at these revs. With the clutch open (neutral, mid-shift) the
            // engine makes no torque, so the pedal stands in for it.
            float available = Mathf.Max(1f, vehicle.Settings.torqueCurve.Evaluate(vehicle.EngineRpm));
            float target = vehicle.EngineTorque > 0f ? vehicle.EngineTorque / available
                : vehicle.IsShifting || vehicle.Gear == VehicleTransmission.Neutral ? vehicle.AppliedThrottle * 0.7f : 0f;
            smoothedLoad = Mathf.SmoothDamp(smoothedLoad, Mathf.Clamp01(target), ref loadVelocity, LoadTime, Mathf.Infinity, dt);
        }

        void UpdateLayers(float dt)
        {
            if (layers.Count == 0)
            {
                Loudness = 0f;
                return;
            }
            bool hasOn = false, hasOff = false;
            foreach (var (_, layer) in layers)
            {
                if (layer.load >= 0.5f) hasOn = true;
                else hasOff = true;
            }
            // With only one set of recordings, it plays for both: coasting is then quieter rather than different.
            float onWeight = hasOn && hasOff ? Mathf.Sin(smoothedLoad * Mathf.PI * 0.5f) : 1f;
            float offWeight = hasOn && hasOff ? Mathf.Cos(smoothedLoad * Mathf.PI * 0.5f) : 0f;
            float loadGain = hasOn && hasOff ? 1f : Mathf.Lerp(0.55f, 1f, smoothedLoad);

            for (int i = 0; i < weights.Length; i++)
                weights[i] = 0f;
            CrossFade(true, hasOn && hasOff ? onWeight : 1f, !(hasOn && hasOff));
            if (hasOn && hasOff)
                CrossFade(false, offWeight, false);

            float loudness = 0f;
            for (int i = 0; i < layers.Count; i++)
            {
                var (source, layer) = layers[i];
                float target = weights[i] * loadGain * master;
                loudness += target;
                source.volume = target * profile.engineVolume;
                // Each recording is pitched by how far the engine is from the RPM it was recorded at.
                source.pitch = Mathf.Clamp(Mathf.Max(Rpm, vehicle.Settings.idleRpm * 0.9f) / Mathf.Max(100f, layer.rpm), 0.35f, 2.5f);
            }
            Loudness = loudness;
        }

        /// <summary>Equal-power cross-fade between the two recordings either side of the current RPM.</summary>
        void CrossFade(bool onLoad, float groupWeight, bool allLayers)
        {
            int below = -1, above = -1;
            for (int i = 0; i < layers.Count; i++)
            {
                var layer = layers[i].layer;
                if (!allLayers && (layer.load >= 0.5f) != onLoad)
                    continue;
                if (layer.rpm <= Rpm && (below < 0 || layer.rpm > layers[below].layer.rpm))
                    below = i;
                if (layer.rpm > Rpm && (above < 0 || layer.rpm < layers[above].layer.rpm))
                    above = i;
            }
            if (below < 0 && above < 0)
                return;
            if (below < 0) { weights[above] += groupWeight; return; }
            if (above < 0) { weights[below] += groupWeight; return; }
            float t = Mathf.InverseLerp(layers[below].layer.rpm, layers[above].layer.rpm, Rpm);
            weights[below] += groupWeight * Mathf.Cos(t * Mathf.PI * 0.5f);
            weights[above] += groupWeight * Mathf.Sin(t * Mathf.PI * 0.5f);
        }

        /// <summary>Lift-off at high revs: a short burst of exhaust crackles, at most once per lift.</summary>
        void UpdatePops(float dt)
        {
            if (exhaust == null || !Running)
                return;
            popCooldown -= dt;
            float throttle = vehicle.AppliedThrottle;
            bool lifted = previousThrottle > 0.7f && throttle < 0.15f;
            previousThrottle = throttle;
            if (lifted && popCooldown <= 0f && Rpm > vehicle.Settings.redlineRpm * 0.6f && manager.Audible)
            {
                popsQueued = Random.Range(2, 5);
                popCooldown = 1.5f;
                nextPopTime = 0f;
            }
            if (popsQueued <= 0)
                return;
            nextPopTime -= dt;
            if (nextPopTime > 0f)
                return;
            var clip = profile.exhaustPops[Random.Range(0, profile.exhaustPops.Length)];
            exhaust.pitch = Random.Range(0.9f, 1.1f);
            exhaust.PlayOneShot(clip, Random.Range(0.5f, 0.9f) * profile.engineVolume);
            PopsPlayed++;
            popsQueued--;
            nextPopTime = Random.Range(0.06f, 0.18f);
        }

    }
}
