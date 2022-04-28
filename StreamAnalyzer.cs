using Lomont.Numerical;
using Lomont.Stats;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Lomont.Gps
{
    /// <summary>
    /// Class that dumps analysis of a file to Trace outputs
    /// Can be passed NMEA file or RTCM file 
    /// </summary>
    public class StreamAnalyzer
    {

        public static void Analyze2(List<Message> messages)
        {
            DetectClusters(messages, 0.05);
        }


        public class Stat
        {
            public double Min = Double.MaxValue;
            public double Max = Double.MinValue;
            public double Avg = 0.0;
            public int Count = 0;

            /// <summary>
            /// call per sample, sums in avg
            /// </summary>
            /// <param name="val"></param>
            public void Add(double val)
            {
                Avg += val;
                Count++;
                Min = Math.Min(val, Min);
                Max = Math.Max(val, Max);
            }

            // call when done
            public void End()
            {
                Avg /= Count;
            }

            public override string ToString()
            {
                //{ Avg: F10}
                return $"<[{Min:F10},{Max:F10}]({Count})";
            }
        }
        public class Run
        {
            public DateTime Utc = DateTime.MinValue;

            public int Len => positions.Count;

            public Stat Lat = new Stat();
            public Stat Lng = new Stat();

            readonly List<Vec3> positions = new List<Vec3>();
            Vec3 posSum = new Vec3(), center = new Vec3();

            public bool InBounds(Location position, double tolerance)
            {
                if (Len == 0)
                    return true;

                var newPos = Wgs84.GeodeticToEcef(position);
                return (newPos - center).Length < tolerance;
            }

            public void Add(Location position)
            {
                var newPos = Wgs84.GeodeticToEcef(position);
                positions.Add(newPos);
                posSum += newPos;
                center = posSum / Len;
                Lat.Add(position.Latitude);
                Lng.Add(position.Longitude);
            }

            public override string ToString()
            {
                return $"{Lat.Avg:F10},{Lng.Avg:F10} : Lat: {Lat}, Lng: {Lng}";
            }
        }

        // scan messages, look for low or no motion clusters
        // tolerance is size of cluster in meters
        public static void DetectClusters(List<Message> messages, double tolerance = 0.02)
        {
            // work on GGA messages
            //var gga = new List<GgaGnsMessage>();
            //foreach (var m in messages)
            //    if (m is GgaGnsMessage g && !g.HasNaN)
            //        gga.Add(g);

            DateTime lastDate = DateTime.MinValue;
            // scan
            var runs = new List<Run>();
            var curRun = new Run();
            foreach (var m in messages)
            {
                if (m is RmcGnsMessage rmc && !String.IsNullOrEmpty(rmc.Date))
                    lastDate = rmc.Utc;

                if (
                    !(m is GgaGnsMessage g) ||
                    g.HasNaN ||
                    g.GpsFixQuality != GpsFixQuality.RealTimeKinematic
                    )
                    continue; // only gga past here

                // add to current run if possible
                if (curRun.InBounds(g.position, tolerance))
                {
                    if (curRun.Utc == DateTime.MinValue)
                        curRun.Utc = lastDate;
                    curRun.Add(g.position); // continue
                }
                else
                { // end of run
                    // for now - get clusters of about the length I sit at a corner
                    if (4 <= curRun.Len && curRun.Len < 30)
                        runs.Add(curRun);
                    curRun = new Run { Utc = lastDate };
                }
            }

            foreach (var run1 in runs)
            {
                run1.Lat.End();
                run1.Lng.End();
            }

            // now clump these, then print colored
            var clumps = new List<List<Run>>();
            var used = new List<bool>();
            for (var i = 0; i < runs.Count; ++i)
                used.Add(false);
            for (var i = 0; i < runs.Count; ++i)
            {
                if (used[i]) continue;
                var m = runs[i];
                var r1 = new List<Run>();
                clumps.Add(r1);
                for (var j = i; j < runs.Count; ++j)
                    if (!used[j] && Distance(m, runs[j]) < tolerance)
                    {
                        r1.Add(runs[j]);
                        used[j] = true;
                    }
            }

            // sort clumps by count
            clumps.Sort(
                (a, b) =>
                    a.Sum(ar => ar.Len).CompareTo(b.Sum(br => br.Len))
                );

            var flip = true;
            foreach (var c in clumps)
            {
                Console.ForegroundColor = flip ? ConsoleColor.Yellow : ConsoleColor.Cyan;
                flip = !flip;
                foreach (var run1 in c)
                {
                    //Console.WriteLine($"Run: {run1.Utc}: {run1}");
                    Console.Write($"{run1.Lat.Avg},{run1.Lng.Avg},");
                }
            }


            double Distance(Run r1, Run r2)
            {
                var lat1 = r1.Lat.Avg;
                var lng1 = r1.Lng.Avg;
                var lat2 = r2.Lat.Avg;
                var lng2 = r2.Lng.Avg;
                return Wgs84.Distance(lat1, lng1, lat2, lng2);
            }
        }


        // stats analysis of all files in a directory
        public static (int successful, int failed) Parse(string path, List<Message> messages, string pattern, string type, Func<string, List<Message>, (int, int)> parser)
        {

            var (successful, failed) = (0, 0);
            foreach (var filename in Directory.EnumerateFiles(path, pattern))
            {
                var (successful1, failed1) = parser(filename, messages);
                successful += successful1;
                failed += failed1;
            }
            Trace.WriteLine($"{type} {successful} success, {failed} fail");
            return (successful, failed);

        }



        public static void TestRtcm(string fn)
        {
            Trace.Listeners.Add(new ConsoleTraceListener());
            var msg = new List<Message>();
            Trace.Assert(File.Exists(fn));
            var (success, fail) = RtcmDecoder.Parse(fn, msg);
            Console.WriteLine($"RTCM {success} success, {fail} fail, {msg.Count} messages");

            Dictionary<Type, int> counts = new();
            foreach (var m in msg)
            {
                var t = m.GetType();
                if (!counts.ContainsKey(t))
                    counts.Add(t, 0);
                counts[t]++;
            }

            foreach (var t in counts.Keys.OrderBy(t => t.Name))
            {
                Console.WriteLine($"{t.Name} -> {counts[t]}");
            }

        }

        static void GpsFileStats(string filename)
        {

            Tally<GnsMessageType> tallyType = new();

            ConsoleTraceListener cw = new();
            Trace.Listeners.Add(cw);

            var messages = new List<Message>();
            var (success, failed) = GnsDecoder.Parse(filename, messages);
            Console.WriteLine($"{filename} had {success} successes and {failed} failures");

            foreach (var m in messages)
            {
                var m1 = m as GnsMessage;
                var t = m1.GnsMessageType;
                tallyType.Add(t);
            }

            var scale = 10000; // steps per meter
            var htDelHist = new Histogram();
            var gga = messages.Where(m => m is GgaGnsMessage).OfType<GgaGnsMessage>().ToList();
            for (var i = 0; i < gga.Count - 1; ++i)
            {
                var cur = gga[i];
                var nxt = gga[i + 1];
                var del = nxt.position.Height - cur.position.Height;
                htDelHist.AddSample((int)(Math.Round(scale * del)));
            }
            Console.WriteLine($"height scale {scale}, del {htDelHist}");

            var posDelta = new Histogram();
            for (var i = 0; i < gga.Count - 1; ++i)
            {
                var cur = gga[i];
                var nxt = gga[i + 1];
                var p1 = nxt.position;
                var p2 = cur.position;
                p1.Height = p2.Height = 0; // remove height part
                var del = Wgs84.GeodeticToEcef(p1) - Wgs84.GeodeticToEcef(p2);
                var d = del.Length;
                posDelta.AddSample((int)(Math.Round(scale * d)));
            }
            Console.WriteLine($"position scale {scale}, del {posDelta}");

            var latDelta = new Histogram();
            var lngDelta = new Histogram();
            var lngScale = 100_000_000_000UL;
            for (var i = 0; i < gga.Count - 1; ++i)
            {
                var cur = gga[i];
                var nxt = gga[i + 1];
                var lngDel = nxt.position.Longitude - cur.position.Longitude;
                var latDel = nxt.position.Latitude - cur.position.Latitude;
                latDelta.AddSample((int)(Math.Round(latDel * lngScale)));
                lngDelta.AddSample((int)(Math.Round(lngDel * lngScale)));
            }
            Console.WriteLine($"Lat scale {lngScale}, del {latDelta}");
            Console.WriteLine($"Lng scale {lngScale}, del {lngDelta}");


            foreach (var (t, c) in tallyType)
            {
                Console.WriteLine($"{t} -> {c}");
            }

        }


        public static void Analyze(List<Message> messages)
        {

            var dict = new Dictionary<string, int>();
            foreach (var msg in messages)
            {
                if (msg is RtcmMessage m1)
                {
                    var key = m1.id.ToString();
                    if (!dict.ContainsKey(key))
                        dict.Add(key, 0);
                    dict[key]++;
                }
                else if (msg is GnsMessage m2)
                {
                    var key = m2.GnsMessageType.ToString();
                    if (!dict.ContainsKey(key))
                        dict.Add(key, 0);
                    dict[key]++;
                }
            }

            Trace.WriteLine($"{messages.Count} messages decoded");
            foreach (var (key, value) in dict)
            {
                Trace.WriteLine($"{key} -> {value}");
            }
            Trace.WriteLine($"{dict.Count} different types");

            foreach (var (key, value) in dict)
            {
                Trace.WriteLine($"{key} -> {value} ");
            }

#if false
            // small movement
            var lat = 0.0;
            var lng = 0.0;
            foreach (var m in messages)
            {
                if (m is GllGnsMessage g)
                {
                    if (!g.Latitude.HasValue)
                        continue;
                    var d1 = g.Latitude - lat;
                    var d2 = g.Longitude - lng;
                    if (d1 * d1 + d2 * d2 < 0.00001)
                    {
                        Trace.WriteLine($"{g.Utc} -> {d1} {d2}");
                    }

                    lat = g.Latitude.Value;
                    lng = g.Longitude.Value;
                }
            }

            // times between 223408 and 224540
            var sb = new StringBuilder();
            foreach (var m in messages)
            {
                if (m is GllGnsMessage g)
                {
                    if (!g.Latitude.HasValue)
                        continue;
                    if (g.Utc < 223408 || 224540 < g.Utc)
                        continue;
                    sb.AppendLine($"{g.Latitude},{g.Longitude},");
                }
            }
            File.WriteAllText("stationary.txt", sb.ToString());
#endif

        }



    }
}
