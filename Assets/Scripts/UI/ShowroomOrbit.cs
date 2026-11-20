using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.Rendering.Universal;

namespace RealisticCarRacing.UI
{
    /// <summary>Preset camera angles of the showroom car.</summary>
    public enum ShowroomView
    {
        ThreeQuarter,
        Front,
        Side,
        Rear
    }

    /// <summary>
    /// Turning and framing the showroom car. Dragging (mouse or one finger) spins the turntable and tilts the camera,
    /// with a little inertia; the mouse wheel, a pinch, the gamepad triggers or +/- zoom; the right stick and Q/E also
    /// turn it. Preset views ease the turntable round to the front, side, rear or three-quarter angle. The camera keeps
    /// the car centred in the part of the screen the menu panel leaves free, at any aspect ratio, and the anti-aliasing
    /// follows the graphics preset (FXAA on Low, MSAA from the preset otherwise).
    /// </summary>
    public sealed class ShowroomOrbit : MonoBehaviour
    {
        [SerializeField] Transform turntable;
        [SerializeField] Camera viewCamera;
        [Tooltip("Point the camera orbits, in world space (roughly the middle of the car).")]
        [SerializeField] Vector3 pivot = new Vector3(0f, 0.62f, 0f);
        [Tooltip("Width (m) that fits the free part of the screen at zoom 1: the car's length plus a margin.")]
        [SerializeField] float frameWidth = 5.8f;
        [SerializeField] float minZoom = 0.5f, maxZoom = 1.45f;
        [SerializeField] float minPitch = 2f, maxPitch = 38f;
        [Tooltip("Closest the camera may come to the pivot (keeps it outside the car).")]
        [SerializeField] float minDistance = 3.1f;
        [Tooltip("Turntable degrees per 1000 pixels of horizontal drag (pixels are relative to a 1080p screen).")]
        [SerializeField] float dragSensitivity = 300f;
        [SerializeField] float autoSpinSpeed = 9f;
        [SerializeField] float autoSpinDelay = 2.5f;

        readonly List<RaycastResult> uiHits = new List<RaycastResult>();
        float yaw = 140f, yawVelocity, viewVelocity, pitchVelocity, zoomVelocity;
        float pitch = 10f, targetPitch = 10f, zoom = 1f, targetZoom = 1f;
        float? targetYaw;
        float idleTime;
        bool dragging;
        int dragTouchId = -1;
        Vector2 lastPointer;
        float lastPinchDistance;
        RectTransform panel;

        /// <summary>Spin slowly on its own after a few seconds without input (off in the garage, on elsewhere).</summary>
        public bool AutoSpin { get; set; } = true;
        /// <summary>Turntable angle in degrees (0 = car facing away from the camera).</summary>
        public float Yaw => yaw;
        public float Pitch => pitch;
        public float Zoom => zoom;
        public float Distance { get; private set; }
        /// <summary>The preset view being shown, or null once the user has turned the car by hand.</summary>
        public ShowroomView? CurrentView { get; private set; } = ShowroomView.ThreeQuarter;
        public Camera Camera => viewCamera;

        public void Configure(Transform turntableTransform, Camera camera)
        {
            turntable = turntableTransform;
            viewCamera = camera;
        }

        /// <summary>The menu panel on the left: the car is framed in the screen area to its right.</summary>
        public void SetPanel(RectTransform leftPanel) => panel = leftPanel;

        public static float ViewYaw(ShowroomView view) => view switch
        {
            ShowroomView.Front => 180f,
            ShowroomView.Side => 90f,
            ShowroomView.Rear => 0f,
            _ => 140f
        };

        static float ViewPitch(ShowroomView view) => view switch
        {
            ShowroomView.Front => 5f,
            ShowroomView.Side => 3f,
            ShowroomView.Rear => 8f,
            _ => 10f
        };

        public void SetView(ShowroomView view)
        {
            targetYaw = ViewYaw(view);
            targetPitch = ViewPitch(view);
            targetZoom = 1f;
            yawVelocity = 0f;
            CurrentView = view;
            idleTime = 0f;
        }

        /// <summary>Turns the turntable by hand (cancels any preset view).</summary>
        public void Rotate(float degrees)
        {
            targetYaw = null;
            CurrentView = null;
            yaw += degrees;
            idleTime = 0f;
        }

        public void Tilt(float degrees)
        {
            targetPitch = Mathf.Clamp(targetPitch + degrees, minPitch, maxPitch);
            idleTime = 0f;
        }

