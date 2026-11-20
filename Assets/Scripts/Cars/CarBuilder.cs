using UnityEngine;

namespace RealisticCarRacing.Vehicles
{
    /// <summary>Spawns a <see cref="CarDefinition"/> with a garage paint and setup, for racing or for display.</summary>
    public static class CarBuilder
    {
        public static VehicleController Build(CarDefinition car, int paint, TuningPreset tuning, Vector3 position, Quaternion rotation,
            bool withPlayerInput, string objectName)
        {
            var settings = VehicleTuning.CreateTuned(car.settings, tuning);
            Material body = null;
            if (car.bodyMaterial != null)
            {
                body = new Material(car.bodyMaterial) { name = $"{car.id} paint {paint}" };
                body.SetColor("_BaseColor", car.Paint(paint));
            }

            var controller = TestVehicleFactory.Create(settings, position, rotation, new TestVehicleFactory.Materials
            {
                body = body,
                cabin = car.cabinMaterial,
                tyre = car.tyreMaterial,
                marker = car.markerMaterial
            }, withPlayerInput);
            controller.gameObject.name = objectName;
            controller.gameObject.AddComponent<RuntimeAssetOwner>().Own(settings, body);
            return controller;
        }

        /// <summary>A static copy for menus and the showroom: no physics, no input.</summary>
        public static GameObject BuildDisplayModel(CarDefinition car, int paint, TuningPreset tuning, Transform parent)
        {
            var controller = Build(car, paint, tuning, parent.position, parent.rotation, withPlayerInput: false, $"{car.displayName} (display)");
            controller.Body.isKinematic = true;
            controller.enabled = false;
            controller.transform.SetParent(parent, true);
            return controller.gameObject;
        }
    }
}
