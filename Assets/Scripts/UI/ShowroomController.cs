using System.Collections.Generic;
using RealisticCarRacing.Vehicles;
using UnityEngine;

namespace RealisticCarRacing.UI
{
    /// <summary>
    /// Displays the selected car, in its garage paint, on a slowly rotating turntable. Each car model is built once
    /// and kept (pooled) for the rest of the menu session; paint changes recolour it in place instead of rebuilding.
    /// </summary>
    public sealed class ShowroomController : MonoBehaviour
    {
        static readonly int BaseColor = Shader.PropertyToID("_BaseColor");

        [SerializeField] Transform turntable;
        [Tooltip("Degrees per second.")]
        [SerializeField] float rotationSpeed = 14f;

        readonly Dictionary<CarDefinition, GameObject> models = new Dictionary<CarDefinition, GameObject>();
        MaterialPropertyBlock paintBlock;

        public GameObject CurrentCar { get; private set; }
        public int ModelsBuilt => models.Count;

        public void Configure(Transform turntableTransform) => turntable = turntableTransform;

        public void Show(CarDefinition car, int paint, TuningPreset tuning)
        {
            if (!models.TryGetValue(car, out var model))
            {
                model = CarBuilder.BuildDisplayModel(car, 0, tuning, turntable);
                models[car] = model;
            }
            foreach (var other in models.Values)
                other.SetActive(other == model);
            CurrentCar = model;

            paintBlock ??= new MaterialPropertyBlock();
            var body = model.transform.Find("Chassis").GetComponent<MeshRenderer>();
            body.GetPropertyBlock(paintBlock);
            paintBlock.SetColor(BaseColor, car.Paint(paint));
            body.SetPropertyBlock(paintBlock);
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

        void Update()
        {
            if (turntable != null)
                turntable.Rotate(0f, rotationSpeed * Time.unscaledDeltaTime, 0f, Space.World);
        }
    }
}