        /// <summary>Multiplies the viewing distance (below 1 zooms in).</summary>
        public void ZoomBy(float factor)
        {
            targetZoom = Mathf.Clamp(targetZoom * factor, minZoom, maxZoom);
            idleTime = 0f;
        }

        /// <summary>Jumps straight to the targets (no easing), e.g. for screenshots.</summary>
        public void Settle()
        {
            if (targetYaw.HasValue)
                yaw = targetYaw.Value;
            pitch = targetPitch;
            zoom = targetZoom;
            yawVelocity = viewVelocity = pitchVelocity = zoomVelocity = 0f;
            Apply();
        }

        void OnEnable()
        {
            QualitySettings.activeQualityLevelChanged += OnQualityChanged;
            ApplyAntialiasing();
        }

        void OnDisable() => QualitySettings.activeQualityLevelChanged -= OnQualityChanged;

        void OnQualityChanged(int previous, int current) => ApplyAntialiasing();

        /// <summary>FXAA where the preset has no MSAA (Low): the cheapest edge smoothing for phones.</summary>
        void ApplyAntialiasing()
        {
            if (viewCamera == null)
                return;
            var data = viewCamera.GetUniversalAdditionalCameraData();
            var asset = UniversalRenderPipeline.asset;
            bool hasMsaa = asset != null && asset.msaaSampleCount > 1;
            data.antialiasing = hasMsaa ? AntialiasingMode.None : AntialiasingMode.FastApproximateAntialiasing;
        }

        void Update()
        {
            float dt = Time.unscaledDeltaTime;
            idleTime += dt;
            ReadPointer(dt);
            ReadButtons(dt);

            if (targetYaw.HasValue)
            {
                yaw = Mathf.SmoothDampAngle(yaw, targetYaw.Value, ref viewVelocity, 0.35f, Mathf.Infinity, dt);
            }
            else if (!dragging)
            {
                // After a fling the turntable coasts to a stop; when idle long enough it starts a slow spin.
                float spin = AutoSpin && idleTime > autoSpinDelay ? autoSpinSpeed : 0f;
                yawVelocity = Mathf.Lerp(yawVelocity, spin, 1f - Mathf.Exp(-(spin > 0f ? 0.8f : 3.5f) * dt));
                if (spin == 0f && Mathf.Abs(yawVelocity) < 0.5f)
                    yawVelocity = 0f; // come to rest instead of creeping
                yaw += yawVelocity * dt;
            }
            yaw = Mathf.Repeat(yaw, 360f);
            pitch = Mathf.SmoothDamp(pitch, targetPitch, ref pitchVelocity, 0.25f, Mathf.Infinity, dt);
            zoom = Mathf.SmoothDamp(zoom, targetZoom, ref zoomVelocity, 0.2f, Mathf.Infinity, dt);
            Apply();
        }

        void Apply()
        {
            if (turntable != null)
                turntable.rotation = Quaternion.Euler(0f, yaw, 0f);
            if (viewCamera == null)
                return;

            // Free screen area right of the panel; the pivot sits in its middle.
            float panelFraction = 0f;
            if (panel != null && panel.gameObject.activeInHierarchy && panel.parent is RectTransform canvas && canvas.rect.width > 0f)
                panelFraction = Mathf.Clamp(panel.rect.width / canvas.rect.width, 0f, 0.7f);
            float tanV = Mathf.Tan(viewCamera.fieldOfView * 0.5f * Mathf.Deg2Rad);
            float tanH = tanV * viewCamera.aspect;
            float fitDistance = Mathf.Max(frameWidth / (2f * tanH * (1f - panelFraction)), frameWidth * 0.62f / (2f * tanV));
            Distance = Mathf.Max(minDistance, fitDistance * zoom);

            var rotation = Quaternion.Euler(pitch, 0f, 0f);
            float centre = (panelFraction + 1f) * 0.5f;
            float shift = (2f * centre - 1f) * tanH * Distance;
            viewCamera.transform.SetPositionAndRotation(pivot - rotation * Vector3.forward * Distance - rotation * Vector3.right * shift, rotation);
        }

        // ---------- Input ----------

        float PixelScale => 1080f / Mathf.Max(1, Screen.height);

