using UnityEngine;

namespace RealisticCarRacing.Vehicles
{
    /// <summary>Spawns a <see cref="CarDefinition"/> with a garage paint, wheels and setup, for racing or for display.</summary>
    public static class CarBuilder
    {
        public static VehicleController Build(CarDefinition car, int paint, int wheels, TuningPreset tuning, Vector3 position, Quaternion rotation,
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
                marker = car.markerMaterial,
                kit = car.visualKit,
                wheelDesign = wheels
            }, withPlayerInput);
            controller.gameObject.name = objectName;
            controller.gameObject.AddComponent<RuntimeAssetOwner>().Own(settings, body);
            return controller;
        }

        /// <summary>A static copy for menus and the showroom: no physics, no input.</summary>
        public static GameObject BuildDisplayModel(CarDefinition car, int paint, int wheels, TuningPreset tuning, Transform parent)
        {
            var controller = Build(car, paint, wheels, tuning, parent.position, parent.rotation, withPlayerInput: false, $"{car.displayName} (display)");
            controller.Body.isKinematic = true;
            // Interpolation would keep drawing the body at its last physics pose: it must follow the turntable instead.
            controller.Body.interpolation = RigidbodyInterpolation.None;
            controller.Body.detectCollisions = false;
            controller.enabled = false;
            controller.transform.SetParent(parent, true);
            return controller.gameObject;
        }

        /// <summary>Swaps the rims of an already built car to another wheel design (no rebuild; visual only).</summary>
        public static void FitWheels(GameObject builtCar, CarVisualKit kit, int design)
        {
            var wheelVisuals = builtCar.transform.Find(TestVehicleFactory.WheelVisualsName);
            if (kit == null || wheelVisuals == null)
                return;
            var (mesh, finish) = kit.Rim(design);
            foreach (var renderer in wheelVisuals.GetComponentsInChildren<MeshRenderer>(true))
            {
                if (renderer.name != TestVehicleFactory.RimName || !renderer.TryGetComponent(out MeshFilter filter))
                    continue;
                filter.sharedMesh = mesh;
                renderer.sharedMaterial = finish;
            }
        }
    }
}
