using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.OnScreen;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace RealisticCarRacing.UI
{
    /// <summary>
    /// Builds the on-screen driving controls. Every control is an Input System on-screen control that writes to a
    /// virtual gamepad, so touch reaches the same actions (and the same vehicle physics) as a real controller:
    /// steering stick → left stick, GAS → right trigger, BRAKE → left trigger, HAND → A, +/− → bumpers,
    /// A/M → Y, RESET → View, CAM → right-stick click.
    /// </summary>
    public static class TouchControlsFactory
    {
        public sealed class TouchControls
        {
            public Canvas canvas;
            public TouchControlsVisibility visibility;
            public RectTransform steeringArea, steeringKnob;
            public RectTransform throttle, brake, handbrake, shiftUp, shiftDown, toggleTransmission, reset, cameraButton, horn;
        }

        /// <param name="buttonSprite">Optional rounded sprite (the editor passes Unity's built-in UI sprite).</param>
        /// <param name="knobSprite">Optional circular sprite for the steering stick.</param>
        public static TouchControls Create(Sprite buttonSprite = null, Sprite knobSprite = null, Font font = null,
            TouchControlsVisibility.Mode visibility = TouchControlsVisibility.Mode.Auto)
        {
            if (Object.FindAnyObjectByType<EventSystem>() == null)
            {
                var eventSystem = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
                eventSystem.GetComponent<InputSystemUIInputModule>().AssignDefaultActions();
            }
            font ??= Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            var canvasObject = new GameObject("TouchControlsCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 1f;

            var root = new GameObject("TouchControls", typeof(RectTransform));
            var rootRect = (RectTransform)root.transform;
            rootRect.SetParent(canvasObject.transform, false);
            rootRect.anchorMin = Vector2.zero;
            rootRect.anchorMax = Vector2.one;
            rootRect.offsetMin = rootRect.offsetMax = Vector2.zero;

            var controls = new TouchControls { canvas = canvas };

            // Steering: drag the knob left/right (only X is used).
            controls.steeringArea = CreateImage("SteeringArea", rootRect, new Vector2(0f, 0f), new Vector2(240f, 230f), new Vector2(300f, 300f), knobSprite, 0.15f);
            controls.steeringKnob = CreateImage("SteeringKnob", controls.steeringArea, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(140f, 140f), knobSprite, 0.45f);
            var stick = controls.steeringKnob.gameObject.AddComponent<OnScreenStick>();
            stick.controlPath = "<Gamepad>/leftStick";
            stick.movementRange = 120f;

            var bottomRight = new Vector2(1f, 0f);
            var topRight = new Vector2(1f, 1f);
            controls.throttle = CreateButton("Throttle", "GAS", "<Gamepad>/rightTrigger", rootRect, bottomRight, new Vector2(-170f, 230f), new Vector2(220f, 320f), buttonSprite, font);
            controls.brake = CreateButton("Brake", "BRAKE\nREV", "<Gamepad>/leftTrigger", rootRect, bottomRight, new Vector2(-420f, 190f), new Vector2(220f, 240f), buttonSprite, font);
            controls.handbrake = CreateButton("Handbrake", "HAND", "<Gamepad>/buttonSouth", rootRect, bottomRight, new Vector2(-420f, 400f), new Vector2(220f, 120f), buttonSprite, font);
            controls.shiftUp = CreateButton("ShiftUp", "+", "<Gamepad>/rightShoulder", rootRect, bottomRight, new Vector2(-170f, 470f), new Vector2(220f, 110f), buttonSprite, font);
            controls.shiftDown = CreateButton("ShiftDown", "−", "<Gamepad>/leftShoulder", rootRect, bottomRight, new Vector2(-170f, 590f), new Vector2(220f, 110f), buttonSprite, font);
            controls.cameraButton = CreateButton("Camera", "CAM", "<Gamepad>/rightStickPress", rootRect, topRight, new Vector2(-130f, -80f), new Vector2(200f, 100f), buttonSprite, font);
            controls.toggleTransmission = CreateButton("ToggleTransmission", "A / M", "<Gamepad>/buttonNorth", rootRect, topRight, new Vector2(-350f, -80f), new Vector2(200f, 100f), buttonSprite, font);
            controls.reset = CreateButton("Reset", "RESET", "<Gamepad>/select", rootRect, topRight, new Vector2(-570f, -80f), new Vector2(200f, 100f), buttonSprite, font);
            controls.horn = CreateButton("Horn", "HORN", "<Gamepad>/leftStickPress", rootRect, topRight, new Vector2(-790f, -80f), new Vector2(200f, 100f), buttonSprite, font);

            controls.visibility = canvasObject.AddComponent<TouchControlsVisibility>();
            controls.visibility.Configure(root, visibility);
            return controls;
        }

        static RectTransform CreateButton(string name, string label, string controlPath, RectTransform parent, Vector2 anchor,
            Vector2 position, Vector2 size, Sprite sprite, Font font)
        {
            var rect = CreateImage(name, parent, anchor, position, size, sprite, 0.3f);
            rect.gameObject.AddComponent<OnScreenButton>().controlPath = controlPath;

            var text = new GameObject("Label", typeof(RectTransform), typeof(Text));
            var textRect = (RectTransform)text.transform;
            textRect.SetParent(rect, false);
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = textRect.offsetMax = Vector2.zero;
            var uiText = text.GetComponent<Text>();
            uiText.text = label;
            uiText.font = font;
            uiText.fontSize = 40;
            uiText.fontStyle = FontStyle.Bold;
            uiText.alignment = TextAnchor.MiddleCenter;
            uiText.color = new Color(1f, 1f, 1f, 0.9f);
            uiText.raycastTarget = false;
            return rect;
        }

        static RectTransform CreateImage(string name, RectTransform parent, Vector2 anchor, Vector2 position, Vector2 size, Sprite sprite, float alpha)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = anchor;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            var image = go.GetComponent<Image>();
            image.sprite = sprite;
            image.type = sprite != null ? Image.Type.Sliced : Image.Type.Simple;
            image.color = new Color(1f, 1f, 1f, alpha);
            return rect;
        }
    }
}
