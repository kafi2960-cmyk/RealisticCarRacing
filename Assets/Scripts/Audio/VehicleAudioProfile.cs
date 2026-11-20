using System;
using UnityEngine;

namespace RealisticCarRacing.Audio
{
    /// <summary>
    /// The recordings one car uses. Every slot is optional: a missing clip just means that sound is silent (nothing
    /// is synthesised in its place). Engine loops are recordings at a steady RPM; give each its RPM and whether it was
    /// recorded on throttle (load 1) or coasting (load 0), and <see cref="CarEngineAudio"/> blends and pitches them.
    /// </summary>
    [CreateAssetMenu(fileName = "VehicleAudio", menuName = "RealisticCarRacing/Vehicle Audio Profile")]
    public sealed class VehicleAudioProfile : ScriptableObject
    {
        [Serializable]
        public struct EngineLayer
        {
            public AudioClip clip;
            [Tooltip("Engine speed the loop was recorded at.")]
            public float rpm;
            [Tooltip("1 = recorded on throttle (on-load), 0 = coasting (off-load).")]
            [Range(0f, 1f)] public float load;
        }

        [Header("Engine")]
        public EngineLayer[] engineLayers = Array.Empty<EngineLayer>();
        [Range(0f, 1f)] public float engineVolume = 1f;
        public AudioClip startup;
        public AudioClip shutdown;
        [Tooltip("Exhaust crackles on lifting off at high RPM.")]
        public AudioClip[] exhaustPops = Array.Empty<AudioClip>();

        [Header("Transmission")]
        public AudioClip gearUp;
        public AudioClip gearDown;
        [Tooltip("Selecting drive or reverse from a standstill.")]
        public AudioClip gearEngage;

        [Header("Tyres and brakes (loops)")]
        [Tooltip("Cornering squeal and wheelspin.")]
        public AudioClip tyreSqueal;
        [Tooltip("Locked wheels under braking. Falls back to the squeal.")]
        public AudioClip tyreLockup;
        [Tooltip("Sustained sideways slide. Falls back to the squeal, pitched down.")]
        public AudioClip drift;
        [Tooltip("Pads on discs: a quiet friction layer while braking.")]
        public AudioClip brake;

        [Header("Surfaces (rolling loops)")]
        public AudioClip asphalt;
        public AudioClip kerb;
        public AudioClip grass;
        public AudioClip gravel;

        [Header("Environment")]
        public AudioClip wind;
        public AudioClip horn;
        public AudioClip scrape;
        public AudioClip[] lightImpacts = Array.Empty<AudioClip>();
        public AudioClip[] heavyImpacts = Array.Empty<AudioClip>();
    }
}
