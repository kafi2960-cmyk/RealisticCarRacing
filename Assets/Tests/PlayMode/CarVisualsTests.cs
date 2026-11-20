using System.Collections;
using NUnit.Framework;
using RealisticCarRacing.Controls;
using RealisticCarRacing.Managers;
using RealisticCarRacing.Vehicles;
using UnityEngine;
using UnityEngine.TestTools;

namespace RealisticCarRacing.Tests
{
    /// <summary>
    /// The production car visuals (<see cref="CarVisualKit"/>): lamps follow the pedals and gear, wheels spin while
    /// calipers only steer, glass and gauges behave in the cockpit, distant cars drop detail, and none of it changes
    /// how the car drives.
    /// </summary>
    public class CarVisualsTests
    {
        sealed class ScriptedInput : IVehicleInputSource
        {
            public VehicleInputState State;
            public VehicleInputState ReadInput() => State;
        }

        GameObject ground;
        VehicleSettings settings;
        CarVisualKit kit;
        VehicleController car;
        ScriptedInput input;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            kit = Resources.Load<GameCatalog>(GameCatalog.ResourcePath).cars[0].visualKit;
            Assert.That(kit, Is.Not.Null, "the game's cars should have a visual kit");
            ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.transform.localScale = new Vector3(300f, 1f, 300f);
            settings = ScriptableObject.CreateInstance<VehicleSettings>();
            car = Spawn(new Vector3(0f, 0.5f, 0f), kit, out input);
            yield return Simulate(1.5f);
        }

        [TearDown]
        public void TearDown()
        {
            Object.Destroy(car.gameObject);
            Object.Destroy(ground);
            Object.Destroy(settings);
        }

        VehicleController Spawn(Vector3 position, CarVisualKit visualKit, out ScriptedInput scripted)
        {
            var spawned = TestVehicleFactory.Create(settings, position, Quaternion.identity,
                new TestVehicleFactory.Materials { kit = visualKit }, addPlayerInput: false);
            scripted = new ScriptedInput();
            spawned.SetInputSource(scripted);
            return spawned;
        }

        static IEnumerator Simulate(float seconds)
        {
            float end = Time.fixedTime + seconds;
            while (Time.fixedTime < end)
                yield return new WaitForFixedUpdate();
            yield return null; // let Update/LateUpdate apply the visuals
        }

        Renderer Find(string path) => car.transform.Find(path).GetComponent<Renderer>();

        [UnityTest]
        public IEnumerator BrakeLights_ComeOnWithTheBrakePedal()
        {
            var lights = car.GetComponent<CarLights>();
            Assert.That(lights.BrakeLamps.Count, Is.EqualTo(1), "both brake lamps are merged into one renderer (one draw call)");
            var lamp = lights.BrakeLamps[0];
            input.State = new VehicleInputState { Throttle = 1f };
            yield return Simulate(2f);
            Assert.That(lights.BrakeLightsOn, Is.False);
            Assert.That(lamp.sharedMaterial, Is.SameAs(kit.tailLight));

            input.State = new VehicleInputState { Brake = 1f };
            yield return Simulate(0.3f);
            Assert.That(lights.BrakeLightsOn, Is.True);
            Assert.That(lamp.sharedMaterial, Is.SameAs(kit.brakeLight));

            input.State = default;
            yield return Simulate(0.3f);
            Assert.That(lights.BrakeLightsOn, Is.False, "lights go out when the pedal is released");
        }

        [UnityTest]
        public IEnumerator ReverseLights_ComeOnInReverse_AndBrakeLightsStayOffWhileReversing()
        {
            var lights = car.GetComponent<CarLights>();
            input.State = new VehicleInputState { Brake = 1f }; // at a standstill, holding the brake selects reverse
            yield return Simulate(2.5f);
            Assert.That(car.Gear, Is.EqualTo(VehicleTransmission.Reverse));
            Assert.That(car.ForwardSpeed, Is.LessThan(-0.5f), "should be reversing");
            Assert.That(lights.ReverseLightsOn, Is.True);
            Assert.That(lights.BrakeLightsOn, Is.False, "the brake pedal is the reverse throttle now");
            Assert.That(lights.ReverseLamps[0].sharedMaterial, Is.SameAs(kit.reverseLightOn));
        }

