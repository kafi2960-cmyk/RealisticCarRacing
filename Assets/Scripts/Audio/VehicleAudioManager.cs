using System.Collections.Generic;
using RealisticCarRacing.Cameras;
using RealisticCarRacing.Vehicles;
using UnityEngine;

namespace RealisticCarRacing.Audio
{
    /// <summary>
    /// A car's sound: creates every AudioSource it needs once, when the car is spawned (none during play), routes
    /// each to its mixer group, and runs the engine, gearbox, tyre and environment components. The player's car gets
    /// everything; AI cars get engine, gearbox and tyres only, and go quiet (sources paused, voices freed) beyond
    /// <see cref="aiCullDistance"/>. All sound is 3D: distance, stereo position and (for passing AI cars) Doppler
    /// come from the listener on the camera.
    /// </summary>
    public sealed class VehicleAudioManager : MonoBehaviour
    {
        [SerializeField] float aiCullDistance = 140f;

        readonly List<AudioSource> sources = new List<AudioSource>();
        readonly List<AudioSource> loops = new List<AudioSource>();
        AudioListener listener;
        bool audible = true;

        public VehicleController Vehicle { get; private set; }
        public VehicleAudioProfile Profile { get; private set; }
        public bool IsPlayer { get; private set; }
        /// <summary>False while an AI car is too far from the listener to be heard (its sources are paused).</summary>
        public bool Audible => audible;
        /// <summary>The camera is inside this car (cockpit view): engine muffled, wind quieter.</summary>
        public bool Interior { get; set; }
        public int SourceCount => sources.Count;
        /// <summary>Loops currently using a voice (silent loops are paused).</summary>
        public int PlayingLoops { get; private set; }

        public CarEngineAudio Engine { get; private set; }
        public GearShiftAudio Gearbox { get; private set; }
        public TireAudio Tires { get; private set; }
        public EnvironmentAudio Environment { get; private set; }

        /// <summary>Adds sound to a spawned car. Returns null when the project has no audio set up.</summary>
        public static VehicleAudioManager Attach(VehicleController vehicle, bool isPlayer, VehicleAudioProfile profile = null)
        {
            profile ??= GameAudio.Instance != null ? GameAudio.Instance.defaultVehicle : null;
            if (profile == null)
                return null;
            var manager = vehicle.gameObject.AddComponent<VehicleAudioManager>();
            manager.Initialise(vehicle, profile, isPlayer);
            return manager;
        }

        void Initialise(VehicleController vehicle, VehicleAudioProfile profile, bool isPlayer)
        {
            Vehicle = vehicle;
            Profile = profile;
            IsPlayer = isPlayer;
            Engine = gameObject.AddComponent<CarEngineAudio>();
            Engine.Initialise(this);
            Gearbox = gameObject.AddComponent<GearShiftAudio>();
            Gearbox.Initialise(this);
            Tires = gameObject.AddComponent<TireAudio>();
            Tires.Initialise(this);
            if (isPlayer)
            {
                Environment = gameObject.AddComponent<EnvironmentAudio>();
                Environment.Initialise(this);
            }
        }

        /// <summary>A looping source (started silent). Returns null for a missing clip, so callers just skip it.</summary>
        public AudioSource CreateLoop(string name, AudioCategory category, AudioClip clip, Vector3 localPosition)
        {
            if (clip == null)
                return null;
            var source = CreateSource(name, category, localPosition);
            source.clip = clip;
            source.loop = true;
            source.volume = 0f;
            // Start each loop at a random point so cars sharing recordings don't phase against each other.
            source.time = Random.Range(0f, clip.length);
            source.Play();
            source.Pause(); // LateUpdate resumes it once it has a level
            loops.Add(source);
            return source;
        }

        /// <summary>A source for one-shot sounds of one category (PlayOneShot layers them without new sources).</summary>
        public AudioSource CreateOneShot(string name, AudioCategory category, Vector3 localPosition)
        {
            var source = CreateSource(name, category, localPosition);
            source.loop = false;
            return source;
        }

        AudioSource CreateSource(string name, AudioCategory category, Vector3 localPosition)
        {
            var go = new GameObject("Audio " + name);
            go.transform.SetParent(transform, false);
            go.transform.localPosition = localPosition;
            var source = go.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 1f;
            source.rolloffMode = AudioRolloffMode.Logarithmic;
            source.minDistance = IsPlayer ? 6f : 4f;
            source.maxDistance = 250f;
            source.dopplerLevel = IsPlayer ? 0f : 0.6f; // the camera follows the player's car: no self-Doppler
            source.priority = IsPlayer ? 32 : 160;      // lower is more important when voices run out
            var group = GameAudio.Instance != null ? GameAudio.Instance.Group(category) : null;
            source.outputAudioMixerGroup = group;
            sources.Add(source);
            return source;
        }

        VehicleCameraRig rig;

        void Start()
        {
            if (!IsPlayer)
                return;
            // The cockpit camera puts the listener inside the car.
            rig = FindAnyObjectByType<VehicleCameraRig>();
            if (rig != null)
            {
                rig.ViewChanged += OnViewChanged;
                OnViewChanged(rig.View);
            }
        }

        void OnDestroy()
        {
            if (rig != null)
                rig.ViewChanged -= OnViewChanged;
        }

        void OnViewChanged(CameraView view) => Interior = view == CameraView.Cockpit;

        void Update()
        {
            if (IsPlayer)
                return;
            if (listener == null || !listener.isActiveAndEnabled)
                listener = FindAnyObjectByType<AudioListener>();
            if (listener != null)
                audible = (listener.transform.position - transform.position).sqrMagnitude < aiCullDistance * aiCullDistance;
        }

        /// <summary>
        /// After the components have set their levels: a silent loop (or any loop of a car too far away to hear) is
        /// paused, so it frees its voice; it resumes where it left off when it is needed again.
        /// </summary>
        void LateUpdate()
        {
            int playing = 0;
            foreach (var loop in loops)
            {
                bool wanted = audible && loop.volume > 0.0005f;
                if (wanted && !loop.isPlaying)
                    loop.UnPause();
                else if (!wanted && loop.isPlaying)
                    loop.Pause();
                if (wanted)
                    playing++;
            }
            PlayingLoops = playing;
        }

        /// <summary>Moves a value towards a target at separate rise and fall rates (per second): no zipper noise.</summary>
        public static float Ease(float current, float target, float rise, float fall, float deltaTime) =>
            Mathf.MoveTowards(current, target, (target > current ? rise : fall) * deltaTime);
    }
}
