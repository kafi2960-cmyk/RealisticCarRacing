using System.Collections.Generic;
using UnityEngine;

namespace RealisticCarRacing.Vehicles
{
    /// <summary>Merges many small mesh renderers into one renderer per material, to cut draw calls.</summary>
    public static class RendererMerger
    {
        /// <summary>
        /// Replaces every MeshRenderer below <paramref name="root"/> (not on it) with one merged renderer per material,
        /// parented to <paramref name="root"/> so the group still moves and rotates as one. Returns the new meshes,
        /// which the caller owns.
        /// </summary>
        public static List<Mesh> MergeChildren(Transform root)
        {
            var groups = new Dictionary<Material, List<CombineInstance>>();
            var order = new List<Material>();
            var originals = new List<GameObject>();
            Matrix4x4 toRoot = root.worldToLocalMatrix;

            foreach (var renderer in root.GetComponentsInChildren<MeshRenderer>(true))
            {
                if (renderer.transform == root || !renderer.TryGetComponent(out MeshFilter filter) || filter.sharedMesh == null)
                    continue;
                var material = renderer.sharedMaterial;
                if (!groups.TryGetValue(material, out var list))
                {
                    groups[material] = list = new List<CombineInstance>();
                    order.Add(material);
                }
                list.Add(new CombineInstance { mesh = filter.sharedMesh, transform = toRoot * renderer.transform.localToWorldMatrix });
                originals.Add(renderer.gameObject);
            }

            var meshes = new List<Mesh>();
            foreach (var material in order)
            {
                var mesh = new Mesh { name = $"{root.name} merged" };
                mesh.CombineMeshes(groups[material].ToArray(), true, true);
                mesh.UploadMeshData(true); // GPU copy only: frees the CPU-side vertex data
                meshes.Add(mesh);

                var merged = new GameObject($"Merged {(material != null ? material.name : "default")}");
                merged.transform.SetParent(root, false);
                merged.layer = root.gameObject.layer;
                merged.AddComponent<MeshFilter>().sharedMesh = mesh;
                merged.AddComponent<MeshRenderer>().sharedMaterial = material;
            }

            foreach (var original in originals)
                Object.DestroyImmediate(original);
            return meshes;
        }
    }
}
