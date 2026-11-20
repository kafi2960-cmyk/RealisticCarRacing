using System.Collections;
using NUnit.Framework;
using RealisticCarRacing.Controls;
using RealisticCarRacing.Managers;
using RealisticCarRacing.Racing;
using RealisticCarRacing.UI;
using RealisticCarRacing.Vehicles;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace RealisticCarRacing.Tests
{
    /// <summary>
    /// The garage with simulated devices: turning the car by mouse, touch, gamepad and keyboard, zooming (wheel,
    /// pinch, triggers, buttons), the preset views, framing beside the menu panel, and the chosen wheels reaching the
    /// race car.
    /// </summary>
    public class GarageTests : InputTestFixture
    {
        MainMenuController menu;
        ShowroomOrbit orbit;
        ShowroomController showroom;

        public override void Setup()
        {
            base.Setup();
            GameSession.UseStorage(new MemoryStorage());
        }

        [UnityTearDown]
        public IEnumerator UnloadScenes()
        {
            yield return TestScenes.ResetToEmptyScene();
        }

        static IEnumerator Frames(int count)
        {
            for (int i = 0; i < count; i++)
                yield return null;
        }

        static IEnumerator Seconds(float seconds)
        {
            float end = Time.realtimeSinceStartup + seconds;
            while (Time.realtimeSinceStartup < end)
                yield return null;
        }

        IEnumerator OpenGarage()
        {
            SceneManager.LoadScene(SceneFlow.MainMenuScene);
            yield return Frames(3);
            menu = Object.FindAnyObjectByType<MainMenuController>();
            orbit = Object.FindAnyObjectByType<ShowroomOrbit>();
            showroom = Object.FindAnyObjectByType<ShowroomController>();
            menu.Show(MainMenuController.Screen.Garage);
            orbit.Settle();
            yield return Frames(2);
        }

        static void Click(string buttonName)
        {
            foreach (var button in Object.FindObjectsByType<Button>())
            {
                if (button.name == buttonName && button.isActiveAndEnabled && button.interactable)
                {
                    button.onClick.Invoke();
                    return;
                }
            }
            Assert.Fail($"No active button '{buttonName}'");
        }

        /// <summary>Screen point in the 3D view, clear of the menu panel and the overlays.</summary>
        static Vector2 InView => new Vector2(Screen.width * 0.8f, Screen.height * 0.5f);
        static Vector2 OnPanel => new Vector2(Screen.width * 0.15f, Screen.height * 0.5f);

        IEnumerator WaitForTurntableToStop()
        {
            float previous;
            do
            {
                previous = orbit.Yaw;
                yield return Frames(5);
            } while (Mathf.Abs(Mathf.DeltaAngle(previous, orbit.Yaw)) > 0.05f);
        }

        IEnumerator MouseDrag(Mouse mouse, Vector2 from, Vector2 delta)
        {
            Set(mouse.position, from);
            yield return Frames(1);
            Press(mouse.leftButton);
            yield return Frames(1);
            for (int i = 1; i <= 10; i++)
            {
                Set(mouse.position, from + delta * (i / 10f));
                yield return Frames(1);
            }
            Release(mouse.leftButton);
            yield return Frames(1);
        }

        [UnityTest]
        public IEnumerator Mouse_DragTurnsTheCar_WheelZooms_AndTheMenuPanelIsLeftAlone()
        {
            var mouse = InputSystem.AddDevice<Mouse>();
            yield return OpenGarage();
            float startYaw = orbit.Yaw, startPitch = orbit.Pitch;

            yield return MouseDrag(mouse, InView, new Vector2(-Screen.width * 0.2f, -Screen.height * 0.1f));
            Assert.That(Mathf.Abs(Mathf.DeltaAngle(startYaw, orbit.Yaw)), Is.GreaterThan(20f), "dragging sideways turns the turntable");
            Assert.That(orbit.CurrentView, Is.Null, "turning by hand leaves the preset view");
            yield return Seconds(0.5f);
            Assert.That(orbit.Pitch, Is.GreaterThan(startPitch + 2f), "dragging down raises the camera");

            yield return WaitForTurntableToStop(); // a fling coasts, then stops (no auto-spin in the garage)
            float settled = orbit.Yaw;
            yield return Seconds(1f);
            Assert.That(Mathf.DeltaAngle(settled, orbit.Yaw), Is.EqualTo(0f).Within(0.05f), "stays still in the garage");
            yield return MouseDrag(mouse, OnPanel, new Vector2(Screen.width * 0.2f, 0f));
            yield return Frames(10);
            Assert.That(Mathf.DeltaAngle(settled, orbit.Yaw), Is.EqualTo(0f).Within(0.05f), "drags that start on the menu panel don't turn the car");

            float distance = orbit.Distance;
            Set(mouse.position, InView);
            Set(mouse.scroll, new Vector2(0f, 240f));
            yield return Seconds(0.6f);
            Assert.That(orbit.Distance, Is.LessThan(distance * 0.8f), "wheel forward zooms in");
            distance = orbit.Distance;
            Set(mouse.scroll, new Vector2(0f, -480f));
            yield return Seconds(0.6f);
            Assert.That(orbit.Distance, Is.GreaterThan(distance * 1.2f), "wheel back zooms out");
        }

        [UnityTest]
        public IEnumerator Touch_OneFingerTurnsTheCar_PinchZooms()
        {
            InputSystem.AddDevice<Touchscreen>();
            yield return OpenGarage();
            float startYaw = orbit.Yaw;

            Vector2 start = InView;
            BeginTouch(1, start);
            yield return Frames(2);
            for (int i = 1; i <= 10; i++)
            {
                MoveTouch(1, start + new Vector2(Screen.width * 0.02f * i, 0f));
                yield return Frames(1);
            }
            EndTouch(1, start + new Vector2(Screen.width * 0.2f, 0f));
            yield return Frames(2);
            Assert.That(Mathf.Abs(Mathf.DeltaAngle(startYaw, orbit.Yaw)), Is.GreaterThan(20f), "one-finger drag turns the car");

            yield return WaitForTurntableToStop();
            float yaw = orbit.Yaw, distance = orbit.Distance;
            Vector2 centre = InView, gap = new Vector2(Screen.width * 0.03f, 0f);
            BeginTouch(2, centre - gap);
            BeginTouch(3, centre + gap);
            yield return Frames(2);
            for (int i = 1; i <= 10; i++)
            {
                MoveTouch(2, centre - gap * (1f + 0.2f * i));
                MoveTouch(3, centre + gap * (1f + 0.2f * i));
                yield return Frames(1);
            }
            EndTouch(2, centre - gap * 3f);
            EndTouch(3, centre + gap * 3f);
            yield return Seconds(0.6f);
            Assert.That(orbit.Distance, Is.LessThan(distance * 0.75f), "spreading two fingers zooms in");
            Assert.That(Mathf.DeltaAngle(yaw, orbit.Yaw), Is.EqualTo(0f).Within(1f), "a pinch doesn't also turn the car");
        }

        [UnityTest]
        public IEnumerator GamepadAndKeyboard_TurnZoomAndCycleViews()
        {
            var gamepad = InputSystem.AddDevice<Gamepad>();
            var keyboard = InputSystem.AddDevice<Keyboard>();
            yield return OpenGarage();

            float yaw = orbit.Yaw;
            Set(gamepad.rightStick, new Vector2(1f, 0f));
            yield return Seconds(0.4f);
            Set(gamepad.rightStick, Vector2.zero);
            Assert.That(Mathf.Abs(Mathf.DeltaAngle(yaw, orbit.Yaw)), Is.GreaterThan(20f), "right stick turns the car");

            float distance = orbit.Distance;
            Set(gamepad.rightTrigger, 1f);
            yield return Seconds(0.5f);
            Set(gamepad.rightTrigger, 0f);
            yield return Seconds(0.4f);
            Assert.That(orbit.Distance, Is.LessThan(distance * 0.8f), "right trigger zooms in");

            orbit.SetView(ShowroomView.ThreeQuarter);
            Press(gamepad.rightShoulder);
            yield return Frames(2);
            Release(gamepad.rightShoulder);
            Assert.That(orbit.CurrentView, Is.EqualTo(ShowroomView.Front), "bumpers step through the views");

            yaw = orbit.Yaw;
            Press(keyboard.qKey);
            yield return Seconds(0.4f);
            Release(keyboard.qKey);
            Assert.That(Mathf.Abs(Mathf.DeltaAngle(yaw, orbit.Yaw)), Is.GreaterThan(20f), "Q/E turn the car");
            distance = orbit.Distance;
            Press(keyboard.minusKey);
            yield return Seconds(0.5f);
            Release(keyboard.minusKey);
            yield return Seconds(0.4f);
            Assert.That(orbit.Distance, Is.GreaterThan(distance * 1.2f), "minus zooms out");
        }

        [UnityTest]
        public IEnumerator ViewButtons_ShowFrontSideAndRear_WithTheWholeCarBesideThePanel()
        {
            yield return OpenGarage();
            var camera = orbit.Camera;
            foreach (var (button, view, facing) in new[] { ("View Front", ShowroomView.Front, -1f), ("View Side", ShowroomView.Side, 0f), ("View Rear", ShowroomView.Rear, 1f) })
            {
                Click(button);
                yield return Seconds(1.5f);
                Assert.That(orbit.CurrentView, Is.EqualTo(view));
                Vector3 carForward = showroom.CurrentCar.transform.forward;
                Vector3 look = Vector3.ProjectOnPlane(camera.transform.forward, Vector3.up).normalized;
                Assert.That(Vector3.Dot(carForward, look), Is.EqualTo(facing).Within(0.03f), $"{view}: car seen from the right side");

                // The whole car is on screen, to the right of the menu panel.
                var bounds = new Bounds(showroom.CurrentCar.transform.position, Vector3.zero);
                foreach (var renderer in showroom.CurrentCar.GetComponentsInChildren<Renderer>())
                    bounds.Encapsulate(renderer.bounds);
                float panelEdge = PanelFraction();
                for (int corner = 0; corner < 8; corner++)
                {
                    var point = new Vector3((corner & 1) == 0 ? bounds.min.x : bounds.max.x, (corner & 2) == 0 ? bounds.min.y : bounds.max.y,
                        (corner & 4) == 0 ? bounds.min.z : bounds.max.z);
                    Vector3 viewport = camera.WorldToViewportPoint(point);
                    Assert.That(viewport.x, Is.InRange(panelEdge, 1f), $"{view}: car clear of the panel");
                    Assert.That(viewport.y, Is.InRange(0f, 1f), $"{view}: car inside the screen");
                }
            }

            float distance = orbit.Distance;
            Click("ZoomIn");
            yield return Seconds(0.6f);
            Assert.That(orbit.Distance, Is.LessThan(distance));
            for (int i = 0; i < 12; i++)
                Click("ZoomIn");
            yield return Seconds(0.8f);
            Assert.That(orbit.Distance, Is.GreaterThanOrEqualTo(3.09f), "zoom stops before the camera reaches the car");
            for (int i = 0; i < 12; i++)
                Click("ZoomOut");
            yield return Seconds(0.8f);
            Assert.That(orbit.Zoom, Is.LessThanOrEqualTo(1.46f), "and before the car becomes tiny");
        }

        float PanelFraction()
        {
            var panel = (RectTransform)menu.transform.Find("MainMenu/SidePanel");
            return panel.rect.width / ((RectTransform)panel.parent).rect.width;
        }

        [UnityTest]
        public IEnumerator ChosenCarPaintAndWheels_AreWhatYouRaceWith()
        {
            yield return OpenGarage();
            var catalog = GameSession.Catalog;
            Click("NextCar");
            Click("Tab PAINT");
            Click("Paint 5");
            Click("Tab WHEELS");
            Click("Wheels 3");
            yield return null;
            var car = catalog.cars[1];
            Assert.That(showroom.CurrentDefinition, Is.EqualTo(car));
            Assert.That(showroom.ShownRim, Is.SameAs(car.visualKit.wheelDesigns[3].rim));

            Click("Tab SPECS");
            yield return null;
            var tuned = VehicleTuning.CreateTuned(car.settings, GameSession.SelectedSetup.tuning);
            var stats = CarStats.From(tuned);
            Object.Destroy(tuned);
            var specs = GameObject.Find("Page Specs").GetComponentsInChildren<Text>();
            string allSpecs = string.Join("|", System.Array.ConvertAll(specs, t => t.text));
            StringAssert.Contains(stats.PowerHp.ToString("N0", System.Globalization.CultureInfo.InvariantCulture) + " hp", allSpecs, "specs show the car's power");
            StringAssert.Contains(stats.DriveLabel, allSpecs);

            Click("Button START RACE");
            float end = Time.realtimeSinceStartup + 15f;
            while (Object.FindAnyObjectByType<RaceManager>() == null && Time.realtimeSinceStartup < end)
                yield return null;
            yield return Frames(2);
            var race = Object.FindAnyObjectByType<RaceManager>();
            Assert.That(race.Player.Car, Is.EqualTo(car));
            var rims = race.Player.Vehicle.transform.Find(TestVehicleFactory.WheelVisualsName).GetComponentsInChildren<MeshFilter>();
            int fitted = 0;
            foreach (var rim in rims)
            {
                if (rim.name != TestVehicleFactory.RimName)
                    continue;
                Assert.That(rim.sharedMesh, Is.SameAs(car.visualKit.wheelDesigns[3].rim));
                fitted++;
            }
            Assert.That(fitted, Is.EqualTo(4), "all four wheels");
            var paint = race.Player.Vehicle.transform.Find("Chassis").GetComponent<Renderer>().sharedMaterial.GetColor("_BaseColor");
            Assert.That(Vector4.Distance(paint, car.Paint(5)), Is.LessThan(0.001f));
            yield return TestScenes.ResetToEmptyScene();
        }
    }

    /// <summary>
    /// The garage's figures against the real physics: the calculated 0-100 km/h must match a timed full-throttle run
    /// within 8 % for every car and setup.
    /// </summary>
    public class CarSpecsTests
    {
        sealed class FullThrottle : IVehicleInputSource
        {
            public VehicleInputState ReadInput() => new VehicleInputState { Throttle = 1f };
        }

        sealed class Idle : IVehicleInputSource
        {
            public VehicleInputState ReadInput() => default;
        }

        [UnityTest]
        public IEnumerator ZeroTo100_MatchesATimedRunForEveryCar()
        {
            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.transform.localScale = new Vector3(100f, 1f, 400f);
            var catalog = Resources.Load<GameCatalog>(GameCatalog.ResourcePath);
            var report = new System.Text.StringBuilder();
            var failures = new System.Collections.Generic.List<string>();
            foreach (var car in catalog.cars)
            foreach (TuningPreset tuning in System.Enum.GetValues(typeof(TuningPreset)))
            {
                var vehicle = CarBuilder.Build(car, 0, 0, tuning, new Vector3(0f, 0.5f, -1800f), Quaternion.identity, false, car.id);
                vehicle.SetInputSource(new Idle());
                float settle = Time.fixedTime + 1.5f;
                while (Time.fixedTime < settle)
                    yield return new WaitForFixedUpdate();

                vehicle.SetInputSource(new FullThrottle());
                float start = Time.fixedTime;
                while (vehicle.ForwardSpeed * 3.6f < 100f && Time.fixedTime - start < 20f)
                    yield return new WaitForFixedUpdate();
                float measured = Time.fixedTime - start;
                float calculated = CarStats.From(vehicle.Settings).zeroTo100Seconds;
                string line = $"{car.displayName} ({tuning}): measured {measured:0.00} s, calculated {calculated:0.00} s";
                report.AppendLine(line);
                if (Mathf.Abs(calculated - measured) > measured * 0.08f)
                    failures.Add(line);
                Object.Destroy(vehicle.gameObject);
                yield return null;
            }
            Debug.Log(report.ToString());
            Object.Destroy(ground);
            Assert.That(failures, Is.Empty, $"calculated 0-100 km/h more than 8 % off the timed run\n{report}");
        }
    }
}
