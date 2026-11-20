using RealisticCarRacing.Cameras;
using RealisticCarRacing.Vehicles;
using UnityEngine;

namespace RealisticCarRacing.UI
{
    /// <summary>
    /// Development read-out of speed, gear, RPM and driver input, plus a controls cheat sheet.
    /// Uses IMGUI so it needs no UI assets; replace with the real race HUD later.
    /// </summary>
    public sealed class VehicleDebugHud : MonoBehaviour
    {
        const string ControlsHelp =
            "Keyboard: W/S or ↑/↓ throttle/brake (hold S when stopped to reverse) · A/D or ←/→ steer · Space handbrake\n" +
            "                 C camera · −/= or mouse wheel camera distance · E/Q shift (manual) · T auto/manual · R reset · P autopilot (circuit)\n" +
            "Gamepad: RT/LT throttle/brake · left stick steer · A/Cross handbrake · R3 camera · D-pad ↑/↓ distance · right stick look around\n" +
            "                 RB/LB shift · Y/Triangle auto/manual · View/Select reset";

        [SerializeField] VehicleController vehicle;
        [SerializeField] VehicleCameraRig cameraRig;
        [SerializeField] bool showControlsHelp = true;

        GUIStyle style;

        public void SetVehicle(VehicleController target) => vehicle = target;

        public void SetCameraRig(VehicleCameraRig rig) => cameraRig = rig;

        void OnGUI()
        {
            if (vehicle == null || !vehicle.isActiveAndEnabled)
                return;

            float scale = Screen.height / 1080f;
            style ??= new GUIStyle(GUI.skin.label) { richText = true };
            style.fontSize = Mathf.RoundToInt(26 * scale);

            var input = vehicle.CurrentInput;
            string shifting = vehicle.IsShifting ? " <color=yellow>shifting</color>" : "";
            string handbrake = input.Handbrake ? "  <color=red>HANDBRAKE</color>" : "";
            string text =
                $"<b>{vehicle.SpeedKmh:0} km/h</b>\n" +
                $"Gear <b>{vehicle.GearLabel}</b>{shifting}   ({vehicle.TransmissionMode})\n" +
                $"{vehicle.EngineRpm:0} rpm\n" +
                $"Throttle {input.Throttle:0.00}  Brake {input.Brake:0.00}  Steer {input.Steer:+0.00;-0.00;0.00}{handbrake}";
            if (cameraRig != null)
                text += cameraRig.View == CameraView.Chase
                    ? $"\nCamera: Chase ({cameraRig.ChaseDistance:0.0} m)"
                    : $"\nCamera: {cameraRig.View}";

            float margin = 20f * scale;
            GUI.Label(new Rect(margin, margin, 900f * scale, 240f * scale), text, style);

            if (showControlsHelp)
            {
                style.fontSize = Mathf.RoundToInt(18 * scale);
                GUI.Label(new Rect(margin, 230f * scale, Screen.width - 2f * margin, 160f * scale), ControlsHelp, style);
            }
        }
    }
}
