using UnityEngine;

namespace RealisticCarRacing.Vehicles
{
    /// <summary>Destroys runtime-created assets (tuned settings, paint materials) together with their car.</summary>
    public sealed class RuntimeAssetOwner : MonoBehaviour
    {
        Object[] owned = System.Array.Empty<Object>();

        public void Own(params Object[] assets) => owned = assets;

        void OnDestroy()
        {
            foreach (var asset in owned)
            {
                if (asset != null)
                    Destroy(asset);
            }
        }
    }
}
