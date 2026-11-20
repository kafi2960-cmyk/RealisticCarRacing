using System.Collections.Generic;
using RealisticCarRacing.Racing;
using UnityEngine;
using UnityEngine.UI;

namespace RealisticCarRacing.UI
{
    /// <summary>
    /// In-race heads-up display: position, lap, race and lap timers, live standings with timing gaps,
    /// speedometer with RPM ring and gear, start countdown and race messages, plus an on-screen pause button.
    /// Layout keeps the bottom corners free for the touch driving controls.
    /// </summary>
    public sealed class RaceHud : MonoBehaviour
    {
        const float GaugeArc = 0.75f;          // the RPM ring covers 270°
        const float StandingsRefresh = 0.25f;  // seconds between standings redraws

        [SerializeField] RaceManager race;

        readonly List<(Image background, Text position, Text name, Text gap)> standingRows = new List<(Image, Text, Text, Text)>();
        // Pre-built speed strings: the speedometer changes every frame and would otherwise allocate.
        static readonly string[] SpeedLabels = BuildSpeedLabels(400);

        Racer player;
        int shownPosition = -1, shownLap = -1, shownSpeed = -1;
        float timerRefresh;
        float standingsTimer;
        float messageTimer;
        float goTimer;

        public Text PositionText { get; private set; }
        public Text LapText { get; private set; }
        public Text RaceTimeText { get; private set; }
        public Text LapTimesText { get; private set; }
        public Text SpeedText { get; private set; }
        public Text GearText { get; private set; }
        public Image RpmFill { get; private set; }
        public Text CountdownText { get; private set; }
        public Text MessageText { get; private set; }
        public Button PauseButton { get; private set; }

        public void Configure(RaceManager manager) => race = manager;

        static string[] BuildSpeedLabels(int count)
        {
            var labels = new string[count];
            for (int i = 0; i < count; i++)
                labels[i] = i.ToString();
            return labels;
        }

        void Start()
        {
            player = race.Player;
            Build();
            race.LapCompleted += OnLapCompleted;
            race.StateChanged += OnStateChanged;
        }

        void OnDestroy()
        {
            if (race == null)
                return;
            race.LapCompleted -= OnLapCompleted;
            race.StateChanged -= OnStateChanged;
        }

        void Build()
        {
            var canvas = UiFactory.Canvas("RaceHUD", 5);
            canvas.transform.SetParent(transform, false);
            var root = canvas.transform;
            var topLeft = new Vector2(0f, 1f);

            PauseButton = UiFactory.Button(root, "II", race.TogglePause, 40, name: "PauseButton");
            ((RectTransform)PauseButton.transform).Place(topLeft, topLeft, topLeft, new Vector2(30f, -30f), new Vector2(96f, 96f));

            PositionText = Readout(root, "POSITION", new Vector2(150f, -24f), 220f);
            LapText = Readout(root, "LAP", new Vector2(390f, -24f), 220f);

            // Timers, top centre.
            var timerBox = UiFactory.Rect("Timers", root).Place(new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -20f), new Vector2(560f, 150f));
            UiFactory.Image(timerBox, "Background", UiTheme.Panel, UiFactory.White).rectTransform.Stretch();
            RaceTimeText = UiFactory.Text(timerBox, "0:00.000", 58, UiTheme.Text, TextAnchor.UpperCenter, FontStyle.Bold, "RaceTime");
            RaceTimeText.rectTransform.offsetMax = new Vector2(0f, -12f);
            LapTimesText = UiFactory.Text(timerBox, "", 26, UiTheme.TextDim, TextAnchor.LowerCenter, FontStyle.Normal, "LapTimes");
            LapTimesText.rectTransform.offsetMin = new Vector2(0f, 14f);

            BuildStandings(root);
            BuildGauge(root);

