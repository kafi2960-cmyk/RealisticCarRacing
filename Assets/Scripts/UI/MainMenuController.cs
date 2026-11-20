using System.Collections.Generic;
using System.Globalization;
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
    /// Main menu flow: Main → Race select → Garage → race, with the Garage also reachable straight from Main.
    /// The Garage is where the car is chosen and prepared: previous/next car, paint, wheels, setup and full
    /// specifications, preset camera views and zoom, and START RACE. Selections are saved in the player profile and
    /// the showroom shows the result live. Works with mouse/touch (click, drag, pinch), keyboard (arrows/WASD, Enter,
    /// Esc, Q/E, +/-) and gamepad (D-pad/stick, A, B, right stick, triggers, bumpers).
    /// </summary>
    public sealed class MainMenuController : MonoBehaviour
    {
        public enum Screen
        {
            Main,
            RaceSelect,
            Garage,
            Settings,
            Audio
        }

        public enum GarageTab
        {
            Paint,
            Wheels,
            Setup,
            Specs
        }

        static readonly CultureInfo Numbers = CultureInfo.InvariantCulture;

        [SerializeField] ShowroomController showroom;
        [SerializeField] ShowroomOrbit orbit;

        readonly Dictionary<Screen, GameObject> screens = new Dictionary<Screen, GameObject>();
        readonly Dictionary<Screen, Selectable> firstSelection = new Dictionary<Screen, Selectable>();
        readonly Dictionary<GarageTab, GameObject> tabPages = new Dictionary<GarageTab, GameObject>();
        readonly Dictionary<GarageTab, Text> tabLabels = new Dictionary<GarageTab, Text>();
        readonly Dictionary<ShowroomView, Text> viewLabels = new Dictionary<ShowroomView, Text>();
        readonly List<(Button button, CarDefinition car, int paint)> paintButtons = new List<(Button, CarDefinition, int)>();
        readonly List<(Text label, int design)> wheelLabels = new List<(Text, int)>();
        readonly Dictionary<TuningPreset, Text> tuningLabels = new Dictionary<TuningPreset, Text>();
        readonly List<Text> specValues = new List<Text>();
        MenuInputActions input;
        Screen garageReturn = Screen.Main;
        RectTransform sidePanel, content, paintGrid, wheelList;
        GameObject titleBlock, garageOverlay;
        Text footer, carName, carCount, carDescription, raceSummary, paintName, tuningDescription;
        Text cardName, cardClass, cardPower, cardSprint, cardTopSpeed, cardWeight;
        Image[] statBars;

        public Screen Current { get; private set; }
        public GarageTab CurrentTab { get; private set; }

        public void Configure(ShowroomController showroomController, ShowroomOrbit showroomOrbit)
        {
            showroom = showroomController;
            orbit = showroomOrbit;
        }

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
            if (Audio.GameAudio.Instance != null)
                Audio.UIAudioManager.PlayMusic(Audio.GameAudio.Instance.menuMusic);
            if (orbit != null)
                orbit.SetPanel(sidePanel);
            ShowTab(GarageTab.Paint);
            RefreshCar();
            Show(Screen.Main);
        }

        // ---------- Navigation ----------

        public void Show(Screen screen)
        {
            foreach (var pair in screens)
                pair.Value.SetActive(pair.Key == screen);
            Current = screen;

            // The garage needs the whole panel height: the title makes way for it.
            bool garage = screen == Screen.Garage;
            titleBlock.SetActive(!garage);
            content.anchoredPosition = new Vector2(0f, garage ? -56f : -330f);
            content.sizeDelta = new Vector2(-120f, garage ? -136f : -420f);
            garageOverlay.SetActive(garage);
            footer.gameObject.SetActive(!garage);
            if (orbit != null)
            {
                orbit.AutoSpin = !garage;
                if (garage)
                    orbit.SetView(ShowroomView.ThreeQuarter);
            }
            if (garage)
                RefreshCar();
            if (EventSystem.current != null && firstSelection.TryGetValue(screen, out var first))
                EventSystem.current.SetSelectedGameObject(first.gameObject);
        }

        public void Back()
        {
            switch (Current)
            {
                case Screen.RaceSelect: Show(Screen.Main); break;
                case Screen.Garage: Show(garageReturn); break;
                case Screen.Settings: Show(Screen.Main); break;
                case Screen.Audio: Show(Screen.Main); break;
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
            OpenGarage();
        }

        public void ShowTab(GarageTab tab)
        {
            CurrentTab = tab;
            foreach (var pair in tabPages)
                pair.Value.SetActive(pair.Key == tab);
            foreach (var pair in tabLabels)
                pair.Value.color = pair.Key == tab ? UiTheme.Text : UiTheme.TextDim;
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
        }

        void SetWheels(int design)
        {
            GameSession.SelectedSetup.wheels = design;
            GameSession.Save();
            RefreshCar();
        }

        void SetTuning(TuningPreset preset)
        {
            GameSession.SelectedSetup.tuning = preset;
            GameSession.Save();
            RefreshCar();
        }

        void SetView(ShowroomView view)
        {
            if (orbit != null)
                orbit.SetView(view);
        }

        void Zoom(float factor)
        {
            if (orbit != null)
                orbit.ZoomBy(factor);
        }

        void StartRace() => SceneFlow.StartRace(GameSession.SelectedRace);

        void Update()
        {
            // The view buttons follow the turntable: none is marked once the car has been turned by hand.
            if (Current != Screen.Garage || orbit == null)
                return;
            foreach (var pair in viewLabels)
                pair.Value.color = orbit.CurrentView == pair.Key ? UiTheme.Accent : UiTheme.Text;
        }

        // ---------- Building ----------

        void Build()
        {
            var canvas = UiFactory.Canvas("MainMenu", 10);
            canvas.transform.SetParent(transform, false);

            var sideImage = UiFactory.Image(canvas.transform, "SidePanel", UiTheme.Panel, UiFactory.White);
            sideImage.raycastTarget = true; // drags on the panel don't turn the car
            sidePanel = sideImage.rectTransform;
            sidePanel.Place(Vector2.zero, new Vector2(0f, 1f), new Vector2(0f, 0.5f), Vector2.zero, new Vector2(760f, 0f));
            var stripe = UiFactory.Image(sidePanel, "AccentStripe", UiTheme.Accent, UiFactory.White).rectTransform;
            stripe.Place(new Vector2(1f, 0f), Vector2.one, new Vector2(1f, 0.5f), Vector2.zero, new Vector2(6f, 0f));

            titleBlock = UiFactory.Rect("TitleBlock", sidePanel).Stretch().gameObject;
            var title = UiFactory.Text(titleBlock.transform, "REALISTIC\nCAR RACING", 76, UiTheme.Text, TextAnchor.UpperLeft, FontStyle.Bold, "Title");
            title.rectTransform.Place(new Vector2(0f, 1f), Vector2.one, new Vector2(0.5f, 1f), new Vector2(0f, -60f), new Vector2(-120f, 200f));
            title.lineSpacing = 0.9f;
            var subtitle = UiFactory.Text(titleBlock.transform, "SINGLE PLAYER", 24, UiTheme.Accent, TextAnchor.UpperLeft, FontStyle.Bold, "Subtitle");
            subtitle.rectTransform.Place(new Vector2(0f, 1f), Vector2.one, new Vector2(0.5f, 1f), new Vector2(0f, -262f), new Vector2(-120f, 40f));

            content = UiFactory.Rect("Screens", sidePanel);
            content.Place(Vector2.zero, Vector2.one, new Vector2(0.5f, 1f), new Vector2(0f, -330f), new Vector2(-120f, -420f));

            footer = UiFactory.Text(canvas.transform, "", 24, UiTheme.TextDim, TextAnchor.LowerRight, FontStyle.Normal, "Footer");
            footer.rectTransform.offsetMin = new Vector2(800f, 36f);
            footer.rectTransform.offsetMax = new Vector2(-48f, 0f);

            BuildMainScreen(content);
            BuildRaceSelect(content);
            BuildGarage(content);
            BuildGarageOverlay(canvas.transform);
            var settings = AddScreen(Screen.Settings, content, "GRAPHICS");
            firstSelection[Screen.Settings] = GraphicsSettingsPanel.Build(settings, Back).FirstSelectable;
            var audioScreen = AddScreen(Screen.Audio, content, "AUDIO");
            audioScreen.GetComponent<VerticalLayoutGroup>().spacing = 8f;
            firstSelection[Screen.Audio] = AudioSettingsPanel.Build(audioScreen, Back).FirstSelectable;
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
            UiFactory.Button(root, "AUDIO", () => Show(Screen.Audio), 40, 96f);
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

        void BuildGarage(RectTransform parent)
        {
            var root = AddScreen(Screen.Garage, parent, null);
            root.GetComponent<VerticalLayoutGroup>().spacing = 12f;

            var heading = UiFactory.Rect("Heading", root);
            UiFactory.Row(heading, 0f);
            UiFactory.Size(heading, height: 40f);
            UiFactory.Size(UiFactory.Text(heading, "GARAGE", 30, UiTheme.TextDim, TextAnchor.MiddleLeft, FontStyle.Bold), flexibleWidth: 1f);
            carCount = UiFactory.Text(heading, "", 24, UiTheme.TextDim, TextAnchor.MiddleRight, FontStyle.Bold, "CarCount");
            UiFactory.Size(carCount, width: 200f);

            var chooser = UiFactory.Rect("CarChooser", root);
            UiFactory.Row(chooser, 12f);
            UiFactory.Size(chooser, height: 80f);
            UiFactory.Size(UiFactory.Button(chooser, "<", () => ChangeCar(-1), 40, name: "PrevCar"), width: 80f, height: 80f);
            carName = UiFactory.Text(chooser, "", 40, UiTheme.Text, TextAnchor.MiddleCenter, FontStyle.Bold, "CarName");
            UiFactory.Size(carName, flexibleWidth: 1f);
            UiFactory.Size(UiFactory.Button(chooser, ">", () => ChangeCar(1), 40, name: "NextCar"), width: 80f, height: 80f);

            carDescription = UiFactory.Text(root, "", 22, UiTheme.TextDim, TextAnchor.UpperLeft, FontStyle.Normal, "Description");
            UiFactory.Size(carDescription, height: 56f);

            var tabs = UiFactory.Rect("Tabs", root);
            UiFactory.Row(tabs, 8f).childForceExpandWidth = true;
            UiFactory.Size(tabs, height: 60f);
            foreach (GarageTab tab in System.Enum.GetValues(typeof(GarageTab)))
            {
                var chosen = tab;
                string title = tab.ToString().ToUpperInvariant();
                var button = UiFactory.Button(tabs, title, () => ShowTab(chosen), 24, 60f, $"Tab {title}");
                tabLabels[tab] = button.GetComponentInChildren<Text>();
            }

            // Pages share one flexible slot, so the panel fits short (phone) screens as well as tall ones.
            var pages = UiFactory.Rect("TabPages", root);
            var pagesLayout = UiFactory.Size(pages, height: 330f);
            pagesLayout.minHeight = 300f;
            pagesLayout.flexibleHeight = 1f;
            tabPages[GarageTab.Paint] = BuildPaintPage(Page(pages, GarageTab.Paint));
            tabPages[GarageTab.Wheels] = BuildWheelsPage(Page(pages, GarageTab.Wheels));
            tabPages[GarageTab.Setup] = BuildSetupPage(Page(pages, GarageTab.Setup));
            tabPages[GarageTab.Specs] = BuildSpecsPage(Page(pages, GarageTab.Specs));

            raceSummary = UiFactory.Text(root, "", 22, UiTheme.Text, TextAnchor.MiddleLeft, FontStyle.Normal, "RaceSummary");
            raceSummary.supportRichText = true;
            UiFactory.Size(raceSummary, height: 34f);
            firstSelection[Screen.Garage] = UiFactory.Button(root, "START RACE", StartRace, 38, 92f);
            UiFactory.Button(root, "BACK", Back, 30, 68f);
        }

        static RectTransform Page(RectTransform parent, GarageTab tab)
        {
            var page = UiFactory.Rect($"Page {tab}", parent).Stretch();
            UiFactory.Column(page, 10f);
            return page;
        }

        static Text Caption(Transform parent, string text, string name = "Caption")
        {
            var caption = UiFactory.Text(parent, text, 22, UiTheme.TextDim, TextAnchor.MiddleLeft, FontStyle.Bold, name);
            UiFactory.Size(caption, height: 32f);
            return caption;
        }

        GameObject BuildPaintPage(RectTransform page)
        {
            paintName = Caption(page, "", "PaintName");
            paintName.supportRichText = true;
            paintGrid = UiFactory.Rect("PaintGrid", page);
            var grid = paintGrid.gameObject.AddComponent<GridLayoutGroup>();
            grid.cellSize = new Vector2(68f, 68f);
            grid.spacing = new Vector2(12f, 12f);
            UiFactory.Size(paintGrid, height: 72f);
            var note = UiFactory.Text(page, "Metallic base coat under a gloss clear coat. Paint is cosmetic: it doesn't change the car's performance.",
                20, UiTheme.TextDim, TextAnchor.UpperLeft, FontStyle.Normal, "PaintNote");
            UiFactory.Size(note, height: 54f);
            return page.gameObject;
        }

        GameObject BuildWheelsPage(RectTransform page)
        {
            Caption(page, "WHEELS");
            wheelList = UiFactory.Rect("WheelList", page);
            UiFactory.Column(wheelList, 8f);
            UiFactory.Size(wheelList, height: 4 * 62f + 3 * 8f);
            var note = UiFactory.Text(page, "Same tyre size on every design: wheels are cosmetic.", 20, UiTheme.TextDim, TextAnchor.UpperLeft,
                FontStyle.Normal, "WheelNote");
            UiFactory.Size(note, height: 28f);
            return page.gameObject;
        }

        GameObject BuildSetupPage(RectTransform page)
        {
            Caption(page, "SETUP");
            var presets = UiFactory.Rect("Presets", page);
            UiFactory.Row(presets, 10f).childForceExpandWidth = true;
            UiFactory.Size(presets, height: 64f);
            foreach (TuningPreset preset in System.Enum.GetValues(typeof(TuningPreset)))
            {
                var chosen = preset;
                var button = UiFactory.Button(presets, VehicleTuning.Title(preset), () => SetTuning(chosen), 24, 64f, $"Tuning {preset}");
                tuningLabels[preset] = button.GetComponentInChildren<Text>();
            }
            tuningDescription = UiFactory.Text(page, "", 20, UiTheme.TextDim, TextAnchor.UpperLeft, FontStyle.Normal, "TuningDescription");
            UiFactory.Size(tuningDescription, height: 52f);
            statBars = new[]
            {
                UiFactory.StatBar(page, "POWER"),
                UiFactory.StatBar(page, "TOP SPEED"),
                UiFactory.StatBar(page, "GRIP"),
                UiFactory.StatBar(page, "AGILITY")
            };
            return page.gameObject;
        }

        static readonly string[] SpecNames = { "POWER", "TORQUE", "0–100 KM/H", "TOP SPEED", "WEIGHT", "POWER / WEIGHT", "DRIVETRAIN", "GEARBOX" };

        GameObject BuildSpecsPage(RectTransform page)
        {
            Caption(page, "SPECIFICATIONS");
            foreach (string name in SpecNames)
            {
                var row = UiFactory.Rect($"Spec {name}", page);
                UiFactory.Row(row, 12f);
                UiFactory.Size(row, height: 30f);
                UiFactory.Size(UiFactory.Text(row, name, 20, UiTheme.TextDim, TextAnchor.MiddleLeft, FontStyle.Bold), width: 230f);
                var value = UiFactory.Text(row, "", 22, UiTheme.Text, TextAnchor.MiddleRight, FontStyle.Bold, "Value");
                UiFactory.Size(value, flexibleWidth: 1f);
                specValues.Add(value);
            }
            var note = UiFactory.Text(page, "Calculated from the car's physics with the current setup.", 18, UiTheme.TextDim, TextAnchor.UpperLeft,
                FontStyle.Italic, "SpecNote");
            UiFactory.Size(note, height: 26f);
            return page.gameObject;
        }

        /// <summary>Over the 3D view: the car's headline figures (top right) and the camera views and zoom (bottom right).</summary>
        void BuildGarageOverlay(Transform canvas)
        {
            var overlay = UiFactory.Rect("GarageOverlay", canvas).Stretch();
            garageOverlay = overlay.gameObject;

            var card = UiFactory.Image(overlay, "SpecCard", new Color(0.04f, 0.05f, 0.07f, 0.72f), UiFactory.White).rectTransform;
            card.Place(Vector2.one, Vector2.one, Vector2.one, new Vector2(-40f, -36f), new Vector2(720f, 196f));
            var accent = UiFactory.Image(card, "Accent", UiTheme.Accent, UiFactory.White).rectTransform;
            accent.Place(Vector2.zero, new Vector2(0f, 1f), new Vector2(0f, 0.5f), Vector2.zero, new Vector2(6f, 0f));
            cardName = UiFactory.Text(card, "", 44, UiTheme.Text, TextAnchor.UpperLeft, FontStyle.Bold, "CardName");
            cardName.rectTransform.Place(new Vector2(0f, 1f), Vector2.one, new Vector2(0.5f, 1f), new Vector2(10f, -16f), new Vector2(-60f, 54f));
            cardClass = UiFactory.Text(card, "", 20, UiTheme.TextDim, TextAnchor.UpperLeft, FontStyle.Normal, "CardClass");
            cardClass.rectTransform.Place(new Vector2(0f, 1f), Vector2.one, new Vector2(0.5f, 1f), new Vector2(10f, -70f), new Vector2(-60f, 28f));
            var figures = UiFactory.Rect("Figures", card);
            figures.Place(Vector2.zero, new Vector2(1f, 0f), new Vector2(0.5f, 0f), new Vector2(10f, 18f), new Vector2(-60f, 80f));
            UiFactory.Row(figures, 10f).childForceExpandWidth = true;
            cardPower = Figure(figures, "POWER");
            cardSprint = Figure(figures, "0–100 KM/H");
            cardTopSpeed = Figure(figures, "TOP SPEED");
            cardWeight = Figure(figures, "WEIGHT");

            var views = UiFactory.Rect("Views", overlay);
            views.Place(new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-40f, 36f), new Vector2(720f, 64f));
            UiFactory.Row(views, 8f);
            foreach (var (view, label) in new[] { (ShowroomView.ThreeQuarter, "3/4"), (ShowroomView.Front, "FRONT"), (ShowroomView.Side, "SIDE"), (ShowroomView.Rear, "REAR") })
            {
                var chosen = view;
                var button = UiFactory.Button(views, label, () => SetView(chosen), 22, 64f, $"View {view}");
                UiFactory.Size(button, height: 64f, flexibleWidth: 1f);
                viewLabels[view] = button.GetComponentInChildren<Text>();
            }
            UiFactory.Size(UiFactory.Button(views, "−", () => Zoom(1.25f), 34, 64f, "ZoomOut"), width: 72f);
            UiFactory.Size(UiFactory.Button(views, "+", () => Zoom(0.8f), 34, 64f, "ZoomIn"), width: 72f);

            bool touch = Application.isMobilePlatform;
            var hint = UiFactory.Text(overlay, touch ? "Drag to turn the car · pinch to zoom" : "Drag to turn the car · scroll to zoom · Q/E, +/−, right stick, triggers",
                20, UiTheme.TextDim, TextAnchor.LowerRight, FontStyle.Normal, "ControlsHint");
            hint.rectTransform.Place(new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-40f, 110f), new Vector2(1000f, 30f));
        }

        static Text Figure(RectTransform parent, string label)
        {
            var cell = UiFactory.Rect($"Figure {label}", parent);
            UiFactory.Column(cell, 2f);
            UiFactory.Size(UiFactory.Text(cell, label, 16, UiTheme.TextDim, TextAnchor.MiddleLeft, FontStyle.Bold), height: 22f);
            var value = UiFactory.Text(cell, "", 30, UiTheme.Text, TextAnchor.MiddleLeft, FontStyle.Bold, "Value");
            value.supportRichText = true;
            UiFactory.Size(value, height: 44f);
            return value;
        }

        // ---------- Refresh ----------

        void RefreshCar()
        {
            var catalog = GameSession.Catalog;
            var car = GameSession.SelectedCar;
            var setup = GameSession.SelectedSetup;
            var race = GameSession.SelectedRace;
            var stats = Stats(car, setup.tuning);

            carName.text = car.displayName.ToUpperInvariant();
            carCount.text = $"CAR {catalog.IndexOf(car) + 1} / {catalog.cars.Length}";
            carDescription.text = car.description;
            raceSummary.text = $"Race: <b>{race.displayName}</b> · {race.Summary}";
            footer.text = $"{car.displayName} · {VehicleTuning.Title(setup.tuning)}\nKeyboard / Controller / Touch";

            RefreshPaints(car, setup.paint);
            RefreshWheels(car, setup.wheels);
            foreach (var pair in tuningLabels)
                pair.Value.text = (pair.Key == setup.tuning ? "• " : "") + VehicleTuning.Title(pair.Key);
            tuningDescription.text = VehicleTuning.Describe(setup.tuning);
            statBars[0].fillAmount = stats.PowerRating;
            statBars[1].fillAmount = stats.TopSpeedRating;
            statBars[2].fillAmount = stats.GripRating;
            statBars[3].fillAmount = stats.AgilityRating;

            string[] specs =
            {
                $"{stats.PowerHp.ToString("N0", Numbers)} hp ({stats.powerKw.ToString("N0", Numbers)} kW) at {stats.powerRpm.ToString("N0", Numbers)} rpm",
                $"{stats.torqueNm.ToString("N0", Numbers)} N·m at {stats.torqueRpm.ToString("N0", Numbers)} rpm",
                $"{stats.zeroTo100Seconds.ToString("0.0", Numbers)} s",
                $"{stats.topSpeedKmh.ToString("N0", Numbers)} km/h",
                $"{stats.massKg.ToString("N0", Numbers)} kg",
                $"{stats.PowerToWeight.ToString("N0", Numbers)} hp / tonne",
                stats.DriveLabel,
                $"{stats.gears}-speed sequential, auto or manual"
            };
            for (int i = 0; i < specs.Length; i++)
                specValues[i].text = specs[i];

            cardName.text = car.displayName.ToUpperInvariant();
            cardClass.text = $"{stats.DriveLabel} · {car.PaintName(setup.paint)} · {WheelName(car, setup.wheels)} wheels";
            cardPower.text = $"{stats.PowerHp.ToString("N0", Numbers)}<size=20> hp</size>";
            cardSprint.text = $"{stats.zeroTo100Seconds.ToString("0.0", Numbers)}<size=20> s</size>";
            cardTopSpeed.text = $"{stats.topSpeedKmh.ToString("N0", Numbers)}<size=20> km/h</size>";
            cardWeight.text = $"{stats.massKg.ToString("N0", Numbers)}<size=20> kg</size>";

            if (showroom != null)
                showroom.Show(car, setup.paint, setup.wheels, setup.tuning);
        }

        static CarStats Stats(CarDefinition car, TuningPreset tuning)
        {
            var tuned = VehicleTuning.CreateTuned(car.settings, tuning);
            var stats = CarStats.From(tuned);
            Destroy(tuned);
            return stats;
        }

        static string WheelName(CarDefinition car, int design)
        {
            var designs = car.visualKit != null ? car.visualKit.wheelDesigns : null;
            return designs == null || designs.Length == 0 ? "Standard wheels" : designs[Mathf.Abs(design) % designs.Length].displayName;
        }

        void RefreshPaints(CarDefinition car, int selected)
        {
            paintName.text = $"PAINT · <color=#F5F5F5>{car.PaintName(selected).ToUpperInvariant()}</color>";
            // Swatches depend on the car's paint list, so rebuild when the car changes.
            if (paintButtons.Count == 0 || paintButtons[0].car != car)
            {
                foreach (var (button, _, _) in paintButtons)
                    Destroy(button.gameObject);
                paintButtons.Clear();
                for (int i = 0; i < car.paints.Length; i++)
                {
                    int paint = i;
                    // The button is a frame (it lights up when hovered or selected); the paint is a chip inside it.
                    var swatch = UiFactory.Button(paintGrid, "", () => SetPaint(paint), 52, 68f, $"Paint {i}");
                    var colors = swatch.colors;
                    colors.normalColor = new Color(1f, 1f, 1f, 0.16f);
                    swatch.colors = colors;
                    var chip = UiFactory.Image(swatch.transform, "Chip", car.Paint(i), UiFactory.White);
                    chip.rectTransform.Stretch(4f);
                    chip.transform.SetAsFirstSibling(); // under the selection mark
                    paintButtons.Add((swatch, car, i));
                }
            }
            foreach (var (button, _, paint) in paintButtons)
            {
                var mark = button.GetComponentInChildren<Text>();
                mark.text = paint == selected ? "•" : "";
                mark.color = car.Paint(paint).grayscale > 0.6f ? Color.black : Color.white;
            }
        }

        void RefreshWheels(CarDefinition car, int selected)
        {
            var designs = car.visualKit != null ? car.visualKit.wheelDesigns : System.Array.Empty<WheelDesign>();
            if (wheelLabels.Count != designs.Length)
            {
                foreach (Transform child in wheelList)
                    Destroy(child.gameObject);
                wheelLabels.Clear();
                for (int i = 0; i < designs.Length; i++)
                {
                    int design = i;
                    var button = UiFactory.Button(wheelList, "", () => SetWheels(design), 22, 62f, $"Wheels {i}");
                    var label = button.GetComponentInChildren<Text>();
                    label.alignment = TextAnchor.MiddleLeft;
                    label.supportRichText = true;
                    wheelLabels.Add((label, i));
                }
            }
            foreach (var (label, design) in wheelLabels)
            {
                string mark = design == Mathf.Abs(selected) % designs.Length ? "•  " : "    ";
                label.text = $"{mark}<size=26><b>{designs[design].displayName.ToUpperInvariant()}</b></size>   <color=#A8ADB9>{designs[design].finishName}</color>";
            }
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
