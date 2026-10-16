using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

class Program
{
    static void Main(string[] args)
    {
        string path = args.Length >= 1 ? args[0]
            : Path.Combine("loadtest", "k6_results.json");
        if (!File.Exists(path))
        {
            Console.WriteLine($"file not found: {path}");
            return;
        }

        // scenario -> durations (ms, already k6 value in ms)
        var durs = new Dictionary<string, List<double>>();
        // scenario -> status code -> count
        var status = new Dictionary<string, Dictionary<int, int>>();
        // scenario -> request tag (tags["read"]) -> durations
        var byTag = new Dictionary<string, Dictionary<string, List<double>>>();

        using var sr = new StreamReader(path);
        long lines = 0;
        string? line;
        while ((line = sr.ReadLine()) != null)
        {
            lines++;
            if (lines % 500000 == 0)
                Console.Error.WriteLine($"  read {lines:N0} lines...");

            // Cheap pre-filter before paying JSON parse cost.
            if (!line.Contains("\"http_req_duration\"") || !line.Contains("\"Point\"")) continue;

            using var doc = JsonDocument.Parse(line);
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object) continue;
            string type = root.TryGetProperty("type", out var t) ? t.GetString() ?? "" : "";
            if (type != "Point") continue;
            if (!root.TryGetProperty("metric", out var m)) continue;
            string metric = m.GetString() ?? "";
            if (metric != "http_req_duration") continue;
            // value + tags are nested under "data"
            if (!root.TryGetProperty("data", out var data)) continue;
            if (!data.TryGetProperty("value", out var v)) continue;
            if (!data.TryGetProperty("tags", out var tags) || tags.ValueKind != JsonValueKind.Object)
                continue;
            if (!tags.TryGetProperty("scenario", out var sc)) continue;
            string? scenario = sc.GetString();
            if (string.IsNullOrEmpty(scenario)) continue;

            double durVal = v.GetDouble();
            if (durVal < 0 || durVal >= 120000) continue; // filter outliers > 2 min
            if (!durs.TryGetValue(scenario, out var list))
            { list = new List<double>(400_000); durs[scenario] = list; }
            list.Add(durVal);

            // Per-endpoint breakdown: cached_reads_500.js tags each request with
            // r=availability|summary|occupancy; concurrency_booking.js uses read=.
            string rt = "";
            if (tags.TryGetProperty("r", out var r0) && r0.ValueKind == JsonValueKind.String) rt = r0.GetString() ?? "";
            else if (tags.TryGetProperty("read", out var rd) && rd.ValueKind == JsonValueKind.String) rt = rd.GetString() ?? "";
            if (rt.Length > 0)
            {
                if (!byTag.TryGetValue(scenario, out var tagMap))
                { tagMap = new Dictionary<string, List<double>>(); byTag[scenario] = tagMap; }
                if (!tagMap.TryGetValue(rt, out var tlist))
                { tlist = new List<double>(); tagMap[rt] = tlist; }
                tlist.Add(durVal);
            }

            // status code is in tags.status (string like "200")
            if (tags.TryGetProperty("status", out var st))
            {
                string stStr = st.GetString() ?? "";
                if (int.TryParse(stStr, out int code))
                {
                    if (!status.TryGetValue(scenario, out var map))
                    { map = new Dictionary<int, int>(); status[scenario] = map; }
                    if (map.TryGetValue(code, out int c)) map[code] = c + 1;
                    else map[code] = 1;
                }
            }
        }
        Console.Error.WriteLine($"done reading {lines:N0} lines");

        string[] order = { "burst_same_slot", "concurrent_logins", "dashboard_fanout", "cached_reads" };
        var seen = new List<string>();
        foreach (var k in durs.Keys) if (!order.Contains(k)) order = order.Append(k).ToArray();
        Console.WriteLine();
        Console.WriteLine("=====================================================================");
        Console.WriteLine("  k6 load test results");
        Console.WriteLine("=====================================================================");

        // Per-request-tag breakdown (tags["read"] = availability/summary/occupancy)
        foreach (var sc in order)
        {
            if (!durs.TryGetValue(sc, out var list)) continue;
            if (!byTag.ContainsKey(sc)) continue;
            Console.WriteLine();
            Console.WriteLine($"─── {sc} (per endpoint) ───");
            foreach (var tk in byTag[sc].Keys.OrderBy(x => x))
            {
                var arr = byTag[sc][tk].ToArray();
                Array.Sort(arr);
                int n = arr.Length;
                double p50 = n > 0 ? arr[(int)(0.50 * n)] : -1;
                double p95 = n > 0 ? arr[(int)(0.95 * n)] : -1;
                double p99 = n > 0 ? arr[(int)(0.99 * n)] : -1;
                double mx  = n > 0 ? arr[n - 1] : -1;
                double mean = n > 0 ? arr.Average() : -1;
                Console.WriteLine($"  {tk,-14} n={n:N0}  p50={p50:0.0}ms  p95={p95:0.0}ms  p99={p99:0.0}ms  max={mx:0.0}ms  mean={mean:0.0}ms");
            }
        }

        foreach (var sc in order)
        {
            Console.WriteLine();
            Console.WriteLine($"─── {sc} ───");
            if (durs.TryGetValue(sc, out var list))
            {
                var arr = list.ToArray();
                Array.Sort(arr);
                int n = arr.Length;
                double mean = arr.Average();
                double p50 = Pctile(arr, 50);
                double p95 = Pctile(arr, 95);
                double p99 = Pctile(arr, 99);
                double mx  = arr[n - 1];
                Console.WriteLine($"  n={n:N0}  p50={p50:0.0}ms  p95={p95:0.0}ms  p99={p99:0.0}ms  max={mx:0.0}ms  mean={mean:0.0}ms");
            }
            else Console.WriteLine("  (no duration data)");

            if (status.TryGetValue(sc, out var map))
            {
                var parts = map.Select(kv => $"{kv.Key}={kv.Value:N0}")
                               .OrderBy(s => s)
                               .ToArray();
                Console.WriteLine($"  status: {string.Join("  ", parts)}");
            }
        }

        Console.WriteLine();
        Console.WriteLine("─────────────────────────────────────────────────────────────────────");
        Console.WriteLine("  k6 thresholds (from script):");
        Console.WriteLine("  • burst_same_slot  p95 < 500ms");
        Console.WriteLine("  • dashboard_fanout p95 < 1000ms");
        Console.WriteLine("  • http_req_failed  < 5% overall");
        Console.WriteLine("─────────────────────────────────────────────────────────────────────");

        static double Pctile(double[] sorted, double p)
        {
            if (sorted.Length == 0) return -1;
            int idx = (int)(p / 100.0 * sorted.Length);
            if (idx >= sorted.Length) idx = sorted.Length - 1;
            return sorted[idx];
        }
    }
}
