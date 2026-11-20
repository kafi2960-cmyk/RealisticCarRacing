using System;
using System.Collections.Generic;
using RealisticCarRacing.Vehicles;
using UnityEngine;

namespace RealisticCarRacing.Managers
{
    /// <summary>Where the profile is saved. Swappable so tests never touch the player's real save.</summary>
    public interface IProfileStorage
    {
        string Read(string key);
        void Write(string key, string value);
    }

    public sealed class PlayerPrefsStorage : IProfileStorage
    {
        public string Read(string key) => PlayerPrefs.GetString(key, string.Empty);

        public void Write(string key, string value)
        {
            PlayerPrefs.SetString(key, value);
            PlayerPrefs.Save();
        }
    }

    public sealed class MemoryStorage : IProfileStorage
    {
        readonly Dictionary<string, string> values = new Dictionary<string, string>();
        public string Read(string key) => values.TryGetValue(key, out var value) ? value : string.Empty;
        public void Write(string key, string value) => values[key] = value;
    }

    [Serializable]
    public sealed class CarSetup
    {
        public string carId;
        public int paint;
        /// <summary>Index into the car's wheel designs (0 = factory wheels; visual only).</summary>
        public int wheels;
        public TuningPreset tuning;
    }

    [Serializable]
    public sealed class RaceRecord
    {
        public string raceId;
        /// <summary>Seconds; 0 = no time set yet.</summary>
        public float bestLap;
        public float bestRaceTime;
        /// <summary>1-based; 0 = never finished.</summary>
        public int bestPosition;
        public int starts;
        public int wins;
    }

    /// <summary>What happened to the records when a result was submitted.</summary>
    public readonly struct RecordUpdate
    {
        public readonly bool newBestLap;
        public readonly bool newBestRaceTime;
        public readonly bool newBestPosition;

        public RecordUpdate(bool newBestLap, bool newBestRaceTime, bool newBestPosition)
        {
            this.newBestLap = newBestLap;
            this.newBestRaceTime = newBestRaceTime;
            this.newBestPosition = newBestPosition;
        }
    }

    /// <summary>Graphics menu choices. -1 / 0 mean "not chosen yet / use the preset's default".</summary>
    [Serializable]
    public sealed class GraphicsPreferences
    {
        /// <summary>Quality level index (0 Low, 1 Medium, 2 High); -1 until the device has been assessed.</summary>
        public int preset = -1;
        /// <summary>Render scale override (0.5..1); 0 = use the preset's own scale.</summary>
        public float renderScale;
        /// <summary>Frame-rate cap; 0 = preset default, -1 = unlimited.</summary>
        public int frameRateLimit;
        public bool showPerformance;
    }

    /// <summary>Audio menu choices: 0..1 linear volumes (master and one per mixer group) and mute.</summary>
    [Serializable]
    public sealed class AudioPreferences
    {
        public float master = 1f;
        public float engine = 1f;
        public float transmission = 1f;
        public float tires = 1f;
        public float environment = 1f;
        public float ui = 0.8f;
        public float music = 0.6f;
        public bool muted;

        public float Get(Audio.AudioCategory category) => category switch
        {
            Audio.AudioCategory.Engine => engine,
            Audio.AudioCategory.Transmission => transmission,
            Audio.AudioCategory.Tires => tires,
            Audio.AudioCategory.Environment => environment,
            Audio.AudioCategory.UI => ui,
            _ => music
        };

        public void Set(Audio.AudioCategory category, float value)
        {
            switch (category)
            {
                case Audio.AudioCategory.Engine: engine = value; break;
                case Audio.AudioCategory.Transmission: transmission = value; break;
                case Audio.AudioCategory.Tires: tires = value; break;
                case Audio.AudioCategory.Environment: environment = value; break;
                case Audio.AudioCategory.UI: ui = value; break;
                default: music = value; break;
            }
        }
    }

    /// <summary>The player's saved selections, garage setups, graphics choices and personal records.</summary>
    [Serializable]
    public sealed class PlayerProfile
    {
        public string selectedCarId = string.Empty;
        public string selectedRaceId = string.Empty;
        public GraphicsPreferences graphics = new GraphicsPreferences();
        public AudioPreferences audio = new AudioPreferences();
        public List<CarSetup> carSetups = new List<CarSetup>();
        public List<RaceRecord> records = new List<RaceRecord>();

        public CarSetup SetupFor(string carId)
        {
            foreach (var setup in carSetups)
            {
                if (setup.carId == carId)
                    return setup;
            }
            var created = new CarSetup { carId = carId };
            carSetups.Add(created);
            return created;
        }

        public RaceRecord RecordFor(string raceId)
        {
            foreach (var record in records)
            {
                if (record.raceId == raceId)
                    return record;
            }
            return null;
        }

        public RecordUpdate SubmitResult(string raceId, int position, float raceTime, float bestLap)
        {
            var record = RecordFor(raceId);
            if (record == null)
            {
                record = new RaceRecord { raceId = raceId };
                records.Add(record);
            }

            record.starts++;
            if (position == 1)
                record.wins++;
            bool lap = bestLap > 0f && (record.bestLap <= 0f || bestLap < record.bestLap);
            bool time = raceTime > 0f && (record.bestRaceTime <= 0f || raceTime < record.bestRaceTime);
            bool place = position > 0 && (record.bestPosition <= 0 || position < record.bestPosition);
            if (lap) record.bestLap = bestLap;
            if (time) record.bestRaceTime = raceTime;
            if (place) record.bestPosition = position;
            return new RecordUpdate(lap, time, place);
        }
    }

    public static class ProfileStore
    {
        public const string Key = "RealisticCarRacing.Profile.v1";

        public static PlayerProfile Load(IProfileStorage storage)
        {
            string json = storage.Read(Key);
            if (string.IsNullOrEmpty(json))
                return new PlayerProfile();
            try
            {
                return JsonUtility.FromJson<PlayerProfile>(json) ?? new PlayerProfile();
            }
            catch (ArgumentException)
            {
                Debug.LogWarning("RealisticCarRacing: saved profile was unreadable and has been reset.");
                return new PlayerProfile();
            }
        }

        public static void Save(IProfileStorage storage, PlayerProfile profile) => storage.Write(Key, JsonUtility.ToJson(profile));
    }
}
