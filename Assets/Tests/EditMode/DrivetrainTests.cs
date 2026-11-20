using NUnit.Framework;
using RealisticCarRacing.Vehicles;
using UnityEngine;

namespace RealisticCarRacing.Tests
{
    public class DrivetrainTests
    {
        VehicleSettings settings;

        [SetUp]
        public void SetUp() => settings = ScriptableObject.CreateInstance<VehicleSettings>();

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(settings);

        [Test]
        public void Engine_ProducesTorqueUnderThrottle()
        {
            var engine = new VehicleEngine(settings);
            float torque = 0f;
            for (int i = 0; i < 100; i++)
                torque = engine.Update(1f, 3000f, clutchEngaged: true, shifting: false, 0.02f);
            Assert.That(torque, Is.GreaterThan(200f));
            Assert.That(engine.Rpm, Is.EqualTo(3200f).Within(50f), "clutch slip should hold launch RPM");
        }

        [Test]
        public void Engine_RevLimiterCutsTorque()
        {
            var engine = new VehicleEngine(settings);
            for (int i = 0; i < 200; i++)
                engine.Update(1f, settings.redlineRpm + 500f, clutchEngaged: true, shifting: false, 0.02f);
            Assert.That(engine.IsRevLimited, Is.True);
            Assert.That(engine.Update(1f, settings.redlineRpm + 500f, true, false, 0.02f), Is.LessThanOrEqualTo(0f));
        }

        [Test]
        public void Engine_BrakesWhenOffThrottle()
        {
            var engine = new VehicleEngine(settings);
            for (int i = 0; i < 100; i++)
                engine.Update(0f, 5000f, clutchEngaged: true, shifting: false, 0.02f);
            Assert.That(engine.Update(0f, 5000f, true, false, 0.02f), Is.LessThan(0f));
        }

        [Test]
        public void Engine_NoTorqueWithClutchOpen()
        {
            var engine = new VehicleEngine(settings);
            Assert.That(engine.Update(1f, 3000f, clutchEngaged: false, shifting: false, 0.02f), Is.EqualTo(0f));
        }

        [Test]
        public void Transmission_ManualShiftsThroughNeutralToReverseOnlyWhenStopped()
        {
            var gearbox = new VehicleTransmission(settings) { Mode = TransmissionMode.Manual };
            Assert.That(gearbox.Gear, Is.EqualTo(1));

            Assert.That(gearbox.ShiftDown(10f), Is.True);
            Assert.That(gearbox.Gear, Is.EqualTo(VehicleTransmission.Neutral));
            Assert.That(gearbox.ShiftDown(10f), Is.False, "reverse must be refused while moving forward");
            Assert.That(gearbox.ShiftDown(0f), Is.True);
            Assert.That(gearbox.Gear, Is.EqualTo(VehicleTransmission.Reverse));
            Assert.That(gearbox.OverallRatio, Is.LessThan(0f));
        }

        [Test]
        public void Transmission_CannotShiftAboveTopGear()
        {
            var gearbox = new VehicleTransmission(settings) { Mode = TransmissionMode.Manual };
            for (int i = 0; i < 20; i++)
                gearbox.ShiftUp(30f);
            Assert.That(gearbox.Gear, Is.EqualTo(settings.ForwardGearCount));
        }

        [Test]
        public void Transmission_AutomaticUpshiftsAndDownshifts()
        {
            var gearbox = new VehicleTransmission(settings);
            gearbox.Tick(1f);
            gearbox.UpdateAutomatic(settings.upshiftRpm + 100f);
            Assert.That(gearbox.Gear, Is.EqualTo(2));
            Assert.That(gearbox.IsShifting, Is.True);

            gearbox.UpdateAutomatic(settings.upshiftRpm + 100f);
            Assert.That(gearbox.Gear, Is.EqualTo(2), "no new shift until the current one finishes");

            gearbox.Tick(1f);
            gearbox.UpdateAutomatic(settings.downshiftRpm - 100f);
            Assert.That(gearbox.Gear, Is.EqualTo(1));
        }

        [Test]
        public void Transmission_ShiftCutsDriveForShiftDuration()
        {
            var gearbox = new VehicleTransmission(settings) { Mode = TransmissionMode.Manual };
            gearbox.ShiftUp(10f);
            Assert.That(gearbox.IsShifting, Is.True);
            gearbox.Tick(settings.shiftDuration + 0.01f);
            Assert.That(gearbox.IsShifting, Is.False);
        }
    }
}
