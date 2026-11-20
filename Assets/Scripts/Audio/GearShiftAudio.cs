using RealisticCarRacing.Vehicles;
using UnityEngine;

namespace RealisticCarRacing.Audio
{
    public enum ShiftSound
    {
        None,
        Up,
        Down,
        Engage
    }

    /// <summary>
    /// Gearbox sounds, played only when the transmission's gear actually changes (it is read every frame, but a
    /// sound fires once per change). Selecting drive or reverse from neutral, or swapping between them, is an
    /// engagement; otherwise an up- or downshift. A manual (sequential) shift is a firm clunk; the automatic's shifts
    /// are softer, as its torque cut already shapes the engine note.
    /// </summary>
    public sealed class GearShiftAudio : MonoBehaviour
    {
        const float AutomaticLevel = 0.55f;

        VehicleAudioManager manager;
        VehicleController vehicle;
        VehicleAudioProfile profile;
        AudioSource source;
        int lastGear;
        bool primed;

        public int ShiftsPlayed { get; private set; }
        public ShiftSound LastSound { get; private set; }

        public void Initialise(VehicleAudioManager owner)
        {
            manager = owner;
            vehicle = owner.Vehicle;
            profile = owner.Profile;
            source = manager.CreateOneShot("Gearbox", AudioCategory.Transmission, new Vector3(0f, 0.4f, 0.3f));
        }

        public static ShiftSound Classify(int from, int to)
        {
            if (from == to)
                return ShiftSound.None;
            bool engage = from == VehicleTransmission.Neutral || from == VehicleTransmission.Reverse || to == VehicleTransmission.Reverse;
            if (to == VehicleTransmission.Neutral)
                return ShiftSound.Down;
            return engage ? ShiftSound.Engage : to > from ? ShiftSound.Up : ShiftSound.Down;
        }

        void Update()
        {
            int gear = vehicle.Gear;
            if (!primed)
            {
                lastGear = gear;
                primed = true;
                return;
            }
            if (gear == lastGear)
                return;
            var sound = Classify(lastGear, gear);
            lastGear = gear;
            LastSound = sound;
            ShiftsPlayed++;
            var clip = sound switch
            {
                ShiftSound.Up => profile.gearUp,
                ShiftSound.Down => profile.gearDown != null ? profile.gearDown : profile.gearUp,
                _ => profile.gearEngage != null ? profile.gearEngage : profile.gearDown
            };
            if (clip == null || !manager.Audible)
                return;
            float level = vehicle.TransmissionMode == TransmissionMode.Manual ? 1f : AutomaticLevel;
            source.pitch = Random.Range(0.96f, 1.04f);
            source.PlayOneShot(clip, level);
        }
    }
}