            CountdownText = UiFactory.Text(root, "", 220, UiTheme.Text, TextAnchor.MiddleCenter, FontStyle.Bold, "Countdown");
            var countdownShadow = CountdownText.gameObject.AddComponent<Outline>();
            countdownShadow.effectColor = new Color(0f, 0f, 0f, 0.6f);
            countdownShadow.effectDistance = new Vector2(4f, -4f);
            MessageText = UiFactory.Text(root, "", 54, UiTheme.Gold, TextAnchor.MiddleCenter, FontStyle.Bold, "Message");
            MessageText.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 230f), new Vector2(1400f, 90f));
            MessageText.gameObject.AddComponent<Outline>().effectColor = new Color(0f, 0f, 0f, 0.7f);
        }

        Text Readout(Transform parent, string label, Vector2 position, float width)
        {
            var box = UiFactory.Rect(label, parent).Place(new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), position, new Vector2(width, 110f));
            UiFactory.Image(box, "Background", UiTheme.Panel, UiFactory.White).rectTransform.Stretch();
            var caption = UiFactory.Text(box, label, 20, UiTheme.TextDim, TextAnchor.UpperCenter, FontStyle.Bold, "Caption");
            caption.rectTransform.offsetMax = new Vector2(0f, -8f);
            var value = UiFactory.Text(box, "-", 54, UiTheme.Text, TextAnchor.LowerCenter, FontStyle.Bold, "Value");
            value.rectTransform.offsetMin = new Vector2(0f, 8f);
            return value;
        }

        void BuildStandings(Transform root)
        {
            var panel = UiFactory.Rect("Standings", root).Place(new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(30f, -150f), new Vector2(460f, 0f));
            UiFactory.Column(panel, 2f);
            var fitter = panel.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            foreach (var _ in race.Racers)
            {
                var background = UiFactory.Image(panel, "Row", UiTheme.Panel, UiFactory.White);
                UiFactory.Size(background, height: 36f);
                var position = UiFactory.Text(background.transform, "", 22, UiTheme.Text, TextAnchor.MiddleCenter, FontStyle.Bold, "Pos");
                position.rectTransform.Place(Vector2.zero, new Vector2(0f, 1f), new Vector2(0f, 0.5f), Vector2.zero, new Vector2(44f, 0f));
                var name = UiFactory.Text(background.transform, "", 22, UiTheme.Text, TextAnchor.MiddleLeft, FontStyle.Normal, "Name");
                name.rectTransform.offsetMin = new Vector2(54f, 0f);
                var gap = UiFactory.Text(background.transform, "", 22, UiTheme.TextDim, TextAnchor.MiddleRight, FontStyle.Normal, "Gap");
                gap.rectTransform.offsetMax = new Vector2(-12f, 0f);
                standingRows.Add((background, position, name, gap));
            }
        }

        void BuildGauge(Transform root)
        {
            var gauge = UiFactory.Rect("Speedometer", root).Place(new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 24f), new Vector2(300f, 300f));
            UiFactory.Image(gauge, "Backdrop", UiTheme.Panel, UiFactory.Disc).rectTransform.Stretch(-6f);
            var ringBack = UiFactory.Image(gauge, "RpmBack", new Color(1f, 1f, 1f, 0.15f), UiFactory.Ring);
            RpmFill = UiFactory.Image(gauge, "RpmFill", UiTheme.Text, UiFactory.Ring);
            foreach (var ring in new[] { ringBack, RpmFill })
            {
                ring.rectTransform.Stretch();
                ring.type = Image.Type.Filled;
                ring.fillMethod = Image.FillMethod.Radial360;
                ring.fillOrigin = (int)Image.Origin360.Bottom;
                ring.fillClockwise = true;
                // Rotate so the 90° gap sits centred at the bottom.
                ring.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -45f);
            }
            ringBack.fillAmount = GaugeArc;

            GearText = UiFactory.Text(gauge, "N", 56, UiTheme.Accent, TextAnchor.MiddleCenter, FontStyle.Bold, "Gear");
            GearText.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 70f), new Vector2(120f, 70f));
            SpeedText = UiFactory.Text(gauge, "0", 92, UiTheme.Text, TextAnchor.MiddleCenter, FontStyle.Bold, "Speed");
            SpeedText.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -8f), new Vector2(240f, 100f));
            var unit = UiFactory.Text(gauge, "KM/H", 22, UiTheme.TextDim, TextAnchor.MiddleCenter, FontStyle.Bold, "Unit");
            unit.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -66f), new Vector2(160f, 30f));
        }

        void Update()
        {
            if (player == null)
                return;
            var vehicle = player.Vehicle;
            int laps = race.Race.laps;

            // Text is only rebuilt when the value changes (and timers at 20 Hz), so the HUD generates almost no garbage.
            int position = player.Position, lap = Mathf.Clamp(player.CompletedLaps + 1, 1, laps);
            if (position != shownPosition)
                PositionText.text = $"{shownPosition = position}/{race.Racers.Count}";
            if (lap != shownLap)
                LapText.text = $"{shownLap = lap}/{laps}";
            timerRefresh -= Time.unscaledDeltaTime;
            if (timerRefresh <= 0f || player.Finished)
            {
                timerRefresh = 0.05f;
                RaceTimeText.text = RaceTimeFormat.Format(player.Finished ? player.FinishTime : race.RaceTime);
                float currentLap = race.State == RaceState.Racing ? race.RaceTime - player.LapStartTime : 0f;
                LapTimesText.text = $"LAP {RaceTimeFormat.Format(currentLap)}     BEST {RaceTimeFormat.Format(player.BestLap)}";
            }

            int speed = Mathf.RoundToInt(vehicle.SpeedKmh);
            if (speed != shownSpeed)
                SpeedText.text = SpeedLabels[Mathf.Clamp(shownSpeed = speed, 0, SpeedLabels.Length - 1)];
            if (!ReferenceEquals(vehicle.GearLabel, GearText.text))
                GearText.text = vehicle.GearLabel;
            float rpm = vehicle.EngineRpm / vehicle.Settings.redlineRpm;
            RpmFill.fillAmount = Mathf.Clamp01(rpm) * GaugeArc;
            RpmFill.color = rpm > 0.9f ? UiTheme.Accent : UiTheme.Text;

            UpdateCountdown();
            UpdateMessages();

            standingsTimer -= Time.unscaledDeltaTime;
            if (standingsTimer <= 0f)
            {
                standingsTimer = StandingsRefresh;
                UpdateStandings();
            }
        }

        void UpdateCountdown()
        {
            if (race.State == RaceState.Countdown)
            {
                CountdownText.text = Mathf.CeilToInt(race.CountdownRemaining).ToString();
                CountdownText.color = UiTheme.Text;
                return;
            }
            goTimer += Time.deltaTime;
            CountdownText.text = goTimer < 1f ? "GO!" : "";
            CountdownText.color = UiTheme.Good;
        }

        void UpdateMessages()
        {
            if (race.State == RaceState.Racing && player.Progress.WrongWaySeconds > 1f)
            {
                MessageText.text = "WRONG WAY";
                MessageText.color = UiTheme.Accent;
                messageTimer = 0.2f;
                return;
            }
            messageTimer -= Time.deltaTime;
            if (messageTimer <= 0f)
                MessageText.text = "";
        }

        void UpdateStandings()
        {
            var order = race.Standings;
            var leader = order[0];
            for (int i = 0; i < standingRows.Count && i < order.Count; i++)
            {
                var racer = order[i];
                var row = standingRows[i];
                row.position.text = racer.Position.ToString();
                row.name.text = racer.Name;
                row.gap.text = i == 0 ? (racer.Finished ? "FINISHED" : "") : RaceStandings.Gap(racer, leader);
                row.background.color = racer.IsPlayer ? new Color(UiTheme.Accent.r, UiTheme.Accent.g, UiTheme.Accent.b, 0.75f) : UiTheme.Panel;
            }
        }

        void ShowMessage(string text, Color color, float seconds)
        {
            MessageText.text = text;
            MessageText.color = color;
            messageTimer = seconds;
        }

        void OnLapCompleted(Racer racer, int laps, float lapTime, bool personalBest)
        {
            if (racer != player || player.Finished)
                return;
            if (laps == race.Race.laps - 1)
                ShowMessage("FINAL LAP", UiTheme.Gold, 2.5f);
            else if (personalBest && laps > 1)
                ShowMessage($"BEST LAP  {RaceTimeFormat.Format(lapTime)}", UiTheme.Good, 2.5f);
        }

        void OnStateChanged(RaceState state)
        {
            if (state == RaceState.Racing)
                goTimer = 0f;
        }
    }
}
