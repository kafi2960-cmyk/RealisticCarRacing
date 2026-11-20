using System;
using RealisticCarRacing.Vehicles;
using UnityEngine;

namespace RealisticCarRacing.Cameras
{
    public enum CameraView
    {
        Chase,
        Hood,
        Cockpit
    }

    [Serializable]
    public struct ChaseCameraSettings
    {
        public float distance;
        public float minDistance;
        public float maxDistance;
        [Tooltip("Camera height = Base Height + distance × Height Per Metre, so zooming out also rises.")]
        public float baseHeight;
        public float heightPerMetre;
        public float lookAtHeight;
        [Tooltip("How quickly the camera swings round behind the car (per second).")]
        public float headingSharpness;
        [Tooltip("How quickly distance changes settle (per second).")]
        public float zoomSharpness;
        [Tooltip("Maximum look-around angle from the orbit input, degrees.")]
        public float orbitRange;
        public float orbitSharpness;
        public float fieldOfView;
        public float maxFieldOfView;
        public float fieldOfViewMaxSpeedKmh;
        [Tooltip("Radius used to keep the camera out of walls and buildings.")]
        public float collisionRadius;

        public static ChaseCameraSettings Default => new ChaseCameraSettings
        {
            distance = 6.5f, minDistance = 4f, maxDistance = 14f, baseHeight = 0.9f, heightPerMetre = 0.2f, lookAtHeight = 1f,
            headingSharpness = 4f, zoomSharpness = 6f, orbitRange = 150f, orbitSharpness = 5f,
            fieldOfView = 60f, maxFieldOfView = 72f, fieldOfViewMaxSpeedKmh = 220f, collisionRadius = 0.3f
        };
    }

    [Serializable]
    public struct MountedCameraSettings
    {
        [Tooltip("Car-local position used when the car has no VehicleCameraMounts.")]
        public Vector3 fallbackOffset;
        public float fieldOfView;
        [Tooltip("Smooths body vibration out of the view (per second); higher is stiffer.")]
        public float rotationSharpness;
        [Tooltip("0 = horizon tilts fully with the car body, 1 = horizon stays level.")]
        [Range(0f, 1f)] public float horizonLevelling;
        public float nearClip;
    }

