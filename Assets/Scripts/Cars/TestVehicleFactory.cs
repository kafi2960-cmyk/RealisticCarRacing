using System.Collections.Generic;
using RealisticCarRacing.Controls;
using UnityEngine;

namespace RealisticCarRacing.Vehicles
{
    /// <summary>
    /// Builds a placeholder car from Unity primitives (box body, cylinder wheels) with the full physics
    /// setup. Used until a licensed car model is imported, and by the automated tests.
    /// </summary>
    public static class TestVehicleFactory
    {
        public struct Materials
        {
            public Material body;
            public Material cabin;
            public Material tyre;
            public Material marker;
        }

        const float WheelWidth = 0.24f;

        static readonly (string name, Axle axle, bool isLeft, Vector3 position)[] WheelLayout =
        {
            ("FrontLeft", Axle.Front, true, new Vector3(-0.8f, 0.4f, 1.35f)),
            ("FrontRight", Axle.Front, false, new Vector3(0.8f, 0.4f, 1.35f)),
            ("RearLeft", Axle.Rear, true, new Vector3(-0.8f, 0.4f, -1.3f)),
            ("RearRight", Axle.Rear, false, new Vector3(0.8f, 0.4f, -1.3f))
        };

        /// <param name="settings">Tuning asset; its wheel radius sizes the wheel meshes.</param>
        /// <param name="addPlayerInput">Adds <see cref="PlayerVehicleInput"/> so the car is drivable straight away.</param>
        public static VehicleController Create(VehicleSettings settings, Vector3 position, Quaternion rotation,
            Materials materials = default, bool addPlayerInput = true)
        {
            var root = new GameObject("TestVehicle");
            root.SetActive(false); // defer Awake until everything is wired up
            root.transform.SetPositionAndRotation(position, rotation);
            root.AddComponent<Rigidbody>();

            CreateBox("Chassis", root.transform, new Vector3(0f, 0.55f, 0f), new Vector3(1.5f, 0.4f, 4.2f), materials.body, keepCollider: true);
            CreateBox("Cabin", root.transform, new Vector3(0f, 1.05f, -0.25f), new Vector3(1.3f, 0.65f, 2f), materials.cabin, keepCollider: true);

            var collidersRoot = CreateChild("WheelColliders", root.transform, Vector3.zero);
            var visualsRoot = CreateChild("WheelVisuals", root.transform, Vector3.zero);
            var wheels = new VehicleWheel[WheelLayout.Length];

            for (int i = 0; i < WheelLayout.Length; i++)
            {
                var (wheelName, axle, isLeft, wheelPosition) = WheelLayout[i];

                var wheelCollider = CreateChild(wheelName + "Collider", collidersRoot, wheelPosition).gameObject.AddComponent<WheelCollider>();
                wheelCollider.radius = settings.wheelRadius;
                wheelCollider.suspensionDistance = settings.suspensionDistance;

                var visual = CreateChild(wheelName, visualsRoot, wheelPosition);
                float diameter = settings.wheelRadius * 2f;
                var tyre = CreatePrimitive(PrimitiveType.Cylinder, "Tyre", visual, Vector3.zero, materials.tyre, keepCollider: false);
                tyre.localRotation = Quaternion.Euler(0f, 0f, 90f);
                tyre.localScale = new Vector3(diameter, WheelWidth * 0.5f, diameter);

                // A bright block on the outer face makes wheel spin visible.
                float outerFace = (isLeft ? -1f : 1f) * (WheelWidth * 0.5f + 0.01f);
                CreateBox("SpinMarker", visual, new Vector3(outerFace, settings.wheelRadius * 0.55f, 0f),
                    new Vector3(0.02f, settings.wheelRadius * 0.6f, 0.1f), materials.marker, keepCollider: false);

                wheels[i] = new VehicleWheel { wheelCollider = wheelCollider, visual = visual, axle = axle, isLeft = isLeft };
            }

            var controller = root.AddComponent<VehicleController>();
            controller.Configure(settings, wheels);
            if (addPlayerInput)
                root.AddComponent<PlayerVehicleInput>();
            BuildInterior(root.transform, controller, materials);
            AddLevelsOfDetail(root);

            int vehicleLayer = LayerMask.NameToLayer(VehicleLayer);
            if (vehicleLayer >= 0)
            {
                foreach (var t in root.GetComponentsInChildren<Transform>(true))
                    t.gameObject.layer = vehicleLayer;
            }

            root.SetActive(true);
            return controller;
        }

        /// <summary>Physics/render layer for cars (cameras ignore it when avoiding walls).</summary>
        public const string VehicleLayer = "Vehicles";

        /// <summary>Distant cars drop their spin markers, then disappear when tiny on screen.</summary>
        static void AddLevelsOfDetail(GameObject root)
        {
            var exterior = new List<Renderer>();
            var details = new List<Renderer>();
            foreach (var renderer in root.GetComponentsInChildren<MeshRenderer>(true))
            {
                if (renderer.transform.IsChildOf(root.transform.Find("Interior")))
                    continue;
                (renderer.name == "SpinMarker" ? details : exterior).Add(renderer);
            }
            var all = new List<Renderer>(exterior);
            all.AddRange(details);
            var lodGroup = root.AddComponent<LODGroup>();
            lodGroup.SetLODs(new[]
            {
                new LOD(0.08f, all.ToArray()),
                new LOD(0.006f, exterior.ToArray())
            });
            lodGroup.RecalculateBounds();
        }

