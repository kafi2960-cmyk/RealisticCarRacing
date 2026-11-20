using RealisticCarRacing.Controls;
using RealisticCarRacing.Tracks;
using RealisticCarRacing.Vehicles;
using UnityEngine;
using UnityEngine.InputSystem;

namespace RealisticCarRacing.AI
{
    /// <summary>
    /// Development helper: press the toggle key (default P) to let a <see cref="TrackFollowerDriver"/>
    /// drive the car around the track, and again to take back control.
    /// </summary>
    [RequireComponent(typeof(VehicleController))]
    public sealed class AutopilotDriver : MonoBehaviour
    {
        [SerializeField] RaceTrack track;
        [SerializeField] Key toggleKey = Key.P;
        [SerializeField] bool engageOnStart;

        VehicleController vehicle;
        IVehicleInputSource playerInput;
        TrackFollowerDriver driver;

        public bool IsEngaged { get; private set; }

        public void SetTrack(RaceTrack raceTrack) => track = raceTrack;

        void Start()
        {
            vehicle = GetComponent<VehicleController>();
            playerInput = GetComponent<IVehicleInputSource>();
            if (track == null)
                track = FindAnyObjectByType<RaceTrack>();
            if (engageOnStart)
                SetEngaged(true);
        }

        void Update()
        {
            var keyboard = Keyboard.current;
            if (keyboard != null && keyboard[toggleKey].wasPressedThisFrame)
                SetEngaged(!IsEngaged);
        }

        public void SetEngaged(bool engaged)
        {
            if (track == null)
                return;
            IsEngaged = engaged;
            driver ??= new TrackFollowerDriver(vehicle, track);
            vehicle.SetInputSource(engaged ? driver : playerInput);
        }
    }
}