    /// <summary>
    /// Player camera with chase, hood and cockpit views. Chase follows the car's heading smoothly, zooms
    /// between min/max distance, orbits on request and pulls in front of walls; hood and cockpit are attached
    /// to the car's <see cref="VehicleCameraMounts"/> with lightly smoothed rotation.
    /// Driven by <see cref="Controls.PlayerCameraInput"/> (or any script) through its public methods.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public sealed class VehicleCameraRig : MonoBehaviour
    {
        const float ChaseNearClip = 0.3f;
        const float MinimumCameraClearance = 0.6f;

        [SerializeField] Transform target;
        [SerializeField] CameraView[] cycleOrder = { CameraView.Chase, CameraView.Hood, CameraView.Cockpit };
        [SerializeField] CameraView startView = CameraView.Chase;
        [SerializeField] ChaseCameraSettings chase = ChaseCameraSettings.Default;
        [SerializeField] MountedCameraSettings hood = new MountedCameraSettings
        {
            fallbackOffset = new Vector3(0f, 1f, 1f), fieldOfView = 62f, rotationSharpness = 25f, horizonLevelling = 0.2f, nearClip = 0.05f
        };
        [SerializeField] MountedCameraSettings cockpit = new MountedCameraSettings
        {
            fallbackOffset = new Vector3(-0.32f, 1.08f, -0.25f), fieldOfView = 65f, rotationSharpness = 30f, horizonLevelling = 0.3f, nearClip = 0.03f
        };
        [SerializeField] LayerMask collisionMask = ~0;

        readonly RaycastHit[] hits = new RaycastHit[16];
        Camera cam;
        Rigidbody targetBody;
        VehicleCameraMounts mounts;
        Vector3 chaseHeading = Vector3.forward;
        float currentDistance;
        float targetDistance;
        float orbitInput;
        float orbitAngle;
        Quaternion mountedRotation = Quaternion.identity;
        bool snapNextFrame = true;

        public CameraView View { get; private set; }
        /// <summary>Chase distance the camera is settling towards.</summary>
        public float ChaseDistance => targetDistance;
        public float MinChaseDistance => chase.minDistance;
        public float MaxChaseDistance => chase.maxDistance;
        public Transform Target => target;
        public event Action<CameraView> ViewChanged;

        void Awake()
        {
            cam = GetComponent<Camera>();
            View = startView;
            // Other cars passing between the camera and the player shouldn't make the camera jump in.
            int vehicles = LayerMask.NameToLayer(TestVehicleFactory.VehicleLayer);
            if (vehicles >= 0)
                collisionMask &= ~(1 << vehicles);
            targetDistance = currentDistance = chase.distance;
            SetTarget(target);
        }

        public void SetTarget(Transform newTarget)
        {
            if (mounts != null)
                mounts.SetInteriorVisible(false);
            target = newTarget;
            targetBody = target != null ? target.GetComponent<Rigidbody>() : null;
            mounts = target != null ? target.GetComponent<VehicleCameraMounts>() : null;
            snapNextFrame = true;
            UpdateInteriorVisibility();
        }

        // The cabin interior is only worth drawing from the driver's seat.
        void UpdateInteriorVisibility()
        {
            if (mounts != null)
                mounts.SetInteriorVisible(View == CameraView.Cockpit);
        }

        public void CycleView()
        {
            int index = Array.IndexOf(cycleOrder, View);
            SetView(cycleOrder[(index + 1) % cycleOrder.Length]);
        }

        public void SetView(CameraView view)
        {
            if (view == View)
                return;
            View = view;
            snapNextFrame = true; // cut between views; smoothing only applies within a view
            UpdateInteriorVisibility();
            ViewChanged?.Invoke(view);
        }

        /// <summary>Changes the chase distance by <paramref name="metres"/> (negative = closer), clamped to the allowed range.</summary>
        public void AdjustDistance(float metres) =>
            targetDistance = Mathf.Clamp(targetDistance + metres, chase.minDistance, chase.maxDistance);

        /// <summary>Look-around input for the chase view, -1 .. 1; returns to centre when released.</summary>
        public void SetOrbitInput(float value) => orbitInput = Mathf.Clamp(value, -1f, 1f);

        void LateUpdate()
        {
            if (target == null)
                return;

            float dt = Time.deltaTime;
            switch (View)
            {
                case CameraView.Hood:
                    UpdateMounted(hood, mounts != null ? mounts.Hood : null, dt);
                    break;
                case CameraView.Cockpit:
                    UpdateMounted(cockpit, mounts != null ? mounts.Cockpit : null, dt);
                    break;
                default:
                    UpdateChase(dt);
                    break;
            }
            snapNextFrame = false;
        }

        void UpdateChase(float dt)
        {
            Vector3 forward = Vector3.ProjectOnPlane(target.forward, Vector3.up);
            if (forward.sqrMagnitude > 0.001f)
                forward.Normalize();
            else
                forward = chaseHeading;

            if (snapNextFrame)
            {
                chaseHeading = forward;
                currentDistance = targetDistance;
                orbitAngle = orbitInput * chase.orbitRange;
            }
            else
            {
                chaseHeading = Vector3.Slerp(chaseHeading, forward, Sharpness(chase.headingSharpness, dt));
                currentDistance = Mathf.Lerp(currentDistance, targetDistance, Sharpness(chase.zoomSharpness, dt));
                orbitAngle = Mathf.Lerp(orbitAngle, orbitInput * chase.orbitRange, Sharpness(chase.orbitSharpness, dt));
            }

            Vector3 direction = Quaternion.AngleAxis(orbitAngle, Vector3.up) * chaseHeading;
            Vector3 focus = target.position + Vector3.up * chase.lookAtHeight;
            Vector3 desired = target.position - direction * currentDistance + Vector3.up * (chase.baseHeight + currentDistance * chase.heightPerMetre);
            Vector3 position = KeepOutOfGeometry(focus, desired);

            transform.SetPositionAndRotation(position, Quaternion.LookRotation(focus - position, Vector3.up));
            cam.nearClipPlane = ChaseNearClip;
            float speedKmh = targetBody != null ? targetBody.linearVelocity.magnitude * 3.6f : 0f;
            cam.fieldOfView = Mathf.Lerp(chase.fieldOfView, chase.maxFieldOfView, speedKmh / chase.fieldOfViewMaxSpeedKmh);
        }

        /// <summary>Pulls the camera towards the car if a wall, barrier or building is in between.</summary>
        Vector3 KeepOutOfGeometry(Vector3 focus, Vector3 desired)
        {
            Vector3 offset = desired - focus;
            float length = offset.magnitude;
            if (length < 0.01f)
                return desired;

            Vector3 direction = offset / length;
            int count = Physics.SphereCastNonAlloc(focus, chase.collisionRadius, direction, hits, length, collisionMask, QueryTriggerInteraction.Ignore);
            float clear = length;
            for (int i = 0; i < count; i++)
            {
                var hit = hits[i];
                // Skip the car itself, and anything the sphere already overlapped at the focus point.
                if ((targetBody != null && hit.rigidbody == targetBody) || hit.distance <= 0f)
                    continue;
                clear = Mathf.Min(clear, hit.distance);
            }
            return focus + direction * Mathf.Max(clear, MinimumCameraClearance);
        }

        void UpdateMounted(MountedCameraSettings settings, Transform mount, float dt)
        {
            Vector3 position = mount != null ? mount.position : target.TransformPoint(settings.fallbackOffset);
            Quaternion mountRotation = mount != null ? mount.rotation : target.rotation;
            Vector3 up = Vector3.Slerp(mountRotation * Vector3.up, Vector3.up, settings.horizonLevelling);
            Quaternion desired = Quaternion.LookRotation(mountRotation * Vector3.forward, up);
            mountedRotation = snapNextFrame ? desired : Quaternion.Slerp(mountedRotation, desired, Sharpness(settings.rotationSharpness, dt));

            transform.SetPositionAndRotation(position, mountedRotation);
            cam.nearClipPlane = settings.nearClip;
            cam.fieldOfView = settings.fieldOfView;
        }

        static float Sharpness(float sharpness, float dt) => 1f - Mathf.Exp(-sharpness * dt);
    }
}
