using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.Profiling;
using UnityEditorInternal;
using UnityEngine;

namespace MafiaNight.UI.Kit.Editor
{
    /// <summary>
    /// Editor-only helper for performance work: records the profiler for a few seconds in play mode and says which markers cost the most on the main thread
    /// and which scripts allocate garbage every frame. Driven from the MCP bridge (<see cref="Start"/>, then <see cref="Report"/>).
    /// </summary>
    public static class PerfProbe
    {
        public static string Start()
        {
            ProfilerDriver.ClearAllFrames();
            ProfilerDriver.profileEditor = false;
            ProfilerDriver.enabled = true;
            return "recording";
        }

        public static string Report(int top = 22, bool totals = false)
        {
            ProfilerDriver.enabled = false;
            int first = ProfilerDriver.firstFrameIndex, last = ProfilerDriver.lastFrameIndex;
            var inclusive = new Dictionary<string, double>();
            var selfTime = new Dictionary<string, double>();
            var gcBytes = new Dictionary<string, long>();
            var gcCount = new Dictionary<string, int>();
            int frames = 0; double mainMs = 0;
            for (int f = first; f <= last; f++)
            {
                using (var v = ProfilerDriver.GetRawFrameDataView(f, 0))
                {
                    if (v == null || !v.valid) continue;
                    frames++; mainMs += v.frameTimeMs;
                    var stack = new string[64];
                    var ends = new List<int>();
                    for (int i = 0; i < v.sampleCount; i++)
                    {
                        string name = v.GetSampleName(i);
                        while (ends.Count > 0 && i > ends[ends.Count - 1]) ends.RemoveAt(ends.Count - 1);
                        int depth = Mathf.Min(ends.Count, 63);
                        int below = v.GetSampleChildrenCount(i);
                        if (below > 0) ends.Add(i + below);
                        stack[depth] = name;
                        float ms = v.GetSampleTimeMs(i);
                        if (depth >= 1) { inclusive.TryGetValue(name, out var a); inclusive[name] = a + ms; }
                        // self time: this sample minus its children
                        float childMs = 0f;
                        for (int j = i + 1, end = i + below; below > 0 && j <= end; j += 1 + v.GetSampleChildrenCount(j)) childMs += v.GetSampleTimeMs(j);
                        selfTime.TryGetValue(name, out var s); selfTime[name] = s + (ms - childMs);
                        if (name == "GC.Alloc")
                        {
                            long bytes = v.GetSampleMetadataAsLong(i, 0);
                            string owner = depth >= 2 ? stack[depth - 2] + " > " + stack[depth - 1] : depth >= 1 ? stack[depth - 1] : "?";
                            gcBytes.TryGetValue(owner, out var b); gcBytes[owner] = b + bytes;
                            gcCount.TryGetValue(owner, out var c); gcCount[owner] = c + 1;
                        }
                    }
                }
            }
            var sb = new StringBuilder();
            double div = totals ? 1.0 : System.Math.Max(1, frames);
            sb.AppendLine("frames " + frames + " · main thread " + (frames > 0 ? mainMs / frames : 0).ToString("0.00") + " ms/frame · " + (totals ? "TOTALS over the recording" : "per frame"));
            sb.AppendLine("— self time per frame (ms), top " + top + ":");
            foreach (var kv in selfTime.OrderByDescending(k => k.Value).Take(top)) sb.AppendLine("  " + (kv.Value / div).ToString("0.000") + "  " + kv.Key);
            sb.AppendLine("— garbage per frame (bytes / allocations), top " + top + " owners:");
            foreach (var kv in gcBytes.OrderByDescending(k => k.Value).Take(top)) sb.AppendLine("  " + (kv.Value / div).ToString("0") + " B  " + (gcCount[kv.Key] / div).ToString("0.0") + " x  " + kv.Key);
            return sb.ToString();
        }
    }
}
