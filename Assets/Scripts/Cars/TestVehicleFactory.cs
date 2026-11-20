using System.Collections.Generic;
using RealisticCarRacing.Controls;
using UnityEngine;

namespace RealisticCarRacing.Vehicles
{
    /// <summary>
    /// Builds the placeholder car (box body) with the full physics setup. Used until a licensed car model is
    /// imported, and by the automated tests. With a <see cref="CarVisualKit"/> it gets the production detail
    /// systems: modelled wheels with brake discs and steering calipers, glass, head/brake/reverse lamps, headlight
    /// beams, seats and working gauges. Without one it stays all primitives (fast, for physics tests).
    /// The visual parts never change the physics: colliders, wheel positions and mass are the same either way.
    /// </summary>
    public static class TestVehicleFactory
    {
        public struct Materials
        {
            public Material body;
            public Material cabin;
            public Material tyre;
            public Material marker;
            public CarVisualKit kit;
            /// <summary>Which of the kit's wheel designs to fit (0 = factory wheels).</summary>
            public int wheelDesign;
        }

        const float WheelWidth = 0.24f;
        const float KitWheelWidth = 0.27f; // visual only; the WheelCollider has no width

        static readonly (string name, Axle axle, bool isLeft, Vector3 position)[] WheelLayout =
        {
            ("FrontLeft", Axle.Front, true, new Vector3(-0.8f, 0.4f, 1.35f)),
            ("FrontRight", Axle.Front, false, new Vector3(0.8f, 0.4f, 1.35f)),
            ("RearLeft", Axle.Rear, true, new Vector3(-0.8f, 0.4f, -1.3f)),
            ("RearRight", Axle.Rear, false, new Vector3(0.8f, 0.4f, -1.3f))
        };

        // Renderer names used to pick the level of detail they belong to.
        const string LowDetailTyre = "TyreLow";
        /// <summary>Name of each wheel's rim renderer (see <see cref="CarBuilder.FitWheels"/>).</summary>
        public const string RimName = "Rim";
        static readonly HashSet<string> CloseUpDetails = new HashSet<string> { "SpinMarker", RimName, "BrakeDisc", "Caliper" };
        public const string WheelVisualsName = "WheelVisuals";
        const string LampsRoot = "Lamps"; // everything under it is a close-up detail