        /// <summary>
        /// Minimal cockpit for the in-car camera (dashboard, pillars, roof lining, turning steering wheel) and the
        /// hood/cockpit camera mounts. The cabin box's own faces are culled from inside, so the view stays open.
        /// </summary>
        static void BuildInterior(Transform root, VehicleController controller, Materials materials)
        {
            var interior = CreateChild("Interior", root, Vector3.zero);
            var trim = CreateChild("Trim", interior, Vector3.zero);
            CreateBox("Dashboard", trim, new Vector3(0f, 0.84f, 0.6f), new Vector3(1.24f, 0.18f, 0.3f), materials.cabin, keepCollider: false);
            CreateBox("RoofLining", trim, new Vector3(0f, 1.35f, -0.25f), new Vector3(1.26f, 0.03f, 1.96f), materials.cabin, keepCollider: false);
            foreach (float side in new[] { -1f, 1f })
            {
                var pillar = CreateBox("APillar", trim, new Vector3(side * 0.6f, 1.06f, 0.62f), new Vector3(0.06f, 0.62f, 0.06f), materials.cabin, keepCollider: false);
                pillar.localRotation = Quaternion.Euler(-24f, 0f, 0f);
            }

            // Steering column tilted 25° down towards the dash; the wheel spins about the column (local Z).
            var steeringWheel = CreateChild("SteeringWheel", interior, new Vector3(-0.32f, 0.86f, 0.3f));
            steeringWheel.localRotation = Quaternion.Euler(25f, 0f, 0f);
            // Open rim made of short segments, so the driver sees the road through it.
            const int rimSegments = 14;
            const float rimRadius = 0.18f;
            for (int i = 0; i < rimSegments; i++)
            {
                float angle = i * 360f / rimSegments;
                var segment = CreateBox("Rim", steeringWheel,
                    Quaternion.Euler(0f, 0f, angle) * new Vector3(rimRadius, 0f, 0f),
                    new Vector3(0.03f, 2f * Mathf.PI * rimRadius / rimSegments * 1.1f, 0.03f), materials.tyre, keepCollider: false);
                segment.localRotation = Quaternion.Euler(0f, 0f, angle);
            }
            CreateBox("Spoke", steeringWheel, new Vector3(0f, -0.02f, 0f), new Vector3(2f * rimRadius, 0.04f, 0.02f), materials.tyre, keepCollider: false);
            CreateBox("Hub", steeringWheel, new Vector3(0f, -0.02f, 0f), new Vector3(0.08f, 0.08f, 0.04f), materials.tyre, keepCollider: false);
            CreateBox("CentreMark", steeringWheel, new Vector3(0f, rimRadius, -0.02f), new Vector3(0.025f, 0.04f, 0.01f), materials.marker, keepCollider: false);
            steeringWheel.gameObject.AddComponent<SteeringWheelVisual>().Configure(controller);

            // At runtime, 20+ interior pieces become 3 renderers (prefabs saved in the editor keep the pieces,
            // because merged meshes are not assets).
            if (Application.isPlaying)
            {
                var merged = RendererMerger.MergeChildren(trim);
                merged.AddRange(RendererMerger.MergeChildren(steeringWheel));
                root.gameObject.AddComponent<RuntimeAssetOwner>().Own(merged.ToArray());
            }

            var mounts = CreateChild("CameraMounts", root, Vector3.zero);
            var hood = CreateChild("Hood", mounts, new Vector3(0f, 0.98f, 1f));
            var cockpit = CreateChild("Cockpit", mounts, new Vector3(-0.32f, 1.08f, -0.25f));
            // The interior is only drawn while the camera is in the cockpit (VehicleCameraRig turns it on).
            root.gameObject.AddComponent<VehicleCameraMounts>().Configure(hood, cockpit, interior);
        }

        static Transform CreateChild(string name, Transform parent, Vector3 localPosition)
        {
            var child = new GameObject(name).transform;
            child.SetParent(parent, false);
            child.localPosition = localPosition;
            return child;
        }

        static Transform CreateBox(string name, Transform parent, Vector3 localPosition, Vector3 size, Material material, bool keepCollider)
        {
            var box = CreatePrimitive(PrimitiveType.Cube, name, parent, localPosition, material, keepCollider);
            box.localScale = size;
            return box;
        }

        static Transform CreatePrimitive(PrimitiveType type, string name, Transform parent, Vector3 localPosition, Material material, bool keepCollider)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            if (!keepCollider)
                Object.DestroyImmediate(go.GetComponent<Collider>()); // immediate, so it never takes part in a physics step
            if (material != null)
                go.GetComponent<MeshRenderer>().sharedMaterial = material;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            return go.transform;
        }
    }
}
