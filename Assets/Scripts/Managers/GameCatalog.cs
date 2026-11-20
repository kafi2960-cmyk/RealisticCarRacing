using System;
using RealisticCarRacing.Racing;
using RealisticCarRacing.Vehicles;
using UnityEngine;

namespace RealisticCarRacing.Managers
{
    /// <summary>Everything the menus offer: cars, races and AI driver names. Lives at Resources/GameCatalog.</summary>
    [CreateAssetMenu(fileName = "GameCatalog", menuName = "RealisticCarRacing/Game Catalog")]
    public sealed class GameCatalog : ScriptableObject
    {
        public const string ResourcePath = "GameCatalog";

        public CarDefinition[] cars = Array.Empty<CarDefinition>();
        public RaceDefinition[] races = Array.Empty<RaceDefinition>();
        [Tooltip("Names given to AI drivers, in grid order.")]
        public string[] opponentNames = Array.Empty<string>();

        public CarDefinition FindCar(string id)
        {
            foreach (var car in cars)
            {
                if (car != null && car.id == id)
                    return car;
            }
            return null;
        }

        public RaceDefinition FindRace(string id)
        {
            foreach (var race in races)
            {
                if (race != null && race.id == id)
                    return race;
            }
            return null;
        }

        public int IndexOf(CarDefinition car) => Array.IndexOf(cars, car);
        public int IndexOf(RaceDefinition race) => Array.IndexOf(races, race);
    }
}
