using System;
using RealisticCarRacing.EditorTools.Circuit;
using RealisticCarRacing.Managers;
using RealisticCarRacing.Racing;
using RealisticCarRacing.Vehicles;
using UnityEditor;
using UnityEngine;

namespace RealisticCarRacing.EditorTools
{
    /// <summary>
    /// Creates the playable content: three cars (settings + definitions), four race events and the GameCatalog
    /// in Resources. Assets that already exist are left as they are, so hand tuning survives re-runs.
    /// </summary>
    public static class GameContentBuilder
    {
        const string CarsFolder = "Assets/Settings/Cars";
        const string RacesFolder = "Assets/Settings/Races";
        const string CatalogPath = "Assets/Resources/GameCatalog.asset";

        static readonly string[] OpponentNames =
        {
            "A. Varga", "K. Mendes", "L. Okafor", "J. Lindqvist", "R. Takeda", "S. Novak",
            "D. Moreau", "T. Haddad", "E. Kowalczyk", "N. Silva", "P. Ivanova"
        };

        [MenuItem("RealisticCarRacing/Create Game Content (Cars, Races)")]
        public static GameCatalog Build()
        {
            var materials = VehicleTestSceneBuilder.LoadOrCreateCarMaterials();
            var cars = new[]
            {
                Car("velocity_gt", "Velocity GT",
                    "Rear-wheel-drive grand tourer. Balanced and predictable: the reference car.",
                    Settings("VelocityGT", _ => { }),
                    Palette(0.75f, 0.1f, 0.08f), materials),
                Car("apex_rs", "Apex RS",
                    "All-wheel-drive turbo. The most power and traction, but also the heaviest.",
                    Settings("ApexRS", s =>
                    {
                        s.driveType = DriveType.AllWheelDrive;
                        s.mass = 1490f;
                        ScaleTorque(s, 1.22f);
                        s.finalDriveRatio = 3.7f;
                        s.sidewaysFriction.stiffness = 1.6f;
                        s.forwardFriction.stiffness = 1.7f;
                        s.downforceCoefficient = 1.5f;
                        s.maxBrakeTorque = 10500f;
                    }),
                    Palette(0.1f, 0.25f, 0.7f), materials),
                Car("strada_sport", "Strada Sport",
                    "Light front-wheel-drive hot hatch. Less power, but nimble in the slow corners.",
                    Settings("StradaSport", s =>
                    {
                        s.driveType = DriveType.FrontWheelDrive;
                        s.mass = 1180f;
                        ScaleTorque(s, 0.82f);
                        s.finalDriveRatio = 4.1f;
                        s.sidewaysFriction.stiffness = 1.45f;
                        s.frontBrakeBias = 0.7f;
                        s.dragCoefficient = 0.34f;
                    }),
                    Palette(0.95f, 0.75f, 0.1f), materials)
            };

            var races = new[]
            {
                Race("sprint", "Sprint", "Two quick laps against a small field of rookies.", 2, 3, AIDifficulty.Easy),
                Race("grand_prix", "Grand Prix", "The main event: three laps against five rivals.", 3, 5, AIDifficulty.Medium),
                Race("championship", "Championship Final", "Five laps on a full grid of the fastest drivers.", 5, 9, AIDifficulty.Hard),
                Race("time_trial", "Time Trial", "Just you and the clock. Set your best lap.", 3, 0, AIDifficulty.Medium)
            };

            var catalog = LoadOrCreate<GameCatalog>(CatalogPath, out _);
            catalog.cars = cars;
            catalog.races = races;
            catalog.opponentNames = OpponentNames;
            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();
            return catalog;
        }

        static T LoadOrCreate<T>(string path, out bool created) where T : ScriptableObject
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            created = asset == null;
            if (!created)
                return asset;
            SceneBuildUtility.EnsureFolder(System.IO.Path.GetDirectoryName(path)?.Replace('\\', '/'));
            asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }

        static VehicleSettings Settings(string name, Action<VehicleSettings> configure)
        {
            var settings = LoadOrCreate<VehicleSettings>($"{CarsFolder}/{name}_Settings.asset", out bool created);
            if (created)
            {
                configure(settings);
                EditorUtility.SetDirty(settings);
            }
            return settings;
        }

        static CarDefinition Car(string id, string displayName, string description, VehicleSettings settings, Color[] paints,
            TestVehicleFactory.Materials materials)
        {
            var car = LoadOrCreate<CarDefinition>($"{CarsFolder}/{displayName.Replace(" ", "")}.asset", out bool created);
            if (created)
            {
                car.id = id;
                car.displayName = displayName;
                car.description = description;
                car.paints = paints;
            }
            car.settings = settings;
            car.bodyMaterial = materials.body;
            car.cabinMaterial = materials.cabin;
            car.tyreMaterial = materials.tyre;
            car.markerMaterial = materials.marker;
            EditorUtility.SetDirty(car);
            return car;
        }

        static RaceDefinition Race(string id, string displayName, string description, int laps, int opponents, AIDifficulty difficulty)
        {
            var race = LoadOrCreate<RaceDefinition>($"{RacesFolder}/{displayName.Replace(" ", "")}.asset", out bool created);
            if (created)
            {
                race.id = id;
                race.displayName = displayName;
                race.description = description;
                race.laps = laps;
                race.opponents = opponents;
                race.difficulty = difficulty;
                race.sceneName = System.IO.Path.GetFileNameWithoutExtension(CircuitSceneBuilder.ScenePath);
                race.trackName = "Grand Prix Circuit";
                EditorUtility.SetDirty(race);
            }
            return race;
        }

        /// <summary>The car's signature colour first, then a common set of paints.</summary>
        static Color[] Palette(float r, float g, float b) => new[]
        {
            new Color(r, g, b),
            new Color(0.92f, 0.92f, 0.9f), new Color(0.06f, 0.06f, 0.07f), new Color(0.55f, 0.57f, 0.6f),
            new Color(0.1f, 0.45f, 0.25f), new Color(0.95f, 0.45f, 0.05f), new Color(0.45f, 0.1f, 0.55f), new Color(0.1f, 0.6f, 0.75f)
        };

        static void ScaleTorque(VehicleSettings settings, float factor)
        {
            var keys = settings.torqueCurve.keys;
            for (int i = 0; i < keys.Length; i++)
                keys[i].value *= factor;
            settings.torqueCurve = new AnimationCurve(keys);
        }

        /// <summary>Regenerates everything: project settings, test track, content, circuit and main menu.</summary>
        [MenuItem("RealisticCarRacing/Build All Game Content")]
        public static void BuildAll()
        {
            ProjectSetup.Apply();
            VehicleTestSceneBuilder.Build();
            Build();
            CircuitSceneBuilder.Build();
            MenuSceneBuilder.Build();
        }
    }
}
