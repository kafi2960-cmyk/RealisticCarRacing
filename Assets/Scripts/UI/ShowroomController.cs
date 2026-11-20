using System.Collections.Generic;
using RealisticCarRacing.Vehicles;
using UnityEngine;

namespace RealisticCarRacing.UI
{
    /// <summary>
    /// Puts the selected car, in its garage paint and wheels, on the showroom turntable. Each car model is built once
    /// and kept (pooled) for the rest of the menu session; paint and wheel changes are applied in place instead of
    /// rebuilding. Turning the turntable and the camera is <see cref="ShowroomOrbit"/>'s job.
    /// </summary>
    public sealed class ShowroomController : MonoBehaviour
    {
        static readonly int BaseColor = Shader.PropertyToID("_BaseColor");

        [SerializeField] Transform turntable;

        readonly Dictionary<CarDefinition, GameObject> models = new Dictionary<CarDefinition, GameObject>();
        MaterialPropertyBlock paintBlock;

        public GameObject CurrentCar { get; private set; }
        public CarDefinition CurrentDefinition { get; private set; }
        public int ModelsBuilt => models.Count;

        public void Configure(Transform turntableTransform) => turntable = turntableTransform;

        public void Show(CarDefinition car, int paint, int wheels, TuningPreset tuning)
        {
            if (!models.TryGetValue(car, out var model))
            {
                model = CarBuilder.BuildDisplayModel(car, 0, wheels, tuning, turntable);
                models[car] = model;
            }
            foreach (var other in models.Values)
                other.SetActive(other == model);
            CurrentCar = model;
            CurrentDefinition = car;

            paintBlock ??= new MaterialPropertyBlock();
            var body = model.transform.Find("Chassis").GetComponent<MeshRenderer>();
            body.GetPropertyBlock(paintBlock);
            paintBlock.SetColor(BaseColor, car.Paint(paint));
            // Every painted panel (body, roof...) shares the chassis paint material.
            foreach (var panel in model.GetComponentsInChildren<MeshRenderer>(true))
            {
                if (panel.sharedMaterial == body.sharedMaterial)
                    panel.SetPropertyBlock(paintBlock);
            }
            CarBuilder.FitWheels(model, car.visualKit, wheels);
        }

        /// <summary>The paint currently shown on the displayed car.</summary>
        public Color ShownPaint
        {
            get
            {
                var body = CurrentCar.transform.Find("Chassis").GetComponent<MeshRenderer>();
                paintBlock ??= new MaterialPropertyBlock();
                body.GetPropertyBlock(paintBlock);
                return paintBlock.GetColor(BaseColor);
            }
        }

        /// <summary>The rim mesh currently fitted to the displayed car.</summary>
        public Mesh ShownRim
        {
            get
            {
                var wheels = CurrentCar.transform.Find(TestVehicleFactory.WheelVisualsName);
                foreach (var filter in wheels.GetComponentsInChildren<MeshFilter>(true))
                {
                    if (filter.name == TestVehicleFactory.RimName)
                        return filter.sharedMesh;
                }
                return null;
            }
        }
    }
}
