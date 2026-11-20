using System.Collections;
using System.Collections.Generic;
using RealisticCarRacing.Controls;
using RealisticCarRacing.Racing;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace RealisticCarRacing.UI
{
    /// <summary>
    /// Pause menu, race-completion screen and results table. Pause with Esc, the gamepad Start button or the
    /// on-screen pause button; Back (gamepad B) resumes. Every panel selects its first button so it can be
    /// driven by keyboard and gamepad as well as mouse and touch.
    /// </summary>
    public sealed class RaceMenus : MonoBehaviour
    {
        const float CompletionDelay = 1.5f;
        const float ResultsRefresh = 0.5f;

        [SerializeField] RaceManager race;

        readonly List<Text[]> resultRows = new List<Text[]>();
        MenuInputActions input;
        Text completionPosition, completionDetails, completionRecords, resultsStatus;
        Button resumeButton, resultsButton, resultsRestartButton;
        GraphicsSettingsPanel settings;
        AudioSettingsPanel audioSettings;
        float resultsTimer;

        public GameObject PausePanel { get; private set; }
        public GameObject SettingsPanel { get; private set; }
        public GameObject AudioPanel { get; private set; }
        public GameObject CompletionPanel { get; private set; }
        public GameObject ResultsPanel { get; private set; }

        public void Configure(RaceManager manager) => race = manager;

        void Awake()
        {
            input = new MenuInputActions();
            input.Pause.performed += _ => OnPausePressed();
            input.Back.performed += context =>
            {
                // Escape is already Pause; Back here is the gamepad "B" to leave the pause menu.
                if (context.control.device is Keyboard || !race.IsPaused)
                    return;
                if (SettingsPanel.activeSelf || AudioPanel.activeSelf)
                    HideSettings();
                else
                    race.SetPaused(false);
            };
        }

        void OnEnable() => input.Enable();

        void OnDisable() => input.Disable();

        void Start()
        {
            Build();
            race.PauseChanged += OnPauseChanged;
            race.RacerFinished += OnRacerFinished;
            race.ResultsFinalised += RefreshResults;
        }

        void OnDestroy()
        {
            input.Dispose();
            if (race == null)
                return;
            race.PauseChanged -= OnPauseChanged;
            race.RacerFinished -= OnRacerFinished;
            race.ResultsFinalised -= RefreshResults;
        }

        void OnPausePressed()
        {
            // Once the player has finished, the completion/results screens take over.
            if (race.State != RaceState.Finished)
                race.TogglePause();
        }

        void Build()
        {
            var canvas = UiFactory.Canvas("RaceMenus", 20);
            canvas.transform.SetParent(transform, false);

            PausePanel = Panel(canvas.transform, "PausePanel", new Vector2(640f, 760f), out var pauseBox);
            Title(pauseBox, "PAUSED", race.Race.displayName);
            resumeButton = UiFactory.Button(pauseBox, "RESUME", () => race.SetPaused(false));
            UiFactory.Button(pauseBox, "RESTART", race.Restart);
            UiFactory.Button(pauseBox, "SETTINGS", ShowSettings);
            UiFactory.Button(pauseBox, "AUDIO", ShowAudio);
            UiFactory.Button(pauseBox, "MAIN MENU", race.QuitToMenu);

            SettingsPanel = Panel(canvas.transform, "SettingsPanel", new Vector2(760f, 860f), out var settingsBox);
            Title(settingsBox, "GRAPHICS", "Changes apply immediately");
            settings = GraphicsSettingsPanel.Build(settingsBox, HideSettings);

            AudioPanel = Panel(canvas.transform, "AudioPanel", new Vector2(760f, 860f), out var audioBox);
            Title(audioBox, "AUDIO", "Changes apply immediately");
            audioSettings = AudioSettingsPanel.Build(audioBox, HideSettings);

            CompletionPanel = Panel(canvas.transform, "CompletionPanel", new Vector2(760f, 700f), out var completionBox);
            Title(completionBox, "FINISHED", race.Race.displayName);
            completionPosition = UiFactory.Text(completionBox, "", 120, UiTheme.Gold, TextAnchor.MiddleCenter, FontStyle.Bold, "Position");
            UiFactory.Size(completionPosition, height: 140f);
            completionDetails = UiFactory.Text(completionBox, "", 30, UiTheme.Text, TextAnchor.MiddleCenter, FontStyle.Normal, "Details");
            UiFactory.Size(completionDetails, height: 90f);
            completionRecords = UiFactory.Text(completionBox, "", 28, UiTheme.Good, TextAnchor.MiddleCenter, FontStyle.Bold, "Records");
            UiFactory.Size(completionRecords, height: 40f);
            resultsButton = UiFactory.Button(completionBox, "VIEW RESULTS", ShowResults);
            UiFactory.Button(completionBox, "RESTART", race.Restart);

            ResultsPanel = Panel(canvas.transform, "ResultsPanel", new Vector2(1320f, 900f), out var resultsBox);
            Title(resultsBox, "RESULTS", race.Race.displayName);
            resultsStatus = UiFactory.Text(resultsBox, "", 24, UiTheme.TextDim, TextAnchor.MiddleCenter, FontStyle.Italic, "Status");
            UiFactory.Size(resultsStatus, height: 34f);
            ResultRow(resultsBox, header: true);
            for (int i = 0; i < race.Racers.Count; i++)
                resultRows.Add(ResultRow(resultsBox, header: false));
            var buttons = UiFactory.Rect("Buttons", resultsBox);
            UiFactory.Row(buttons, 20f, TextAnchor.MiddleCenter).childForceExpandWidth = true;
            UiFactory.Size(buttons, height: 84f);
            resultsRestartButton = UiFactory.Button(buttons, "RESTART", race.Restart);
            UiFactory.Button(buttons, "NEXT RACE", () => Managers.SceneFlow.StartRace(race.NextRace()));
            UiFactory.Button(buttons, "MAIN MENU", race.QuitToMenu);

            PausePanel.SetActive(false);
            SettingsPanel.SetActive(false);
            AudioPanel.SetActive(false);
            CompletionPanel.SetActive(false);
            ResultsPanel.SetActive(false);
        }

        static GameObject Panel(Transform parent, string name, Vector2 size, out RectTransform box)
        {
            var dim = UiFactory.Image(parent, name, new Color(0f, 0f, 0f, 0.55f), UiFactory.White);
            dim.raycastTarget = true; // blocks the HUD and touch controls underneath
            dim.rectTransform.Stretch();
            var background = UiFactory.Image(dim.transform, "Box", UiTheme.Panel, UiFactory.White);
            box = background.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, size);
            UiFactory.Column(box, 16f, 40, TextAnchor.UpperCenter);
            var accent = UiFactory.Image(background.transform, "AccentBar", UiTheme.Accent, UiFactory.White);
            accent.rectTransform.Place(new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), Vector2.zero, new Vector2(0f, 8f));
            accent.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            return dim.gameObject;
        }

        static Text Title(Transform box, string title, string subtitle)
        {
            var text = UiFactory.Text(box, title, 64, UiTheme.Text, TextAnchor.MiddleCenter, FontStyle.Bold, "Title");
            UiFactory.Size(text, height: 80f);
            var sub = UiFactory.Text(box, subtitle.ToUpperInvariant(), 26, UiTheme.TextDim, TextAnchor.MiddleCenter, FontStyle.Bold, "Subtitle");
            UiFactory.Size(sub, height: 40f);
            return text;
        }

        static Text[] ResultRow(Transform box, bool header)
        {
            var row = UiFactory.Image(box, header ? "Header" : "Row", header ? Color.clear : UiTheme.PanelLight, UiFactory.White);
            UiFactory.Row(row.rectTransform, 10f);
            UiFactory.Size(row, height: header ? 36f : 44f);
            var color = header ? UiTheme.TextDim : UiTheme.Text;
            int size = header ? 20 : 26;
            var style = header ? FontStyle.Bold : FontStyle.Normal;
            var cells = new[]
            {
                (header ? "POS" : "", 90f, TextAnchor.MiddleCenter),
                (header ? "DRIVER" : "", 300f, TextAnchor.MiddleLeft),
                (header ? "CAR" : "", 300f, TextAnchor.MiddleLeft),
                (header ? "TIME / GAP" : "", 260f, TextAnchor.MiddleRight),
                (header ? "BEST LAP" : "", 200f, TextAnchor.MiddleRight)
            };
            var texts = new Text[cells.Length];
            for (int i = 0; i < cells.Length; i++)
            {
                texts[i] = UiFactory.Text(row.transform, cells[i].Item1, size, color, cells[i].Item3, style, $"Cell{i}");
                UiFactory.Size(texts[i], width: cells[i].Item2);
            }
            return texts;
        }

        void Update()
        {
            if (!ResultsPanel.activeSelf)
                return;
            resultsTimer -= Time.unscaledDeltaTime;
            if (resultsTimer <= 0f)
            {
                resultsTimer = ResultsRefresh;
                RefreshResults();
            }
        }

        void OnPauseChanged(bool paused)
        {
            PausePanel.SetActive(paused);
            SettingsPanel.SetActive(false);
            AudioPanel.SetActive(false);
            if (paused)
                Select(resumeButton);
        }

        void ShowSettings()
        {
            PausePanel.SetActive(false);
            SettingsPanel.SetActive(true);
            Select(settings.FirstSelectable);
        }

        void ShowAudio()
        {
            PausePanel.SetActive(false);
            AudioPanel.SetActive(true);
            Select(audioSettings.FirstSelectable);
        }

        void HideSettings()
        {
            SettingsPanel.SetActive(false);
            AudioPanel.SetActive(false);
            PausePanel.SetActive(race.IsPaused);
            if (race.IsPaused)
                Select(resumeButton);
        }

        void OnRacerFinished(Racer racer)
        {
            if (racer.IsPlayer)
                StartCoroutine(ShowCompletionAfterDelay());
        }

        IEnumerator ShowCompletionAfterDelay()
        {
            yield return new WaitForSecondsRealtime(CompletionDelay);
            var player = race.Player;
            completionPosition.text = Ordinal(player.Position);
            completionDetails.text = $"of {race.Racers.Count}\nRace time {RaceTimeFormat.Format(player.FinishTime)}   ·   Best lap {RaceTimeFormat.Format(player.BestLap)}";
            var records = race.PlayerRecords;
            var lines = new List<string>();
            if (records.newBestRaceTime) lines.Add("NEW RECORD TIME");
            if (records.newBestLap) lines.Add("NEW BEST LAP");
            completionRecords.text = string.Join("   ·   ", lines);
            PausePanel.SetActive(false);
            CompletionPanel.SetActive(true);
            Select(resultsButton);
        }

        public void ShowResults()
        {
            CompletionPanel.SetActive(false);
            ResultsPanel.SetActive(true);
            RefreshResults();
            Select(resultsRestartButton);
        }

        void RefreshResults()
        {
            if (ResultsPanel == null)
                return;
            var order = race.Standings;
            resultsStatus.text = race.ResultsFinal ? "Final classification" : "Provisional — cars still on track";
            for (int i = 0; i < resultRows.Count && i < order.Count; i++)
            {
                var racer = order[i];
                var cells = resultRows[i];
                cells[0].text = racer.Position.ToString();
                cells[1].text = racer.IsPlayer ? "YOU" : racer.Name;
                cells[2].text = racer.Car.displayName;
                cells[3].text = i == 0 ? (racer.Finished ? RaceTimeFormat.Format(racer.FinishTime) : "RUNNING") : RaceStandings.Gap(racer, order[0]);
                cells[4].text = RaceTimeFormat.Format(racer.BestLap);
                var color = racer.IsPlayer ? UiTheme.Gold : UiTheme.Text;
                foreach (var cell in cells)
                    cell.color = color;
            }
        }

        static void Select(Selectable selectable)
        {
            if (EventSystem.current != null)
                EventSystem.current.SetSelectedGameObject(selectable.gameObject);
        }

        static string Ordinal(int position)
        {
            string suffix = (position % 100) is >= 11 and <= 13 ? "th" : (position % 10) switch { 1 => "st", 2 => "nd", 3 => "rd", _ => "th" };
            return position + suffix;
        }
    }
}