        /// <param name="settings">Tuning asset; its wheel radius sizes the wheel meshes.</param>
        /// <param name="addPlayerInput">Adds <see cref="PlayerVehicleInput"/> so the car is drivable straight away (and, with a kit, headlight beams).</param>
        public static VehicleController Create(VehicleSettings settings, Vector3 position, Quaternion rotation,
            Materials materials = default, bool addPlayerInput = true)
        {
            var kit = materials.kit;
            var root = new GameObject("TestVehicle");
            root.SetActive(false); // defer Awake until everything is wired up
            root.transform.SetPositionAndRotation(position, rotation);
            root.AddComponent<Rigidbody>();

            CreateBox("Chassis", root.transform, new Vector3(0f, 0.55f, 0f), new Vector3(1.5f, 0.4f, 4.2f), materials.body, keepCollider: true);
            var cabin = CreateBox("Cabin", root.transform, new Vector3(0f, 1.05f, -0.25f), new Vector3(1.3f, 0.65f, 2f), materials.cabin, keepCollider: true);
            Renderer glass = null;
            if (kit != null)
            {
                // The cabin box stays as the collider (same mass distribution); a shaped glasshouse is drawn instead.
                Object.DestroyImmediate(cabin.GetComponent<MeshRenderer>());
                Object.DestroyImmediate(cabin.GetComponent<MeshFilter>());
                CreateMesh("CabinFrame", root.transform, kit.greenhouseFrame, materials.body, Vector3.zero, Vector3.one);
                glass = CreateMesh("CabinGlass", root.transform, kit.greenhouseGlass, kit.glass, Vector3.zero, Vector3.one);
                glass.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }

            var collidersRoot = CreateChild("WheelColliders", root.transform, Vector3.zero);
            var visualsRoot = CreateChild(WheelVisualsName, root.transform, Vector3.zero);
            var wheels = new VehicleWheel[WheelLayout.Length];

            for (int i = 0; i < WheelLayout.Length; i++)
            {
                var (wheelName, axle, isLeft, wheelPosition) = WheelLayout[i];

                var wheelCollider = CreateChild(wheelName + "Collider", collidersRoot, wheelPosition).gameObject.AddComponent<WheelCollider>();
                wheelCollider.radius = settings.wheelRadius;
                wheelCollider.suspensionDistance = settings.suspensionDistance;

                var visual = CreateChild(wheelName, visualsRoot, wheelPosition);
                if (kit != null)
                    BuildKitWheel(kit, materials.wheelDesign, settings.wheelRadius, isLeft, visual, wheelCollider, visualsRoot);
                else
                    BuildPrimitiveWheel(settings.wheelRadius, isLeft, visual, materials);

                wheels[i] = new VehicleWheel { wheelCollider = wheelCollider, visual = visual, axle = axle, isLeft = isLeft };
            }

            var controller = root.AddComponent<VehicleController>();
            controller.Configure(settings, wheels);
            if (addPlayerInput)
                root.AddComponent<PlayerVehicleInput>();
            if (kit != null)
                BuildLamps(root.transform, controller, kit, headlightBeams: addPlayerInput);
            BuildInterior(root.transform, controller, materials, glass);
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

        static void BuildPrimitiveWheel(float radius, bool isLeft, Transform visual, Materials materials)
        {
            float diameter = radius * 2f;
            var tyre = CreatePrimitive(PrimitiveType.Cylinder, "Tyre", visual, Vector3.zero, materials.tyre, keepCollider: false);
            tyre.localRotation = Quaternion.Euler(0f, 0f, 90f);
            tyre.localScale = new Vector3(diameter, WheelWidth * 0.5f, diameter);

            // A bright block on the outer face makes wheel spin visible.
            float outerFace = (isLeft ? -1f : 1f) * (WheelWidth * 0.5f + 0.01f);
            CreateBox("SpinMarker", visual, new Vector3(outerFace, radius * 0.55f, 0f),
                new Vector3(0.02f, radius * 0.6f, 0.1f), materials.marker, keepCollider: false);
        }

        /// <summary>Tyre, rim and disc spin with the wheel; the caliper only steers (see <see cref="BrakeCaliperVisual"/>).</summary>
        static void BuildKitWheel(CarVisualKit kit, int wheelDesign, float radius, bool isLeft, Transform visual, WheelCollider wheelCollider,
            Transform visualsRoot)
        {
            // Kit meshes face +X (outboard of a right-hand wheel); left wheels are mirrored.
            var scale = new Vector3((isLeft ? -1f : 1f) * KitWheelWidth, radius, radius);
            CreateMesh("Tyre", visual, kit.tyre, kit.tyreRubber, Vector3.zero, scale);
            CreateMesh(LowDetailTyre, visual, kit.tyreLow, kit.tyreRubber, Vector3.zero, scale);
            // Only the tyre casts a shadow: the rim, disc and caliper are inside its silhouette anyway, and every
            // shadow caster is drawn again for each shadow cascade.
            var (rimMesh, rimFinish) = kit.Rim(wheelDesign);
            CreateMesh(RimName, visual, rimMesh, rimFinish, Vector3.zero, scale).shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            CreateMesh("BrakeDisc", visual, kit.brakeDisc, kit.brakeDiscMetal, Vector3.zero, scale).shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            var caliper = CreateMesh("Caliper", visualsRoot, kit.caliper, kit.caliperPaint, visual.localPosition, scale);
            caliper.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            caliper.gameObject.AddComponent<BrakeCaliperVisual>().Configure(wheelCollider, visual);
        }

        /// <summary>
        /// Head, tail/brake and reverse lamps on the body, and (for the player) headlight beams. At runtime each
        /// left/right pair is merged into one renderer (one draw call per lamp type).
        /// </summary>
        static void BuildLamps(Transform root, VehicleController controller, CarVisualKit kit, bool headlightBeams)
        {
            var lamps = CreateChild(LampsRoot, root, Vector3.zero);
            var head = CreateChild("HeadLamps", lamps, Vector3.zero);
            var brake = CreateChild("BrakeLamps", lamps, Vector3.zero);
            var reverse = CreateChild("ReverseLamps", lamps, Vector3.zero);
            var beams = new List<Light>();
            foreach (float side in new[] { -1f, 1f })
            {
                CreateBox("Lamp_Head", head, new Vector3(side * 0.52f, 0.64f, 2.095f), new Vector3(0.38f, 0.09f, 0.03f), kit.headlight, keepCollider: false);
                CreateBox("Lamp_Brake", brake, new Vector3(side * 0.5f, 0.66f, -2.095f), new Vector3(0.36f, 0.07f, 0.03f), kit.tailLight, keepCollider: false);
                CreateBox("Lamp_Reverse", reverse, new Vector3(side * 0.18f, 0.66f, -2.095f), new Vector3(0.12f, 0.05f, 0.03f), kit.reverseLightOff, keepCollider: false);

                if (!headlightBeams)
                    continue;
                // A child of the lamps root, not the scaled lens, so the light isn't distorted.
                var beam = CreateChild("HeadlightBeam", lamps, new Vector3(side * 0.52f, 0.64f, 2.15f)).gameObject.AddComponent<Light>();
                beam.transform.localRotation = Quaternion.Euler(4f, side * 2f, 0f);
                beam.type = LightType.Spot;
                beam.range = 45f;
                beam.spotAngle = 70f;
                beam.innerSpotAngle = 35f;
                beam.intensity = 6f;
                beam.color = new Color(1f, 0.96f, 0.88f);
                beam.shadows = LightShadows.None; // shadowed spot lights are too costly for every frame on phones
                beam.renderMode = LightRenderMode.ForcePixel;
                beams.Add(beam);
            }

            if (Application.isPlaying)
            {
                var merged = new List<Mesh>();
                foreach (var group in new[] { head, brake, reverse })
                    merged.AddRange(RendererMerger.MergeChildren(group));
                root.gameObject.AddComponent<RuntimeAssetOwner>().Own(merged.ToArray());
            }
            foreach (var lamp in lamps.GetComponentsInChildren<MeshRenderer>(true))
                lamp.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            root.gameObject.AddComponent<CarLights>().Configure(controller,
                brake.GetComponentsInChildren<Renderer>(true), reverse.GetComponentsInChildren<Renderer>(true),
                kit.tailLight, kit.brakeLight, kit.reverseLightOff, kit.reverseLightOn, beams.ToArray());
        }

        /// <summary>
        /// Close up everything is drawn; further away the small details (rims, discs, calipers, lamps, spin
        /// markers) are dropped and coarse tyres used; tiny on screen the car is culled.
        /// </summary>
        static void AddLevelsOfDetail(GameObject root)
        {
            var near = new List<Renderer>();
            var far = new List<Renderer>();
            var interior = root.transform.Find("Interior");
            var lamps = root.transform.Find(LampsRoot);
            bool hasLowTyres = false;
            foreach (var renderer in root.GetComponentsInChildren<MeshRenderer>(true))
                hasLowTyres |= renderer.name == LowDetailTyre;
            foreach (var renderer in root.GetComponentsInChildren<MeshRenderer>(true))
            {
                if (renderer.transform.IsChildOf(interior))
                    continue;
                bool closeUpOnly = CloseUpDetails.Contains(renderer.name) || (hasLowTyres && renderer.name == "Tyre")
                    || (lamps != null && renderer.transform.IsChildOf(lamps));
                if (renderer.name != LowDetailTyre)
                    near.Add(renderer);
                if (!closeUpOnly)
                    far.Add(renderer);
            }
            var lodGroup = root.AddComponent<LODGroup>();
            lodGroup.SetLODs(new[]
            {
                new LOD(0.08f, near.ToArray()),
                new LOD(0.006f, far.ToArray())
            });
            lodGroup.RecalculateBounds();
        }

        /// <summary>
        /// Cockpit for the in-car camera (dashboard, pillars, roof lining, turning steering wheel; with a kit also
        /// seats, console, gauges with live needles and a screen) and the hood/cockpit camera mounts. The cabin's
        /// own faces are culled from inside, so the view stays open.
        /// </summary>
        static void BuildInterior(Transform root, VehicleController controller, Materials materials, Renderer exteriorGlass)
        {
            var kit = materials.kit;
            var trimMaterial = kit != null ? kit.plastic : materials.cabin;
            var gripMaterial = kit != null ? kit.leather : materials.tyre;

            var interior = CreateChild("Interior", root, Vector3.zero);
            var trim = CreateChild("Trim", interior, Vector3.zero);
            CreateBox("Dashboard", trim, new Vector3(0f, 0.84f, 0.6f), new Vector3(1.24f, 0.18f, 0.3f), trimMaterial, keepCollider: false);
            CreateBox("RoofLining", trim, new Vector3(0f, 1.35f, -0.25f), new Vector3(1.26f, 0.03f, 1.96f), kit != null ? kit.headliner : trimMaterial, keepCollider: false);
            foreach (float side in new[] { -1f, 1f })
            {
                var pillar = CreateBox("APillar", trim, new Vector3(side * 0.6f, 1.06f, 0.62f), new Vector3(0.06f, 0.62f, 0.06f), trimMaterial, keepCollider: false);
                pillar.localRotation = Quaternion.Euler(-24f, 0f, 0f);
            }

            if (kit != null)
            {
                foreach (float side in new[] { -1f, 1f })
                {
                    CreateBox("SeatBase", trim, new Vector3(side * 0.32f, 0.84f, -0.32f), new Vector3(0.5f, 0.12f, 0.5f), kit.leather, keepCollider: false);
                    var back = CreateBox("SeatBack", trim, new Vector3(side * 0.32f, 1.08f, -0.6f), new Vector3(0.48f, 0.55f, 0.12f), kit.leather, keepCollider: false);
                    back.localRotation = Quaternion.Euler(-12f, 0f, 0f);
                    CreateBox("DoorCard", trim, new Vector3(side * 0.64f, 0.9f, -0.15f), new Vector3(0.04f, 0.3f, 1.3f), kit.leather, keepCollider: false);
                }
                CreateBox("Console", trim, new Vector3(0f, 0.82f, 0.05f), new Vector3(0.22f, 0.14f, 0.7f), kit.plastic, keepCollider: false);
                // Instrument binnacle on top of the dash, in front of the driver, with a shade over it.
                CreateBox("Binnacle", trim, new Vector3(-0.32f, 0.975f, 0.56f), new Vector3(0.46f, 0.13f, 0.16f), kit.plastic, keepCollider: false);
                CreateBox("BinnacleHood", trim, new Vector3(-0.32f, 1.05f, 0.52f), new Vector3(0.48f, 0.03f, 0.24f), kit.plastic, keepCollider: false);
                // Thin boxes rather than quads: the box's driver-facing side shows the whole texture, unmirrored.
                var screen = CreateBox("Screen", trim, new Vector3(0f, 0.86f, 0.448f), new Vector3(0.2f, 0.12f, 0.004f), kit.screen, keepCollider: false);
                screen.localRotation = Quaternion.Euler(10f, 0f, 0f);
            }

            // Steering column tilted 25° down towards the dash; the wheel spins about the column (local Z).
            // Low enough that the driver sees the gauges over the top of the rim.
            var steeringWheel = CreateChild("SteeringWheel", interior, new Vector3(-0.32f, 0.82f, 0.3f));
            steeringWheel.localRotation = Quaternion.Euler(25f, 0f, 0f);
            // Open rim made of short segments, so the driver sees the road through it.
            const int rimSegments = 14;
            const float rimRadius = 0.18f;
            for (int i = 0; i < rimSegments; i++)
            {
                float angle = i * 360f / rimSegments;
                var segment = CreateBox("Rim", steeringWheel,
                    Quaternion.Euler(0f, 0f, angle) * new Vector3(rimRadius, 0f, 0f),
                    new Vector3(0.03f, 2f * Mathf.PI * rimRadius / rimSegments * 1.1f, 0.03f), gripMaterial, keepCollider: false);
                segment.localRotation = Quaternion.Euler(0f, 0f, angle);
            }
            CreateBox("Spoke", steeringWheel, new Vector3(0f, -0.02f, 0f), new Vector3(2f * rimRadius, 0.04f, 0.02f), gripMaterial, keepCollider: false);
            CreateBox("Hub", steeringWheel, new Vector3(0f, -0.02f, 0f), new Vector3(0.08f, 0.08f, 0.04f), gripMaterial, keepCollider: false);
            CreateBox("CentreMark", steeringWheel, new Vector3(0f, rimRadius, -0.02f), new Vector3(0.025f, 0.04f, 0.01f), materials.marker, keepCollider: false);
            steeringWheel.gameObject.AddComponent<SteeringWheelVisual>().Configure(controller);

            if (kit != null)
                BuildGauges(interior, trim, controller, kit);

            // At runtime, 20+ interior pieces become a few renderers (prefabs saved in the editor keep the pieces,
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
            // The interior is only drawn while the camera is in the cockpit (VehicleCameraRig turns it on), and the
            // outer glass only while it is outside.
            root.gameObject.AddComponent<VehicleCameraMounts>().Configure(hood, cockpit, interior,
                exteriorGlass != null ? new[] { exteriorGlass } : null);
        }

        /// <summary>Rev counter (left) and speedometer (right) in the driver's binnacle, tilted towards the driver.</summary>
        /// <remarks>The dial faces don't move, so they join the trim (merged); only the needles stay separate.</remarks>
        static void BuildGauges(Transform interior, Transform trim, VehicleController controller, CarVisualKit kit)
        {
            var gauges = CreateChild("Gauges", interior, new Vector3(-0.32f, 0.975f, 0.475f));
            gauges.localRotation = Quaternion.Euler(15f, 0f, 0f);
            var needles = new Transform[2];
            for (int i = 0; i < 2; i++)
            {
                float x = i == 0 ? -0.1f : 0.1f;
                CreateBox("GaugeFace", gauges, new Vector3(x, 0f, 0f), new Vector3(0.14f, 0.14f, 0.004f), kit.gaugeFace, keepCollider: false)
                    .SetParent(trim, worldPositionStays: true);
                needles[i] = CreateChild("NeedlePivot", gauges, new Vector3(x, 0f, -0.004f));
                CreateBox("Needle", needles[i], new Vector3(0f, 0.027f, 0f), new Vector3(0.005f, 0.055f, 0.003f), kit.gaugeNeedle, keepCollider: false);
            }
            gauges.gameObject.AddComponent<DashboardGauges>().Configure(controller, needles[0], needles[1]);
        }

        static Transform CreateChild(string name, Transform parent, Vector3 localPosition)
        {
            var child = new GameObject(name).transform;
            child.SetParent(parent, false);
            child.localPosition = localPosition;
            return child;
        }

        static MeshRenderer CreateMesh(string name, Transform parent, Mesh mesh, Material material, Vector3 localPosition, Vector3 localScale)
        {
            var child = CreateChild(name, parent, localPosition);
            child.localScale = localScale;
            child.gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = child.gameObject.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            return renderer;
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