        [UnityTest]
        public IEnumerator Calipers_SteerWithTheWheel_ButDoNotSpin()
        {
            var wheel = car.Wheels[0]; // front left
            var caliper = car.transform.Find("WheelVisuals/Caliper");
            var rim = wheel.visual.Find("Rim");
            input.State = new VehicleInputState { Throttle = 0.6f, Steer = 0.5f };
            yield return Simulate(2f);

            Quaternion local = Quaternion.Inverse(car.transform.rotation) * caliper.rotation;
            float steer = Vector3.SignedAngle(Vector3.forward, local * Vector3.forward, Vector3.up);
            Assert.That(steer, Is.EqualTo(wheel.wheelCollider.steerAngle).Within(1f), "caliper turns with the steering");
            Assert.That(Vector3.Angle(local * Vector3.up, Vector3.up), Is.LessThan(1f), "caliper does not rotate with the wheel");
            Assert.That(Vector3.Distance(caliper.position, wheel.visual.position), Is.LessThan(0.001f), "caliper follows the suspension");

            Quaternion before = Quaternion.Inverse(car.transform.rotation) * rim.rotation;
            Quaternion caliperBefore = Quaternion.Inverse(car.transform.rotation) * caliper.rotation;
            yield return Simulate(0.1f);
            Assert.That(Quaternion.Angle(before, Quaternion.Inverse(car.transform.rotation) * rim.rotation), Is.GreaterThan(5f), "rim spins");
            Assert.That(Quaternion.Angle(caliperBefore, Quaternion.Inverse(car.transform.rotation) * caliper.rotation), Is.LessThan(1f));
        }

        [UnityTest]
        public IEnumerator Cockpit_HidesTheOuterGlass_AndTheGaugesShowRevsAndSpeed()
        {
            var mounts = car.GetComponent<VehicleCameraMounts>();
            var glass = Find("CabinGlass");
            Assert.That(glass.enabled, Is.True, "glass is drawn from outside");
            mounts.SetInteriorVisible(true);
            Assert.That(glass.enabled, Is.False, "and hidden from the driver's seat");
            mounts.SetInteriorVisible(false);
            Assert.That(glass.enabled, Is.True);

            mounts.SetInteriorVisible(true); // gauges only animate while the cockpit is shown
            var gauges = car.GetComponentInChildren<DashboardGauges>(true);
            var tach = gauges.transform.GetChild(0); // needle pivots (the dial faces are merged into the trim)
            var speedo = gauges.transform.GetChild(1);
            yield return null; // first cockpit frame: the needles show idle revs and zero speed
            Assert.That(Mathf.DeltaAngle(speedo.localEulerAngles.z, gauges.NeedleAngle(0f)), Is.EqualTo(0f).Within(1f), "speedometer at zero");
            float idleTach = tach.localEulerAngles.z, idleSpeedo = speedo.localEulerAngles.z;
            input.State = new VehicleInputState { Throttle = 1f };
            yield return Simulate(3f);
            Assert.That(Mathf.DeltaAngle(idleTach, tach.localEulerAngles.z), Is.LessThan(-10f), "rev needle swings clockwise as revs rise");
            Assert.That(Mathf.DeltaAngle(idleSpeedo, speedo.localEulerAngles.z), Is.LessThan(-10f), "speedometer needle too");
            float expected = gauges.NeedleAngle(car.SpeedKmh / 320f);
            Assert.That(Mathf.DeltaAngle(speedo.localEulerAngles.z, expected), Is.EqualTo(0f).Within(3f));
        }

