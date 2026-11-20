using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace RealisticCarRacing.Tracks
{
    /// <summary>
    /// Draws many copies of one mesh (trees, rocks...) with GPU instancing instead of thousands of GameObjects.
    /// Instances are grouped into spatial chunks that are frustum- and distance-culled; lowering
    /// <see cref="Density"/> draws a random subset (instances are shuffled when baked).
    /// Decoration only: instances have no colliders, so keep them outside the drivable area.
    /// </summary>
    [ExecuteAlways]
    public sealed class InstancedScatter : MonoBehaviour
    {
        [Serializable]
        public sealed class Chunk
        {
            public Bounds bounds;
            public Matrix4x4[] matrices;
        }

        [SerializeField] Mesh mesh;
        [Tooltip("One material per sub-mesh. Materials must have GPU instancing enabled.")]
        [SerializeField] Material[] materials;
        [SerializeField] Chunk[] chunks = Array.Empty<Chunk>();
        [SerializeField] float drawDistance = 900f;
        [SerializeField, Range(0f, 1f)] float density = 1f;
        [Tooltip("Chunks within this distance of the camera cast shadows; 0 = no shadows.")]
        [SerializeField] float shadowDistance = 150f;

        readonly Plane[] frustumPlanes = new Plane[6];

        public float DrawDistance { get => drawDistance; set => drawDistance = value; }
        public float Density { get => density; set => density = Mathf.Clamp01(value); }
        public float ShadowDistance { get => shadowDistance; set => shadowDistance = Mathf.Max(0f, value); }
        public int InstanceCount
        {
            get
            {
                int total = 0;
                foreach (var chunk in chunks)
                    total += chunk.matrices.Length;
                return total;
            }
        }

        public void Configure(Mesh instanceMesh, Material[] subMeshMaterials, Chunk[] instanceChunks)
        {
            mesh = instanceMesh;
            materials = subMeshMaterials;
            chunks = instanceChunks;
        }

        void Update()
        {
            if (mesh == null || materials == null || chunks.Length == 0)
                return;

            // In edit mode there is no single gameplay camera, so draw everything for the Scene view.
            Camera cullingCamera = Application.isPlaying ? Camera.main : null;
            if (cullingCamera != null)
                GeometryUtility.CalculateFrustumPlanes(cullingCamera, frustumPlanes);
            float maxDistanceSqr = drawDistance * drawDistance;

            float shadowDistanceSqr = shadowDistance * shadowDistance;
            for (int subMesh = 0; subMesh < mesh.subMeshCount && subMesh < materials.Length; subMesh++)
            {
                // Two variants: only chunks near the camera are submitted to the shadow passes, because every
                // instanced draw that casts shadows is repeated once per shadow cascade.
                var withShadows = new RenderParams(materials[subMesh]) { shadowCastingMode = ShadowCastingMode.On, receiveShadows = true, layer = gameObject.layer };
                var withoutShadows = new RenderParams(materials[subMesh]) { shadowCastingMode = ShadowCastingMode.Off, receiveShadows = true, layer = gameObject.layer };

                foreach (var chunk in chunks)
                {
                    float distanceSqr = 0f;
                    if (cullingCamera != null)
                    {
                        distanceSqr = chunk.bounds.SqrDistance(cullingCamera.transform.position);
                        if (distanceSqr > maxDistanceSqr)
                            continue;
                        if (!GeometryUtility.TestPlanesAABB(frustumPlanes, chunk.bounds))
                            continue;
                    }

                    int count = Mathf.CeilToInt(chunk.matrices.Length * density);
                    if (count > 0)
                        Graphics.RenderMeshInstanced(distanceSqr < shadowDistanceSqr ? withShadows : withoutShadows, mesh, subMesh, chunk.matrices, count);
                }
            }
        }

        void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.2f, 0.8f, 0.2f, 0.5f);
            foreach (var chunk in chunks)
                Gizmos.DrawWireCube(chunk.bounds.center, chunk.bounds.size);
        }
    }
}
