using UnityEngine;

namespace RealisticCarRacing.Audio
{
    /// <summary>
    /// The player's car in its surroundings: wind that builds with speed (quieter from the cockpit), the horn while
    /// its button is held, impacts scaled by how hard the car hits (rate-limited, light or heavy recordings), and a
    /// scrape loop while the car slides along a barrier. Collision callbacks arrive here because the component sits
    /// on the car's Rigidbody.
    /// </summary>
    public sealed class EnvironmentAudio : MonoBehaviour
    {
        const float MinImpactSpeed = 1.5f, HeavyImpactSpeed = 8f, FullImpactSpeed = 15f;
        const float ImpactInterval = 0.12f;

        VehicleAudioManager manager;
        VehicleAudioProfile profile;
        AudioSource wind, horn, scrape, impacts;
        float lastImpact = -1f, scrapeTarget, scrapeLevel, hornLevel;

        public float Wind { get; private set; }
        public float Horn => hornLevel;
        public float Scrape => scrapeLevel;
        public int ImpactsPlayed { get; private set; }
        public float LastImpactVolume { get; private set; }

        public void Initialise(VehicleAudioManager owner)
        {
            manager = owner;
            profile = owner.Profile;
            wind = manager.CreateLoop("Wind", AudioCategory.Environment, profile.wind, new Vector3(0f, 1.2f, 0.5f));
            horn = manager.CreateLoop("Horn", AudioCategory.Environment, profile.horn, new Vector3(0f, 0.5f, 2f));
            scrape = manager.CreateLoop("Scrape", AudioCategory.Environment, profile.scrape, new Vector3(0f, 0.4f, 0f));
            impacts = manager.CreateOneShot("Impacts", AudioCategory.Environment, new Vector3(0f, 0.5f, 0f));
        }

        void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f)
                return;
            float speedKmh = manager.Vehicle.SpeedKmh;
            float windTarget = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(40f, 230f, speedKmh)) * (manager.Interior ? 0.35f : 1f);
            Wind = VehicleAudioManager.Ease(Wind, windTarget, 2f, 2f, dt);
            if (wind != null)
            {
                wind.volume = Wind * 0.7f;
                wind.pitch = 0.85f + speedKmh / 400f;
            }

            hornLevel = VehicleAudioManager.Ease(hornLevel, manager.Vehicle.CurrentInput.Horn ? 1f : 0f, 25f, 12f, dt);
            if (horn != null)
                horn.volume = hornLevel * 0.9f;

            scrapeLevel = VehicleAudioManager.Ease(scrapeLevel, scrapeTarget, 10f, 6f, dt);
            if (scrape != null)
            {
                scrape.volume = scrapeLevel * 0.8f;
                scrape.pitch = 0.85f + scrapeLevel * 0.3f;
            }
        }

        void OnCollisionEnter(Collision collision)
        {
            float speed = ImpactSpeed(collision);
            if (speed < MinImpactSpeed || Time.time - lastImpact < ImpactInterval)
                return;
            lastImpact = Time.time;
            ImpactsPlayed++;
            LastImpactVolume = Mathf.Clamp01(speed / FullImpactSpeed);
            var set = speed >= HeavyImpactSpeed && profile.heavyImpacts.Length > 0 ? profile.heavyImpacts : profile.lightImpacts;
            if (set.Length == 0)
                set = profile.heavyImpacts;
            if (set.Length == 0)
                return;
            impacts.transform.position = collision.GetContact(0).point;
            impacts.pitch = Random.Range(0.92f, 1.08f);
            impacts.PlayOneShot(set[Random.Range(0, set.Length)], LastImpactVolume);
        }

        void OnCollisionStay(Collision collision)
        {
            // Sliding along something: speed along the contact surface.
            var contact = collision.GetContact(0);
            Vector3 along = Vector3.ProjectOnPlane(collision.relativeVelocity, contact.normal);
            scrapeTarget = Mathf.InverseLerp(2f, 25f, along.magnitude);
        }

        void OnCollisionExit(Collision collision) => scrapeTarget = 0f;

        /// <summary>Closing speed along the contact normal (a glancing touch is quiet, a head-on hit loud).</summary>
        static float ImpactSpeed(Collision collision)
        {
            if (collision.contactCount == 0)
                return collision.relativeVelocity.magnitude;
            return Mathf.Abs(Vector3.Dot(collision.relativeVelocity, collision.GetContact(0).normal));
        }
    }
}