        [UnityTest]
        public IEnumerator DistantCars_DropSmallDetails()
        {
            var lods = car.GetComponent<LODGroup>().GetLODs();
            Assert.That(lods.Length, Is.EqualTo(2));
            bool Has(LOD lod, string name) => System.Array.Exists(lod.renderers, r => r != null && r.name == name);
            var brakeLamp = car.GetComponent<CarLights>().BrakeLamps[0];
            Assert.That(System.Array.IndexOf(lods[0].renderers, brakeLamp), Is.GreaterThanOrEqualTo(0), "lamps close up");
            Assert.That(System.Array.IndexOf(lods[1].renderers, brakeLamp), Is.LessThan(0), "lamps dropped at distance");
            foreach (var detail in new[] { "Rim", "BrakeDisc", "Caliper", "Tyre" })
            {
                Assert.That(Has(lods[0], detail), Is.True, $"{detail} close up");
                Assert.That(Has(lods[1], detail), Is.False, $"{detail} dropped at distance");
            }
            Assert.That(Has(lods[1], "TyreLow"), Is.True);
            Assert.That(Has(lods[0], "TyreLow"), Is.False);
            Assert.That(Has(lods[1], "Chassis") && Has(lods[1], "CabinFrame") && Has(lods[1], "CabinGlass"), Is.True);
            yield break;
        }

        [UnityTest]
        public IEnumerator EveryWheelDesign_FitsInsideTheTyre_AndLeavesThePhysicsAlone()
        {
            Assert.That(kit.wheelDesigns.Length, Is.GreaterThanOrEqualTo(4), "the garage offers several wheel designs");
            for (int i = 0; i < kit.wheelDesigns.Length; i++)
            {
                var design = kit.wheelDesigns[i];
                Assert.That(design.rim, Is.Not.Null, design.displayName);
                Assert.That(design.finish, Is.Not.Null, design.displayName);
                // Unit wheel: the tyre's bead sits at radius 0.66 and its faces at x = ±0.5.
                var bounds = design.rim.bounds;
                Assert.That(Mathf.Max(bounds.max.y, -bounds.min.y, bounds.max.z, -bounds.min.z), Is.LessThan(0.68f), $"{design.displayName} fits the tyre");
                Assert.That(bounds.max.x, Is.LessThanOrEqualTo(0.5f), $"{design.displayName} doesn't poke out of the tyre's outer face");

                var fitted = TestVehicleFactory.Create(settings, new Vector3(10f + 6f * i, 0.5f, 0f), Quaternion.identity,
                    new TestVehicleFactory.Materials { kit = kit, wheelDesign = i }, addPlayerInput: false);
                var rim = fitted.Wheels[0].visual.Find(TestVehicleFactory.RimName).GetComponent<MeshFilter>();
                Assert.That(rim.sharedMesh, Is.SameAs(design.rim));
                Assert.That(fitted.Body.mass, Is.EqualTo(car.Body.mass));
                Assert.That(Vector3.Distance(fitted.Body.inertiaTensor, car.Body.inertiaTensor), Is.LessThan(1e-3f), $"{design.displayName}: same inertia");
                Object.Destroy(fitted.gameObject);
            }
            yield break;
        }

        [UnityTest]
        public IEnumerator Physics_AreIdenticalWithAndWithoutTheKit()
        {
            var plain = Spawn(new Vector3(20f, 0.5f, 0f), null, out var plainInput);
            Assert.That(plain.Body.mass, Is.EqualTo(car.Body.mass));
            Assert.That(Vector3.Distance(plain.Body.centerOfMass, car.Body.centerOfMass), Is.LessThan(1e-4f), "same centre of mass");
            Assert.That(Vector3.Distance(plain.Body.inertiaTensor, car.Body.inertiaTensor), Is.LessThan(1e-3f), "same inertia");
            yield return Simulate(1.5f);

            var drive = new VehicleInputState { Throttle = 1f, Steer = 0.2f };
            input.State = drive;
            plainInput.State = drive;
            yield return Simulate(4f);
            Assert.That(car.SpeedKmh, Is.EqualTo(plain.SpeedKmh).Within(0.5f));
            Assert.That(car.transform.position.x - plain.transform.position.x, Is.EqualTo(-20f).Within(0.3f));
            Assert.That(car.transform.position.z - plain.transform.position.z, Is.EqualTo(0f).Within(0.3f));
            Object.Destroy(plain.gameObject);
        }
    }
}
