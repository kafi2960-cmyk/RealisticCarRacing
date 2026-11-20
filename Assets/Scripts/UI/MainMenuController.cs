using System.Collections.Generic;
using RealisticCarRacing.Controls;
using RealisticCarRacing.Managers;
using RealisticCarRacing.Racing;
using RealisticCarRacing.Vehicles;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace RealisticCarRacing.UI
{
    /// <summary>
    /// Main menu flow: Main → Race select → Car select → race, plus the Garage (paint and setup) reachable from
    /// Main and Car select. Selections are saved in the player profile; the showroom shows the current car.
    /// Works with mouse/touch (click), keyboard (arrows/WASD, Enter, Esc) and gamepad (D-pad/stick, A, B).
    /// </summary>
    public sealed class MainMenuController : MonoBehaviour
    {
        public enum Screen
        {
            Main,
            RaceSelect,
            CarSelect,
            Garage,
            Settings
        }

        [SerializeField] ShowroomController showroom;

        readonly Dictionary<Screen, GameObject> screens = new Dictionary<Screen, GameObject>();
        readonly Dictionary<Screen, Selectable> firstSelection = new Dictionary<Screen, Selectable>();
        readonly List<(Button button, CarSetup setup, int paint)> paintButtons = new List<(Button, CarSetup, int)>();
        readonly Dictionary<TuningPreset, Text> tuningLabels = new Dictionary<TuningPreset, Text>();
        MenuInputActions input;
        Screen garageReturn = Screen.Main;
        Text footer, carName, carDescription, raceSummary, garageCarName, tuningDescription;
        Image[] carStatBars, garageStatBars;
        RectTransform paintGrid;

        public Screen Current { get; private set; }

        public void Configure(ShowroomController showroomController) => showroom = showroomController;

        void Awake()
        {
            input = new MenuInputActions();
            input.Back.performed += _ => Back();
        }

        void OnEnable() => input.Enable();

        void OnDisable() => input.Disable();

        void OnDestroy() => input.Dispose();

        void Start()
        {
            Time.timeScale = 1f;
            Build();
            RefreshCar();
            Show(Screen.Main);
        }

        // ---------- Navigation ----------

        public void Show(Screen screen)
        {
            foreach (var pair in screens)
                pair.Value.SetActive(pair.Key == screen);
            Current = screen;
            if (screen == Screen.Garage)
                RefreshGarage();
            if (EventSystem.current != null && firstSelection.TryGetValue(screen, out var first))
                EventSystem.current.SetSelectedGameObject(first.gameObject);
        }

        public void Back()
        {
            switch (Current)
            {
                case Screen.RaceSelect: Show(Screen.Main); break;
                case Screen.CarSelect: Show(Screen.RaceSelect); break;
                case Screen.Garage: Show(garageReturn); break;
                case Screen.Settings: Show(Screen.Main); break;
            }
        }

        void OpenGarage()
        {
            garageReturn = Current;
            Show(Screen.Garage);
        }

        void ChooseRace(RaceDefinition race)
        {
            GameSession.SelectRace(race);
            RefreshCar();
            Show(Screen.CarSelect);
        }

        void ChangeCar(int step)
        {
            var cars = GameSession.Catalog.cars;
            int index = (GameSession.Catalog.IndexOf(GameSession.SelectedCar) + step + cars.Length) % cars.Length;
            GameSession.SelectCar(cars[index]);
            RefreshCar();
        }

        void SetPaint(int paint)
        {
            GameSession.SelectedSetup.paint = paint;
            GameSession.Save();
            RefreshCar();
            RefreshGarage();
        }

        void SetTuning(TuningPreset preset)
        {
            GameSession.SelectedSetup.tuning = preset;
            GameSession.Save();
            RefreshCar();
            RefreshGarage();
        }

        void StartRace() => SceneFlow.StartRace(GameSession.SelectedRace);

        // ---------- Building ----------

        void Build()
        {
            var canvas = UiFactory.Canvas("MainMenu", 10);
            canvas.transform.SetParent(transform, false);

            var side = UiFactory.Image(canvas.transform, "SidePanel", UiTheme.Panel, UiFactory.White).rectTransform;
            side.Place(Vector2.zero, new Vector2(0f, 1f), new Vector2(0f, 0.5f), Vector2.zero, new Vector2(760f, 0f));
            var stripe = UiFactory.Image(side, "AccentStripe", UiTheme.Accent, UiFactory.White).rectTransform;
            stripe.Place(new Vector2(1f, 0f), Vector2.one, new Vector2(1f, 0.5f), Vector2.zero, new Vector2(6f, 0f));

            var title = UiFactory.Text(side, "REALISTIC\nCAR RACING", 76, UiTheme.Text, TextAnchor.UpperLeft, FontStyle.Bold, "Title");
            title.rectTransform.Place(new Vector2(0f, 1f), Vector2.one, new Vector2(0.5f, 1f), new Vector2(0f, -60f), new Vector2(-120f, 200f));
            title.lineSpacing = 0.9f;
            var subtitle = UiFactory.Text(side, "SINGLE PLAYER", 24, UiTheme.Accent, TextAnchor.UpperLeft, FontStyle.Bold, "Subtitle");
            subtitle.rectTransform.Place(new Vector2(0f, 1f), Vector2.one, new Vector2(0.5f, 1f), new Vector2(0f, -262f), new Vector2(-120f, 40f));

            var content = UiFactory.Rect("Screens", side);
            content.Place(Vector2.zero, Vector2.one, new Vector2(0.5f, 1f), new Vector2(0f, -330f), new Vector2(-120f, -420f));

            footer = UiFactory.Text(canvas.transform, "", 24, UiTheme.TextDim, TextAnchor.LowerRight, FontStyle.Normal, "Footer");
            footer.rectTransform.offsetMin = new Vector2(800f, 36f);
            footer.rectTransform.offsetMax = new Vector2(-48f, 0f);

            BuildMainScreen(content);
            BuildRaceSelect(content);
            BuildCarSelect(content);
            BuildGarage(content);
            var settings = AddScreen(Screen.Settings, content, "GRAPHICS");
            firstSelection[Screen.Settings] = GraphicsSettingsPanel.Build(settings, Back).FirstSelectable;
        }

        RectTransform AddScreen(Screen screen, RectTransform parent, string heading)
        {
            var root = UiFactory.Rect(screen.ToString(), parent).Stretch();
            UiFactory.Column(root, 14f);
            if (heading != null)
            {
                var text = UiFactory.Text(root, heading, 34, UiTheme.TextDim, TextAnchor.MiddleLeft, FontStyle.Bold, "Heading");
                UiFactory.Size(text, height: 50f);
            }
            screens[screen] = root.gameObject;
            return root;
        }

        void BuildMainScreen(RectTransform parent)
        {
            var root = AddScreen(Screen.Main, parent, null);
            firstSelection[Screen.Main] = UiFactory.Button(root, "RACE", () => Show(Screen.RaceSelect), 40, 96f);
            UiFactory.Button(root, "GARAGE", OpenGarage, 40, 96f);
            UiFactory.Button(root, "SETTINGS", () => Show(Screen.Settings), 40, 96f);
            bool canQuit = Application.platform != RuntimePlatform.IPhonePlayer && Application.platform != RuntimePlatform.WebGLPlayer;
            if (canQuit)
                UiFactory.Button(root, "QUIT", SceneFlow.Quit, 40, 96f);
        }

        void BuildRaceSelect(RectTransform parent)
        {
            var root = AddScreen(Screen.RaceSelect, parent, "SELECT RACE");
            foreach (var race in GameSession.Catalog.races)
            {
                var record = GameSession.Profile.RecordFor(race.id);
                string best = record != null && record.bestLap > 0f
                    ? $"Best lap {RaceTimeFormat.Format(record.bestLap)} · best finish {Ordinal(record.bestPosition)}"
                    : "No time set";
                var chosen = race;
                var button = UiFactory.Button(root, "", () => ChooseRace(chosen), 28, 118f, $"Race {race.id}");
                var label = button.GetComponentInChildren<Text>();
                label.alignment = TextAnchor.MiddleLeft;
                label.supportRichText = true;
                label.text = $"<size=34><b>{race.displayName.ToUpperInvariant()}</b></size>\n<size=22>{race.trackName} · {race.Summary}\n{best}</size>";
                firstSelection.TryAdd(Screen.RaceSelect, button);
            }
            UiFactory.Button(root, "BACK", Back);
        }

        void BuildCarSelect(RectTransform parent)
        {
            var root = AddScreen(Screen.CarSelect, parent, "SELECT CAR");
            var chooser = UiFactory.Rect("CarChooser", root);
            UiFactory.Row(chooser, 12f);
            UiFactory.Size(chooser, height: 84f);
            var previous = UiFactory.Button(chooser, "<", () => ChangeCar(-1), 40, name: "PrevCar");
            UiFactory.Size(previous, width: 84f, height: 84f);
            carName = UiFactory.Text(chooser, "", 40, UiTheme.Text, TextAnchor.MiddleCenter, FontStyle.Bold, "CarName");
            UiFactory.Size(carName, flexibleWidth: 1f);
            var next = UiFactory.Button(chooser, ">", () => ChangeCar(1), 40, name: "NextCar");
            UiFactory.Size(next, width: 84f, height: 84f);

            carDescription = UiFactory.Text(root, "", 24, UiTheme.TextDim, TextAnchor.UpperLeft, FontStyle.Normal, "Description");
            UiFactory.Size(carDescription, height: 70f);
            carStatBars = StatBars(root);
            raceSummary = UiFactory.Text(root, "", 24, UiTheme.Text, TextAnchor.MiddleLeft, FontStyle.Normal, "RaceSummary");
            UiFactory.Size(raceSummary, height: 40f);

            firstSelection[Screen.CarSelect] = UiFactory.Button(root, "START RACE", StartRace, 38, 96f);
            UiFactory.Button(root, "GARAGE", OpenGarage);
            UiFactory.Button(root, "BACK", Back);
        }

        void BuildGarage(RectTransform parent)
        {
            var root = AddScreen(Screen.Garage, parent, "GARAGE");
            garageCarName = UiFactory.Text(root, "", 36, UiTheme.Text, TextAnchor.MiddleLeft, FontStyle.Bold, "CarName");
            UiFactory.Size(garageCarName, height: 50f);

            UiFactory.Size(UiFactory.Text(root, "PAINT", 22, UiTheme.TextDim, TextAnchor.MiddleLeft, FontStyle.Bold), height: 30f);
            paintGrid = UiFactory.Rect("PaintGrid", root);
            var grid = paintGrid.gameObject.AddComponent<GridLayoutGroup>();
            grid.cellSize = new Vector2(66f, 66f);
            grid.spacing = new Vector2(10f, 10f);
            UiFactory.Size(paintGrid, height: 70f);

            UiFactory.Size(UiFactory.Text(root, "SETUP", 22, UiTheme.TextDim, TextAnchor.MiddleLeft, FontStyle.Bold), height: 30f);
            var presets = UiFactory.Rect("Presets", root);
            UiFactory.Row(presets, 10f).childForceExpandWidth = true;
            UiFactory.Size(presets, height: 72f);
            foreach (TuningPreset preset in System.Enum.GetValues(typeof(TuningPreset)))
            {
                var chosen = preset;
                var button = UiFactory.Button(presets, VehicleTuning.Title(preset), () => SetTuning(chosen), 24, 72f, $"Tuning {preset}");
                tuningLabels[preset] = button.GetComponentInChildren<Text>();
            }
            tuningDescription = UiFactory.Text(root, "", 22, UiTheme.TextDim, TextAnchor.UpperLeft, FontStyle.Normal, "TuningDescription");
            UiFactory.Size(tuningDescription, height: 60f);
            garageStatBars = StatBars(root);
            firstSelection[Screen.Garage] = UiFactory.Button(root, "BACK", Back);
        }

        static Image[] StatBars(Transform parent) => new[]
        {
            UiFactory.StatBar(parent, "POWER"),
            UiFactory.StatBar(parent, "TOP SPEED"),
            UiFactory.StatBar(parent, "GRIP"),
            UiFactory.StatBar(parent, "AGILITY")
        };

        // ---------- Refresh ----------

        void RefreshCar()
        {
            var car = GameSession.SelectedCar;
            var setup = GameSession.SelectedSetup;
            var race = GameSession.SelectedRace;
            carName.text = car.displayName.ToUpperInvariant();
            carDescription.text = car.description;
            raceSummary.text = $"Race: <b>{race.displayName}</b> · {race.Summary}";
            raceSummary.supportRichText = true;
            ShowStats(carStatBars, car, setup.tuning);
            footer.text = $"{car.displayName} · {VehicleTuning.Title(setup.tuning)}\nKeyboard / Controller / Touch";
            if (showroom != null)
                showroom.Show(car, setup.paint, setup.tuning);
        }

        void RefreshGarage()
        {
            var car = GameSession.SelectedCar;
            var setup = GameSession.SelectedSetup;
            garageCarName.text = car.displayName.ToUpperInvariant();

            // Swatches depend on the car's paint list, so rebuild when the car changes.
            if (paintButtons.Count != car.paints.Length || paintButtons.Count > 0 && paintButtons[0].setup != setup)
            {
                foreach (var (button, _, _) in paintButtons)
                    Destroy(button.gameObject);
                paintButtons.Clear();
                for (int i = 0; i < car.paints.Length; i++)
                {
                    int paint = i;
                    var swatch = UiFactory.Button(paintGrid, "", () => SetPaint(paint), 56, 66f, $"Paint {i}");
                    var colors = swatch.colors;
                    colors.normalColor = car.Paint(i);
                    colors.highlightedColor = Color.Lerp(car.Paint(i), Color.white, 0.3f);
                    colors.selectedColor = Color.Lerp(car.Paint(i), Color.white, 0.45f);
                    swatch.colors = colors;
                    paintButtons.Add((swatch, setup, i));
                }
            }
            foreach (var (button, _, paint) in paintButtons)
                button.GetComponentInChildren<Text>().text = paint == setup.paint ? "•" : "";
            foreach (var pair in tuningLabels)
                pair.Value.text = (pair.Key == setup.tuning ? "• " : "") + VehicleTuning.Title(pair.Key);
            tuningDescription.text = VehicleTuning.Describe(setup.tuning);
            ShowStats(garageStatBars, car, setup.tuning);
        }

        static void ShowStats(Image[] bars, CarDefinition car, TuningPreset tuning)
        {
            var tuned = VehicleTuning.CreateTuned(car.settings, tuning);
            var stats = CarStats.From(tuned);
            Destroy(tuned);
            bars[0].fillAmount = stats.PowerRating;
            bars[1].fillAmount = stats.TopSpeedRating;
            bars[2].fillAmount = stats.GripRating;
            bars[3].fillAmount = stats.AgilityRating;
        }

        static string Ordinal(int position)
        {
            if (position <= 0)
                return "-";
            string suffix = (position % 100) is >= 11 and <= 13 ? "th" : (position % 10) switch { 1 => "st", 2 => "nd", 3 => "rd", _ => "th" };
            return position + suffix;
        }
    }
}
