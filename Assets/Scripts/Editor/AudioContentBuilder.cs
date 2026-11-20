using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using RealisticCarRacing.Audio;
using UnityEditor;
using UnityEngine;
using UnityEngine.Audio;

namespace RealisticCarRacing.EditorTools
{
    /// <summary>
    /// Creates the audio setup: the mixer (Master → Engine, Transmission, Tires, Environment, UI, Music, each with an
    /// exposed volume), the <see cref="GameAudio"/> asset in Resources, and the default <see cref="VehicleAudioProfile"/>.
    /// Clips are picked up from Assets/Audio by file name (see <see cref="Slots"/>); a slot with no matching clip stays
    /// empty and that sound is silent. Re-running keeps existing assets and only fills empty slots.
    /// <para>
    /// Unity has no public API for building mixers, so this uses the editor's own (internal) mixer controller. If a
    /// Unity update renames it, the build stops with an error explaining how to make the mixer by hand.
    /// </para>
    /// </summary>
    public static class AudioContentBuilder
    {
        public const string MixerPath = "Assets/Audio/Mixer/GameMixer.mixer";
        const string GameAudioPath = "Assets/Resources/Audio/GameAudio.asset";
        const string ProfilePath = "Assets/Settings/Audio/DefaultVehicleAudio.asset";
        const string AudioRoot = "Assets/Audio";

        [MenuItem("RealisticCarRacing/Create Audio Content")]
        public static GameAudio Build()
        {
            foreach (string folder in new[] { "Assets/Audio/Mixer", "Assets/Resources/Audio", "Assets/Settings/Audio",
                         "Assets/Audio/Engine", "Assets/Audio/Loops", "Assets/Audio/SFX", "Assets/Audio/UI", "Assets/Audio/Music" })
                SceneBuildUtility.EnsureFolder(folder);

            var mixer = AssetDatabase.LoadAssetAtPath<AudioMixer>(MixerPath);
            if (mixer == null)
                mixer = CreateMixer();

            var gameAudio = LoadOrCreate<GameAudio>(GameAudioPath);
            gameAudio.mixer = mixer;
            gameAudio.engine = FindGroup(mixer, nameof(AudioCategory.Engine));
            gameAudio.transmission = FindGroup(mixer, nameof(AudioCategory.Transmission));
            gameAudio.tires = FindGroup(mixer, nameof(AudioCategory.Tires));
            gameAudio.environment = FindGroup(mixer, nameof(AudioCategory.Environment));
            gameAudio.ui = FindGroup(mixer, nameof(AudioCategory.UI));
            gameAudio.music = FindGroup(mixer, nameof(AudioCategory.Music));

            var profile = LoadOrCreate<VehicleAudioProfile>(ProfilePath);
            AssignClips(profile, gameAudio);
            gameAudio.defaultVehicle = profile;
            EditorUtility.SetDirty(profile);
            EditorUtility.SetDirty(gameAudio);
            AssetDatabase.SaveAssets();
            return gameAudio;
        }