        void Drag(Vector2 delta, float dt)
        {
            float degrees = delta.x * PixelScale * dragSensitivity / 1000f;
            Rotate(-degrees);
            Tilt(-delta.y * PixelScale * dragSensitivity * 0.4f / 1000f);
            if (dt > 0f)
                yawVelocity = Mathf.Lerp(yawVelocity, -degrees / dt, 0.5f); // released mid-drag: keep spinning a little
        }

        void ReadPointer(float dt)
        {
            var touchscreen = Touchscreen.current;
            int touching = 0;
            if (touchscreen != null)
            {
                TouchControl first = null, second = null;
                foreach (var touch in touchscreen.touches)
                {
                    if (!touch.isInProgress)
                        continue;
                    touching++;
                    if (first == null) first = touch;
                    else if (second == null) second = touch;
                }

                if (touching >= 2)
                {
                    // Pinch: zoom by the change in finger spacing.
                    dragging = false;
                    float spacing = Vector2.Distance(first.position.ReadValue(), second.position.ReadValue());
                    if (lastPinchDistance > 0f && spacing > 0f)
                        ZoomBy(lastPinchDistance / spacing);
                    lastPinchDistance = spacing;
                    return;
                }
                lastPinchDistance = 0f;

                if (touching == 1)
                {
                    Vector2 position = first.position.ReadValue();
                    int id = first.touchId.ReadValue();
                    if (!dragging || dragTouchId != id)
                    {
                        // A new finger: only starts a drag off the menu.
                        dragging = !IsOverUi(position);
                        dragTouchId = id;
                    }
                    else
                    {
                        Drag(position - lastPointer, dt);
                    }
                    lastPointer = position;
                    return;
                }
                if (dragTouchId >= 0)
                {
                    dragging = false;
                    dragTouchId = -1;
                }
            }

            var mouse = Mouse.current;
            if (mouse == null || touching > 0)
                return;
            Vector2 pointer = mouse.position.ReadValue();
            if (mouse.leftButton.wasPressedThisFrame)
                dragging = !IsOverUi(pointer);
            else if (dragging && mouse.leftButton.isPressed)
                Drag(pointer - lastPointer, dt);
            else if (!mouse.leftButton.isPressed)
                dragging = false;
            lastPointer = pointer;

            float scroll = mouse.scroll.ReadValue().y;
            if (Mathf.Abs(scroll) > 0.01f && !IsOverUi(pointer))
                ZoomBy(Mathf.Exp(-scroll * 0.0015f)); // one 120-unit wheel notch ≈ 16% closer
        }

        void ReadButtons(float dt)
        {
            float turn = 0f, tilt = 0f, zoomRate = 0f;
            var gamepad = Gamepad.current;
            if (gamepad != null)
            {
                Vector2 stick = gamepad.rightStick.ReadValue();
                turn += stick.x;
                tilt -= stick.y;
                zoomRate += gamepad.leftTrigger.ReadValue() - gamepad.rightTrigger.ReadValue();
                if (gamepad.rightShoulder.wasPressedThisFrame)
                    CycleView(1);
                if (gamepad.leftShoulder.wasPressedThisFrame)
                    CycleView(-1);
            }
            var keyboard = Keyboard.current;
            if (keyboard != null)
            {
                if (keyboard.qKey.isPressed) turn -= 1f;
                if (keyboard.eKey.isPressed) turn += 1f;
                if (keyboard.equalsKey.isPressed || keyboard.numpadPlusKey.isPressed) zoomRate -= 1f;
                if (keyboard.minusKey.isPressed || keyboard.numpadMinusKey.isPressed) zoomRate += 1f;
            }
            if (Mathf.Abs(turn) > 0.15f)
                Rotate(-turn * 120f * dt);
            if (Mathf.Abs(tilt) > 0.15f)
                Tilt(tilt * 60f * dt);
            if (Mathf.Abs(zoomRate) > 0.05f)
                ZoomBy(Mathf.Exp(zoomRate * 1.2f * dt));
        }

        void CycleView(int step)
        {
            int count = System.Enum.GetValues(typeof(ShowroomView)).Length;
            int current = CurrentView.HasValue ? (int)CurrentView.Value : 0;
            SetView((ShowroomView)(((current + step) % count + count) % count));
        }

        bool IsOverUi(Vector2 screenPosition)
        {
            var eventSystem = EventSystem.current;
            if (eventSystem == null)
                return false;
            uiHits.Clear();
            eventSystem.RaycastAll(new PointerEventData(eventSystem) { position = screenPosition }, uiHits);
            return uiHits.Count > 0;
        }
    }
}
