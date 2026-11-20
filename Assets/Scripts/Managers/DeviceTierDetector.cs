using UnityEngine;
using UnityEngine.Rendering;

namespace RealisticCarRacing.Managers
{
    /// <summary>
    /// First-launch guess at the right graphics preset from hardware facts. It is a heuristic, not a compatibility
    /// guarantee: the player can always change preset in the graphics menu, and only devices that have actually been
    /// benchmarked (see PERFORMANCE.md) have measured results.
    /// </summary>
    public static class DeviceTierDetector
    {
        public const int Low = 0, Medium = 1, High = 2;

        public readonly struct DeviceFacts
        {
            public readonly bool isMobile;
            public readonly int systemMemoryMB;
            public readonly int graphicsMemoryMB;
            public readonly int processorCount;
            public readonly int shaderLevel;
            public readonly GraphicsDeviceType api;

            public DeviceFacts(bool isMobile, int systemMemoryMB, int graphicsMemoryMB, int processorCount, int shaderLevel, GraphicsDeviceType api)
            {
                this.isMobile = isMobile;
                this.systemMemoryMB = systemMemoryMB;
                this.graphicsMemoryMB = graphicsMemoryMB;
                this.processorCount = processorCount;
                this.shaderLevel = shaderLevel;
                this.api = api;
            }

            public static DeviceFacts Current => new DeviceFacts(Application.isMobilePlatform, SystemInfo.systemMemorySize,
                SystemInfo.graphicsMemorySize, SystemInfo.processorCount, SystemInfo.graphicsShaderLevel, SystemInfo.graphicsDeviceType);
        }

        public static int Recommend() => Recommend(DeviceFacts.Current);

        public static int Recommend(DeviceFacts device)
        {
            // OpenGL ES devices, very little memory or old shader models: start low.
            if (device.api == GraphicsDeviceType.OpenGLES3 || device.systemMemoryMB < 3500 || device.shaderLevel < 45)
                return Low;
            if (device.isMobile)
                return device.systemMemoryMB >= 5500 && device.processorCount >= 8 ? Medium : Low;
            // Desktop/laptop: High needs plenty of RAM and a capable GPU; otherwise Medium.
            return device.systemMemoryMB >= 12000 && device.graphicsMemoryMB >= 3000 && device.processorCount >= 6 ? High : Medium;
        }
    }
}