        static T LoadOrCreate<T>(string path) where T : ScriptableObject
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset != null)
                return asset;
            asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }

        static AudioMixerGroup FindGroup(AudioMixer mixer, string name) =>
            mixer.FindMatchingGroups("Master").FirstOrDefault(g => g.name == name);

        // ---------- Mixer (internal editor API) ----------

        static AudioMixer CreateMixer()
        {
            var editor = typeof(Editor).Assembly;
            var controllerType = editor.GetType("UnityEditor.Audio.AudioMixerController", true);
            var groupType = editor.GetType("UnityEditor.Audio.AudioMixerGroupController", true);
            var pathType = editor.GetType("UnityEditor.Audio.AudioGroupParameterPath", true);
            var exposedType = editor.GetType("UnityEditor.Audio.ExposedAudioParameter", true);
            const BindingFlags any = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;

            try
            {
                var controller = controllerType.GetMethod("CreateMixerControllerAtPath", any).Invoke(null, new object[] { MixerPath });
                var master = controllerType.GetProperty("masterGroup", any).GetValue(controller);
                var createGroup = controllerType.GetMethod("CreateNewGroup", any);
                var addChild = controllerType.GetMethod("AddChildToParent", any);
                var addExposed = controllerType.GetMethods(any).First(m => m.Name == "AddExposedParameter" && m.GetParameters().Length == 1);
                var getVolumeGuid = groupType.GetMethod("GetGUIDForVolume", any);
                var exposedProperty = controllerType.GetProperty("exposedParameters", any);

                var exposed = new List<(object guid, string name)>();
                void Expose(object group, string parameter)
                {
                    object guid = getVolumeGuid.Invoke(group, null);
                    addExposed.Invoke(controller, new[] { Activator.CreateInstance(pathType, group, guid) });
                    exposed.Add((guid, parameter));
                }

                Expose(master, AudioSettingsService.MasterParameter);
                foreach (AudioCategory category in Enum.GetValues(typeof(AudioCategory)))
                {
                    var group = createGroup.Invoke(controller, new object[] { category.ToString(), false });
                    addChild.Invoke(controller, new[] { group, master });
                    Expose(group, AudioSettingsService.Parameter(category));
                }

                // Give the exposed parameters their names (they are created as "MyExposedParam N").
                var current = (Array)exposedProperty.GetValue(controller);
                var guidField = exposedType.GetField("guid", any);
                var nameField = exposedType.GetField("name", any);
                for (int i = 0; i < current.Length; i++)
                {
                    object item = current.GetValue(i);
                    object guid = guidField.GetValue(item);
                    var match = exposed.FirstOrDefault(e => e.guid.Equals(guid));
                    if (match.name == null)
                        continue;
                    nameField.SetValue(item, match.name);
                    current.SetValue(item, i);
                }
                exposedProperty.SetValue(controller, current);
                EditorUtility.SetDirty((UnityEngine.Object)controller);
                AssetDatabase.SaveAssets();
                return (AudioMixer)controller;
            }
            catch (Exception exception)
            {
                throw new InvalidOperationException(
                    "Couldn't create the audio mixer with this Unity version's editor API. Create it by hand: Assets > Create > " +
                    $"Audio Mixer at {MixerPath}; add child groups Engine, Transmission, Tires, Environment, UI and Music under Master; " +
                    "expose each group's Volume (right-click > Expose) and name them MasterVolume, EngineVolume, TransmissionVolume, " +
                    "TiresVolume, EnvironmentVolume, UIVolume and MusicVolume; then run Create Audio Content again.", exception);
            }
        }

        // ---------- Clips ----------

        /// <summary>
        /// File-name conventions for picking up recordings (case-insensitive, anywhere under Assets/Audio):
        /// engine loops <c>engine_on_3000</c> / <c>engine_off_3000</c> (on-throttle / coasting, recorded RPM),
        /// and for every other slot a file whose name starts with the slot key, e.g. <c>tyre_squeal</c>,
        /// <c>impact_light_01</c>, <c>ui_click</c>.
        /// </summary>
        static readonly string[] Slots =
        {
            "engine_startup", "engine_shutdown", "exhaust_pop", "gear_up", "gear_down", "gear_engage", "tyre_squeal",
            "tyre_lockup", "tyre_drift", "brake_loop", "surface_asphalt", "surface_kerb", "surface_grass", "surface_gravel",
            "wind_loop", "horn_loop", "scrape_loop", "impact_light", "impact_heavy", "ui_hover", "ui_click", "ui_back",
            "countdown_beep", "countdown_go", "lap_complete", "race_finish", "music_menu"
        };

        static void AssignClips(VehicleAudioProfile profile, GameAudio gameAudio)
        {
            var clips = AssetDatabase.FindAssets("t:AudioClip", new[] { AudioRoot })
                .Select(guid => AssetDatabase.LoadAssetAtPath<AudioClip>(AssetDatabase.GUIDToAssetPath(guid)))
                .Where(clip => clip != null)
                .OrderBy(clip => clip.name)
                .ToList();
            AudioClip One(string key) => clips.FirstOrDefault(c => c.name.StartsWith(key, StringComparison.OrdinalIgnoreCase));
            AudioClip[] All(string key) => clips.Where(c => c.name.StartsWith(key, StringComparison.OrdinalIgnoreCase)).ToArray();
            T Keep<T>(T existing, T found) where T : class => existing ?? found;

            if (profile.engineLayers.Length == 0)
            {
                var layers = new List<VehicleAudioProfile.EngineLayer>();
                foreach (var clip in clips)
                {
                    var parts = clip.name.ToLowerInvariant().Split('_');
                    if (parts.Length >= 3 && parts[0] == "engine" && (parts[1] == "on" || parts[1] == "off") && float.TryParse(parts[2], out float rpm))
                        layers.Add(new VehicleAudioProfile.EngineLayer { clip = clip, rpm = rpm, load = parts[1] == "on" ? 1f : 0f });
                }
                profile.engineLayers = layers.OrderBy(l => l.load).ThenBy(l => l.rpm).ToArray();
            }
            profile.startup = Keep(profile.startup, One("engine_startup"));
            profile.shutdown = Keep(profile.shutdown, One("engine_shutdown"));
            if (profile.exhaustPops.Length == 0) profile.exhaustPops = All("exhaust_pop");
            profile.gearUp = Keep(profile.gearUp, One("gear_up"));
            profile.gearDown = Keep(profile.gearDown, One("gear_down"));
            profile.gearEngage = Keep(profile.gearEngage, One("gear_engage"));
            profile.tyreSqueal = Keep(profile.tyreSqueal, One("tyre_squeal"));
            profile.tyreLockup = Keep(profile.tyreLockup, One("tyre_lockup"));
            profile.drift = Keep(profile.drift, One("tyre_drift"));
            profile.brake = Keep(profile.brake, One("brake_loop"));
            profile.asphalt = Keep(profile.asphalt, One("surface_asphalt"));
            profile.kerb = Keep(profile.kerb, One("surface_kerb"));
            profile.grass = Keep(profile.grass, One("surface_grass"));
            profile.gravel = Keep(profile.gravel, One("surface_gravel"));
            profile.wind = Keep(profile.wind, One("wind_loop"));
            profile.horn = Keep(profile.horn, One("horn_loop"));
            profile.scrape = Keep(profile.scrape, One("scrape_loop"));
            if (profile.lightImpacts.Length == 0) profile.lightImpacts = All("impact_light");
            if (profile.heavyImpacts.Length == 0) profile.heavyImpacts = All("impact_heavy");

            gameAudio.hover = Keep(gameAudio.hover, One("ui_hover"));
            gameAudio.click = Keep(gameAudio.click, One("ui_click"));
            gameAudio.back = Keep(gameAudio.back, One("ui_back"));
            gameAudio.countdownBeep = Keep(gameAudio.countdownBeep, One("countdown_beep"));
            gameAudio.countdownGo = Keep(gameAudio.countdownGo, One("countdown_go"));
            gameAudio.lapComplete = Keep(gameAudio.lapComplete, One("lap_complete"));
            gameAudio.raceFinish = Keep(gameAudio.raceFinish, One("race_finish"));
            gameAudio.menuMusic = Keep(gameAudio.menuMusic, One("music_menu"));

            int filled = Slots.Count(slot => One(slot) != null);
            Debug.Log($"RealisticCarRacing audio: {profile.engineLayers.Length} engine loops, {filled}/{Slots.Length} other sound slots have recordings.");
        }
    }
}
