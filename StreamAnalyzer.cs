using Lomont.Numerical;
using Lomont.Stats;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Lomont.Gps
{
    /// <summary>
    /// Class providing pieces to make analysis tools of GNS and RTCM streams
    /// Provides some initial analyzers
    /// Messages dumped to trace logs, so add a console listener to trace listerners
    /// </summary>
    public class StreamAnalyzer
    {

        /// <summary>
        /// Analyze files in path
        /// </summary>
        /// <param name="path"></param>
        /// <param name="filePattern"></param>
        public static Result AnalyzeFiles(string path, TextWriter output = null, string filePattern = null)
        {
            var reg = filePattern != null ? new Regex(filePattern, RegexOptions.IgnoreCase) : null;
            if (output == null)
                output = Console.Out;
            var result = new Result();
            foreach (var filename in Directory.GetFiles(path, "*.*"))
            {
                if (filePattern != null && !reg.IsMatch(Path.GetFileName(filename)))
                    continue; // no match
                var r2 = ReadFile(filename);
                Summarize(r2, output);
                result.Add(r2);
            }
            AnalyzeMessages(result,output);
            return result;           
        }

        /// <summary>
        /// Analyze a single file
        /// </summary>
        /// <param name="filename"></param>
        /// <param name="output"></param>
        /// <returns></returns>
        public static Result AnalyzeFile(string filename, TextWriter output = null)
        {
            if (output == null)
                output = Console.Out;
            if (!File.Exists(filename))
            {
                output.WriteLine($"File {filename} not present");
                return new Result();
            }
            var result = ReadFile(filename);

            Summarize(result,output);
            AnalyzeMessages(result, output);

            return result;
        }

        /// <summary>
        /// Read one file into a result structure
        /// </summary>
        /// <param name="filename"></param>
        /// <returns></returns>
        /// <exception cref="NotImplementedException"></exception>
        public static Result ReadFile(string filename)
        {
            var messages = new List<Message>();
            var fileType = DetectFiletype(filename);
            var (succ, fail) = fileType switch
            {
                FileType.Unknown => (0, 0),
                FileType.Gps => GnsDecoder.Parse(filename, messages),
                FileType.Rtcm => RtcmDecoder.Parse(filename, messages),
                _ => throw new NotImplementedException()
            };
            var r = new Result { successful = succ, failed = fail };
            r.Messages.AddRange(messages);

            r.MessagesByFile.Add(new(
                r.Messages,fileType,filename 
                ));
            return r;
        }

        /// <summary>
        /// Analyze a set of results
        /// </summary>
        /// <param name="result"></param>
        /// <param name="output"></param>
        public static void AnalyzeMessages(Result result, TextWriter output)
        {
            if (output == null)
                output = Console.Out;
            var messages = result.Messages;

            // output.WriteLine($"Messages: {r.successful} success, {r.failed} fail, {messages.Count} messages");

            TallyMessageTypes(messages,output);

            if (result.MessagesByFile.Count == 1 && result.MessagesByFile[0].FileType == FileType.Gps)
            {
                DetectClusters(messages, output); // todo - tolerance options
                GpsFileStats(messages, output);
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

        /// <summary>
        /// Show message types given a list of messages
        /// Return Dictionary of type name and count
        /// </summary>
        /// <param name="messages"></param>
        /// <param name="output">null to skip output</param>
        public static Dictionary<string,int> TallyMessageTypes(List<Message> messages, TextWriter output)
        {
            var dict = new Dictionary<string, int>();
            foreach (var msg in messages)
            {
                if (msg is RtcmMessage m1)
                {
                    var key = m1.MessageId.ToString();
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

            if (output != null)
            {
                output.WriteLine($"{messages.Count} message types");
                output.WriteLine("   Ordered by type");
                foreach (var (key, value) in dict.OrderBy(p => p.Key.ToString()))
                    output.WriteLine($"      {key} -> {value}");

                output.WriteLine("   Ordered by count");
                foreach (var (key, value) in dict.OrderBy(p => -p.Value))
                    output.WriteLine($"      {key} -> {value}");
            }
            return dict;
        }



        // brief summary of one file
        static void Summarize(Result result, TextWriter output)
        {
            var filename = result.MessagesByFile[0].filename;
            output.WriteLine($"Filename {filename}");
            output.WriteLine($"   Type {result.MessagesByFile[0].FileType}, {result.Messages.Count} messages: {result.successful} successful, {result.failed} failed");
        }

        // scan messages, look for low or no motion clusters
        // tolerance is size of cluster in meters
        public static void DetectClusters(List<Message> messages, TextWriter output, double tolerance = 0.02)
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
                //Console.ForegroundColor = flip ? ConsoleColor.Yellow : ConsoleColor.Cyan;
                flip = !flip;
                foreach (var run1 in c)
                {
                    //Console.WriteLine($"Run: {run1.Utc}: {run1}");
                    output.Write($"{run1.Lat.Avg},{run1.Lng.Avg},");
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

        class BinHistogram
        {
            public List<double> bins = new();
            public int [] counts;
            public BinHistogram( params double[] bins)
            {
                this.bins.AddRange(bins);
                this.bins.Add(double.MaxValue);
                counts = new int[this.bins.Count];
            }
            
            public void Add(double sample)
            {
                if (Double.IsNaN(sample))
                    return; // todo - warning
                var i = 0;
                while (bins[i] <= sample)
                    ++i;
                counts[i-1]++;
            }
        }


        public static void GpsFileStats(List<Message> messages, TextWriter output)
        {
            var gga = messages.Where(m => m is GgaGnsMessage).OfType<GgaGnsMessage>().ToList();

            // height delta analysis
            ComputeDeltas(
                gga, (cur,nxt)=>Math.Abs(cur.Height - nxt.Height), 
                "Height deltas in m", 
                output
                );

            // position delta analysis
            ComputeDeltas(
                gga, (cur, nxt) => (Wgs84.GeodeticToEcef(cur) - Wgs84.GeodeticToEcef(nxt)).Length,
                "Position deltas in m",
                output
                );

            // lat delta analysis
            var scale = 100_000;
            ComputeDeltas(
                gga, (cur, nxt) => Math.Abs((cur.Latitude - nxt.Latitude)*scale),
                $"Lat deltas, scale {scale}",
                output
                );
            // lng delta analysis
            ComputeDeltas(
                gga, (cur, nxt) => Math.Abs((cur.Longitude - nxt.Longitude) * scale),
                $"Lng deltas, scale {scale}",
                output
                );

            var (count,minLat) = MinNonzero((cur, nxt) => Math.Abs((cur.Latitude - nxt.Latitude)));
            output.WriteLine($"Min nonzero lat delta {minLat}, count {count}");
            var (count2, minLng) = MinNonzero((cur, nxt) => Math.Abs((cur.Longitude- nxt.Longitude)));
            output.WriteLine($"Min nonzero lng delta {minLng}, count {count2}");

            // digits of accuracy
            var minD = gga.Min(m=>m.DigitsOfAccuracy);
            var maxD = gga.Max(m => m.DigitsOfAccuracy);
            output.WriteLine($" lat/lng digits of accuracy min {minD}, max {maxD}");

            var minDh = gga.Min(m => m.HeightDigitsOfAccuracy);
            var maxDh = gga.Max(m => m.HeightDigitsOfAccuracy);
            output.WriteLine($" height digits of accuracy min {minDh}, max {maxDh}");

            // fix mode
            var mode = new Dictionary<GpsFixQuality,int>();
            foreach (var m in gga)
            {
                var f = m.GpsFixQuality;
                if (!mode.ContainsKey(f))
                    mode.Add(f, 0);
                mode[f]++;
            }
            output.WriteLine("Fix qualities ");
            foreach (var p in mode.OrderBy(p=>p.Key))
                output.WriteLine($"   {p.Key} -> {p.Value}");

            // msg rates
#if false
            SimpleTrack simpleTrack = new(messages);
            var t = simpleTrack.ToList();
            var startTime = t.First().Utc;
            var endTime = t.Last().Utc;
#else
            var startTime = gga.First().Utc;
            var endTime = gga.Last().Utc;
#endif
            var elapsedMs = (endTime - startTime).TotalMilliseconds;
            var dict = TallyMessageTypes(messages, null);
            output.WriteLine($"Message frequency (over {elapsedMs/1000:F3} elapsed seconds)");
            foreach (var p in dict.OrderBy(p=>p.Key))
            {
                var freq = 1000.0*p.Value / elapsedMs;
                output.WriteLine($"   {p.Key} count {p.Value} freq {freq:F1} hz");
            }
            


            (int,double) MinNonzero(Func<Location, Location, double> convert)
            {
                var min = double.MaxValue;
                int count = 0;
                for (var i = 0; i < gga.Count - 1; ++i)
                {
                    var cur = gga[i];
                    var nxt = gga[i + 1];
                    var val = convert(cur.position, nxt.position);
                    if (val > 0)
                    {
                        // val within 1% of min, count as same
                        var same = Math.Abs((val - min) / min) < 0.01;
                        if (same) ++count;
                        else if (val < min)
                        {
                            min = val;
                            count = 1;
                        }
                    }
                }
                return (count,min);
            }

            static void ComputeDeltas(List<GgaGnsMessage> gga, Func<Location, Location, double> convert, string msg, TextWriter output)
            {
                // height analysis
                BinHistogram deltas = new(
                    0.0, 0.01, 0.03, 0.05, 0.07, 0.10, 0.15, 0.20
                    );

                for (var i = 0; i < gga.Count - 1; ++i)
                {
                    var cur = gga[i];
                    var nxt = gga[i + 1];
                    var val = convert(cur.position,nxt.position);
                    deltas.Add(Math.Abs(val));
                }

                output.WriteLine(msg);
                for (var i = 0; i < deltas.counts.Length; ++i)
                    output.WriteLine($"   Up to {deltas.bins[i]} : {deltas.counts[i]}");

            }

        }

#region Helpers

        public class Result
        {
            public List<Message> Messages { get; } = new();
            public int successful = 0, failed = 0;

            public List<(List<Message> Messages, FileType FileType, string filename)> MessagesByFile { get; } = new();
            
            /// <summary>
            /// Merge parameter into this instance
            /// </summary>
            /// <param name="r"></param>
            public void Add(Result r)
            {
                Messages.AddRange(r.Messages);
                MessagesByFile.AddRange(r.MessagesByFile);
                failed += r.failed;
                successful += r.successful;
            }
        }

        public enum FileType { Unknown, Gps, Rtcm };
        public static FileType DetectFiletype(string filename)
        {
            if (GnsDecoder.DetectFile(filename))
                return FileType.Gps;
            if (RtcmDecoder.DetectFile(filename))
                return FileType.Rtcm;
            return FileType.Unknown;
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

#endregion
    }
}
