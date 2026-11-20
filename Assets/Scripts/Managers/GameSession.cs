using RealisticCarRacing.Racing;
using RealisticCarRacing.Vehicles;
using UnityEngine;

namespace RealisticCarRacing.Managers
{
    /// <summary>
    /// State shared across scenes: the catalog, the saved profile and the race about to be run.
    /// Selections fall back to the first catalog entry, so any scene can be played directly from the editor.
    /// </summary>
    public static class GameSession
    {
        static GameCatalog catalog;
        static PlayerProfile profile;

        public static IProfileStorage Storage { get; private set; } = new PlayerPrefsStorage();

        public static GameCatalog Catalog
        {
            get
            {
                if (catalog == null)
                    catalog = Resources.Load<GameCatalog>(GameCatalog.ResourcePath);
                return catalog;
            }
            set => catalog = value;
        }

        public static PlayerProfile Profile => profile ??= ProfileStore.Load(Storage);

        /// <summary>Race chosen for the next race scene load. Overrides the profile's selection (used by restart and tests).</summary>
        public static RaceDefinition PendingRace { get; set; }

        public static CarDefinition SelectedCar
        {
            get
            {
                var car = Catalog.FindCar(Profile.selectedCarId);
                return car != null ? car : Catalog.cars[0];
            }
        }

        public static CarSetup SelectedSetup => Profile.SetupFor(SelectedCar.id);

        public static RaceDefinition SelectedRace
        {
            get
            {
                if (PendingRace != null)
                    return PendingRace;
                var race = Catalog.FindRace(Profile.selectedRaceId);
                return race != null ? race : Catalog.races[0];
            }
        }

        public static void SelectCar(CarDefinition car)
        {
            Profile.selectedCarId = car.id;
            Save();
        }

        public static void SelectRace(RaceDefinition race)
        {
            Profile.selectedRaceId = race.id;
            PendingRace = null;
            Save();
        }

        public static void Save() => ProfileStore.Save(Storage, Profile);

        /// <summary>Switches save storage (e.g. to <see cref="MemoryStorage"/> in tests) and clears session state.</summary>
        public static void UseStorage(IProfileStorage storage)
        {
            Storage = storage;
            profile = null;
            PendingRace = null;
        }
    }
}
