using System;
using System.Collections.Generic;
using Unity.Profiling;
using UnityEngine;

namespace RealisticCarRacing.Managers
{
    /// <summary>Summary of a measured period. Counters that the build doesn't expose are -1.</summary>
    [Serializable]
    public sealed class PerformanceReport
    {
        public string label;
        public string qualityLevel;
        public string device;
        public string graphicsDevice;
        public string resolution;
        public float seconds;
        public int frames;
        public float averageFps;
        public float medianFrameMs;
        public float p95FrameMs;
        public float p99FrameMs;
        public float onePercentLowFps;
        public float medianCpuMainThreadMs = -1f;
        public float medianGpuMs = -1f;
        public float averageBatches = -1f;
        public float averageSetPassCalls = -1f;
        public float averageDrawCalls = -1f;
        public float averageTrianglesThousands = -1f;
        public float totalUsedMemoryMB = -1f;
        public float textureMemoryMB = -1f;
        public float meshMemoryMB = -1f;
        public float managedHeapMB = -1f;
        public float averageGcAllocBytesPerFrame = -1f;
        public float sceneLoadSeconds = -1f;
    }

    /// <summary>
    /// Collects frame times (wall clock, CPU main thread and GPU via <see cref="FrameTimingManager"/>) and render/memory
    /// counters via <see cref="ProfilerRecorder"/>. Render counters are only available in development builds and the editor.
    /// </summary>
    public sealed class PerformanceSampler : IDisposable
    {
        readonly List<float> frameMs = new List<float>(4096);
        readonly List<float> cpuMs = new List<float>(4096);
        readonly List<float> gpuMs = new List<float>(4096);
        readonly FrameTiming[] timings = new FrameTiming[1];
        readonly (ProfilerRecorder recorder, string name)[] renderCounters;
        readonly double[] renderSums;
        ProfilerRecorder totalMemory, textureMemory, meshMemory, managedHeap, gcAllocInFrame;
        double gcAllocSum;
        float started;

        public PerformanceSampler()
        {
            renderCounters = new[]
            {
                (ProfilerRecorder.StartNew(ProfilerCategory.Render, "Batches Count"), "batches"),
                (ProfilerRecorder.StartNew(ProfilerCategory.Render, "SetPass Calls Count"), "setpass"),
                (ProfilerRecorder.StartNew(ProfilerCategory.Render, "Draw Calls Count"), "draws"),
                (ProfilerRecorder.StartNew(ProfilerCategory.Render, "Triangles Count"), "triangles")
            };
            renderSums = new double[renderCounters.Length];
            totalMemory = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "Total Used Memory");
            textureMemory = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "Texture Memory");
            meshMemory = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "Mesh Memory");
            managedHeap = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC Reserved Memory");
            gcAllocInFrame = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC Allocated In Frame");
            started = Time.realtimeSinceStartup;
        }

        public int FrameCount => frameMs.Count;

        /// <summary>Call once per rendered frame.</summary>
        public void SampleFrame()
        {
            frameMs.Add(Time.unscaledDeltaTime * 1000f);
            FrameTimingManager.CaptureFrameTimings();
            if (FrameTimingManager.GetLatestTimings(1, timings) > 0)
            {
                if (timings[0].cpuMainThreadFrameTime > 0) cpuMs.Add((float)timings[0].cpuMainThreadFrameTime);
                if (timings[0].gpuFrameTime > 0) gpuMs.Add((float)timings[0].gpuFrameTime);
            }
            for (int i = 0; i < renderCounters.Length; i++)
            {
                if (renderCounters[i].recorder.Valid)
                    renderSums[i] += renderCounters[i].recorder.LastValue;
            }
            if (gcAllocInFrame.Valid)
                gcAllocSum += gcAllocInFrame.LastValue;
        }

        public PerformanceReport Report(string label)
        {
            var report = new PerformanceReport
            {
                label = label,
                qualityLevel = QualitySettings.names[QualitySettings.GetQualityLevel()],
                device = $"{SystemInfo.deviceModel} · {SystemInfo.processorType} · {SystemInfo.systemMemorySize} MB RAM",
                graphicsDevice = $"{SystemInfo.graphicsDeviceName} ({SystemInfo.graphicsDeviceType})",
                resolution = $"{Screen.width}x{Screen.height}",
                seconds = Time.realtimeSinceStartup - started,
                frames = frameMs.Count
            };
            if (frameMs.Count == 0)
                return report;

            var sorted = new List<float>(frameMs);
            sorted.Sort();
            float total = 0f;
            foreach (float ms in frameMs)
                total += ms;
            report.averageFps = 1000f * frameMs.Count / total;
            report.medianFrameMs = Percentile(sorted, 0.5f);
            report.p95FrameMs = Percentile(sorted, 0.95f);
            report.p99FrameMs = Percentile(sorted, 0.99f);
            report.onePercentLowFps = 1000f / report.p99FrameMs;
            if (cpuMs.Count > 0) report.medianCpuMainThreadMs = Median(cpuMs);
            if (gpuMs.Count > 0) report.medianGpuMs = Median(gpuMs);

            float frames = frameMs.Count;
            if (renderCounters[0].recorder.Valid) report.averageBatches = (float)(renderSums[0] / frames);
            if (renderCounters[1].recorder.Valid) report.averageSetPassCalls = (float)(renderSums[1] / frames);
            if (renderCounters[2].recorder.Valid) report.averageDrawCalls = (float)(renderSums[2] / frames);
            if (renderCounters[3].recorder.Valid) report.averageTrianglesThousands = (float)(renderSums[3] / frames / 1000.0);
            if (totalMemory.Valid) report.totalUsedMemoryMB = totalMemory.LastValue / 1048576f;
            if (textureMemory.Valid) report.textureMemoryMB = textureMemory.LastValue / 1048576f;
            if (meshMemory.Valid) report.meshMemoryMB = meshMemory.LastValue / 1048576f;
            if (managedHeap.Valid) report.managedHeapMB = managedHeap.LastValue / 1048576f;
            if (gcAllocInFrame.Valid) report.averageGcAllocBytesPerFrame = (float)(gcAllocSum / frames);
            return report;
        }

        static float Median(List<float> values)
        {
            var sorted = new List<float>(values);
            sorted.Sort();
            return Percentile(sorted, 0.5f);
        }

        static float Percentile(List<float> sorted, float p) => sorted[Mathf.Clamp(Mathf.CeilToInt(p * sorted.Count) - 1, 0, sorted.Count - 1)];

        public void Dispose()
        {
            foreach (var (recorder, _) in renderCounters)
                recorder.Dispose();
            totalMemory.Dispose();
            textureMemory.Dispose();
            meshMemory.Dispose();
            managedHeap.Dispose();
            gcAllocInFrame.Dispose();
        }
    }
}
