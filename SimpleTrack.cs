using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace Lomont.Gps
{
    // keep track of GPS path items of interest
    // merges info from a few messages to get the best track possible
    public class SimpleTrack
    {

        public record Node(Location Position, GnsMessage.FaaMode Mode, DateTime Utc, bool Valid, double GroundSpeedKnots, GnsSystem System);

        public List<Node> Nodes { get; } = new();


        /// <summary>
        /// Load messages
        /// </summary>
        /// <param name="messages"></param>
        public void FromMessages(IList<Message> messages)
        {
            /* most common messages, relative frequencies from a sample
             * 760 GSV - satellites in view
             * 155 GSA - GPS DOP and active satellites
             *  40 GLL - geographic position, 
             *  40 GGA - gps fix data: time, position, fix
             *  40 VTG - track made good - ground speed, course heading, knots, km/hr
             *  40 RMC - recommended min nav info
             *
             *
             * From inspection, example had GLL (last group position),
             * then RMC (with new position),
             * then VTG
             * then GGA (same pos as RMC)
             *
             * So we read RMC (same info as GLL?) for first check,
             *    then check GGA has same
             *    then check GLL has same
             *
             *    as sanity check.
             *
             */



#if DEBUG
            // debug types
            Dictionary<string, int> counts = new();
#endif

            foreach (var msg in messages)
            {
#if DEBUG
                var gns = msg as GnsMessage;
                var name = gns.GnsMessageType.ToString();
                if (!counts.ContainsKey(name))
                    counts.Add(name, 0);
                counts[name]++;
#endif

                if (msg is RmcGnsMessage rmc)
                {
                    // RMC has no height info.... get from other message soon
                    Trace.Assert(rmc.FaaMode.HasValue); // todo - test? what if happens? ignore?
                    Nodes.Add(new(rmc.position, rmc.FaaMode.Value, rmc.Utc, rmc.Valid, rmc.groundSpeedKnots, GnsSystem.UNKNOWN));
                }
                else if (msg is GgaGnsMessage gga)
                {
                    // GGA does not have year, month, day, field
                    if (Nodes.Count > 0)
                    {
                        // RMC,hhmmss.ss
                        // GGA,hhmmss.ss
                        var n = Nodes.Last();
                        Check1(gga.position, n.Position, false);
                        Check2(gga.Utc, n.Utc, false);
                        Check4(gga.GpsFixQuality, n.Mode);

                        // put in height
                        Nodes[Nodes.Count - 1] = n with { Position = new Location(n.Position.Latitude, n.Position.Longitude, gga.position.Height)};

                    }
                    //gga.position;
                    //gga.Utc;
                    //gga.GpsFixQuality;
                    //gga.SvCount;
                    //gga.HDOP;
                    //gga.orthometricHeight;
                    //gga.geoidSeparation;
                    //gga.dgpsAge;
                    //gga.refStationId;
                }
                else if (msg is GllGnsMessage gll)
                {
                    // GLL does not have year, month, day, field

                    if (Nodes.Count > 0)
                    {
                        var n = Nodes.Last();
                        Check1(gll.position, n.Position, false);
                        Check2(gll.Utc, n.Utc, false);
                        Check3(gll.Valid, n.Valid);
                        Trace.Assert(gll.FaaMode.HasValue);
                        Check5(gll.FaaMode.Value, n.Mode);

                        Nodes[Nodes.Count - 1] = n with {System = gll.GnsSystem};

                    }

                    //g.position;
                    //g.FaaMode; // todo - describe
                    //g.Utc;
                    //g.Valid;
                    //g.GnsSystem; // 
                }
                else if (msg is VtgGnsMessage vtg)
                {
                    if (Nodes.Count > 0)
                    {
                        var n = Nodes.Last();
                        Trace.Assert(vtg.FaaMode.HasValue);
                        Check5(vtg.FaaMode.Value, n.Mode);
                        Check6(vtg.SpeedOverGroundKnots, n.GroundSpeedKnots);
                    }
                    
                    // for some reason, the ratio of speeds is slightly different
                    //Trace.WriteLine($"VTG {vtg.SpeedOverGroundKnots/vtg.SpeedOverGroundKmPerHour}");

                }
            }
            
            // todo - sanity check, remove missing items, check all fields make sense

#if DEBUG
            foreach (var p in counts.OrderBy(p => p.Value))
            {
                Trace.WriteLine($"  {p.Value}: {p.Key}");
            }
#endif

            //foreach (var p in Nodes)
            //{
            //    if (p.Position.Height != 0)
            //        Trace.WriteLine(p.Position.Height);
            //}


            void Check1(Location l1, Location l2, bool compareHeight = true)
            {
                var d = l1.ToVec3() - l2.ToVec3();
                if (!compareHeight)
                    d.Z = 0;
                Trace.Assert(d.LengthSquared == 0);
            }

            void Check2(DateTime t1, DateTime t2, bool useDate = true)
            {
                if (!useDate)
                {
                    t1 = t1.AddYears(t2.Year - t1.Year);
                    t1 = t1.AddDays(t2.Day - t1.Day);
                    t1 = t1.AddMonths(t2.Month- t1.Month);
                }

                var d = t1 - t2;
                Trace.Assert(d.Ticks == 0);
            }
            void Check3(bool b1, bool b2)
            {
                Trace.Assert(b1 == b2);
            }
            void Check4(GpsFixQuality t2, GnsMessage.FaaMode t1)
            {
                if (
                    (t1 == GnsMessage.FaaMode.Estimated && t2 == GpsFixQuality.Estimated) ||
                    (t1 == GnsMessage.FaaMode.Differential && t2 == GpsFixQuality.Differential) ||
                    (t1 == GnsMessage.FaaMode.RtkInteger && t2 == GpsFixQuality.RealTimeKinematic) || 
                    (t1 == GnsMessage.FaaMode.RtkFloat && t2 == GpsFixQuality.FloatRTK)
                    )
                {
                    return;
                }
                Trace.Assert(false,$"Uncompared modes {t1} and {t2}");
                /*
                 * modes, * says mode in opposite item too
                 * FAA mode - GLL message
                 *
                            Autonomous, // A
                            * Differential, // D
                            * Estimated, // E dead reckoning
                            * RtkFloat, // F
                            * Manual, // M
                            NotValid, // N
                            Precise,  // P
                            RtkInteger,  // R
                            * Simulated,   // S
                 */
                /* GpsFixQuality  - GGA messages
                    NoFixAvailable = 0,
                    GPS,
                    * Differential,
                    PPS,
                    RealTimeKinematic = 4,
                    * FloatRTK = 5,
                    * Estimated,
                    * Manual,
                    * Simulation
                */
            }


            void Check5(GnsMessage.FaaMode t1, GnsMessage.FaaMode t2)
            {
                if (t1 != t2)
                {
                    //Trace.Assert(t1 == t2);
                    // get differential compared to rtk float and rtk integer sometimes
                    Trace.TraceWarning($"mismatched GPS type {t1} != {t2}");
                }
            }

            void Check6(double v1, double v2)
            {
                Trace.Assert(v1==v2); // check bitwise!
            }
        }

        /// <summary>
        /// Load from a file
        /// </summary>
        /// <param name="filename"></param>
        public void ReadFile(string filename)
        {
            List<Message> messages = new();
            var (successful, failed) = GnsDecoder.Parse(File.ReadLines(filename), messages);
            Trace.WriteLine($"GPS message reader: {successful} succeeded, {failed} failed");
            FromMessages(messages);
        }
    }
}
