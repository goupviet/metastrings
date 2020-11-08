using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace metastrings
{
    public static class ScopeTiming
    {
        public static void Init(bool enable)
        {
            sm_enabled = enable;
        }

        public static Stopwatch StartTiming()
        {
            if (!sm_enabled)
                return null;

            return Stopwatch.StartNew();
        }

        public static void RecordScope(string scope, Stopwatch sw)
        {
            if (sw == null)
                return;

            lock (sm_timings)
            {
                Scope scopeObj;
                if (!sm_timings.TryGetValue(scope, out scopeObj))
                {
                    scopeObj = new Scope() { ScopeName = scope };
                    sm_timings.Add(scope, scopeObj);
                }

                ++scopeObj.Hits;
                scopeObj.Allotted += sw.Elapsed;
            }

            sw.Restart();
        }

        public static string Summary
        {
            get
            {
                var sb = new List<string>();
                lock (sm_timings)
                {
                    foreach (Scope obj in sm_timings.Values)
                    {
                        if (obj.Hits == 0)
                            continue;

                        sb.Add
                        (
                            $"{obj.ScopeName} -> {obj.Hits} hits - " +
                            $"{Math.Round(obj.Allotted.TotalMilliseconds)} ms total -> " +
                            $"{Math.Round((double)obj.Allotted.TotalMilliseconds / obj.Hits)} ms avg"
                        );
                    }
                }
                sb.Sort();
                return string.Join("\r\n", sb);
            }
        }

        public static void Clear()
        {
            lock (sm_timings)
                sm_timings.Clear();
        }

        private static bool sm_enabled;

        private class Scope
        {
            public string ScopeName;
            public int Hits;
            public TimeSpan Allotted;
        }
        private static Dictionary<string, Scope> sm_timings = new Dictionary<string, Scope>();
    }
}
