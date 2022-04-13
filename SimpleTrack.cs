using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

// todo
// - add # satellites used in fix?
// - resolve difference in speeds seen? km/hr and land speed knots are not always the same
// - add heading info?


namespace Lomont.Gps
{
    // keep track of GPS path items of interest
    // merges info from a few messages to get the best track possible
    public class SimpleTrack : IEnumerable<SimpleTrack.Node>
    {

        /// <summary>
        /// Groups various messages in the stream into a nicer format
        /// </summary>
        /// <param name="Position"></param>
        /// <param name="Mode"></param>
        /// <param name="Utc"></param>
        /// <param name="Valid"></param>
        /// <param name="GroundSpeedKnots"></param>
        /// <param name="System"></param>
        public record Node(Location Position, GnsMessage.FaaMode Mode, DateTime Utc, bool Valid, double GroundSpeedKnots, GnsSystem System);


        /// <summary>
        /// Load messages
        /// </summary>
        /// <param name="messages"></param>
        public SimpleTrack(IEnumerable<Message> messages)
        {
            this.messages = messages;
        }

        /// <summary>
        /// Load from a file
        /// </summary>
        /// <param name="filename"></param>
        public SimpleTrack(string filename)
        {
            messages = new GnsDecoder.MessageEnumerator(
                File.ReadLines(filename)
                );
        }

        readonly IEnumerable<Message> messages;

        public IEnumerator<Node> GetEnumerator()
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

            Node node = null;
            bool differentialPaired = false; // have not yet seen this
            bool differentialMissed = false; // have not yet seen this

#if DEBUG
            // debug types
            Dictionary<string, int> counts = new();
#endif

            foreach (var msg in messages)
            {
#if DEBUG
                var gns = msg as GnsMessage;
                if (gns == null)
                {
                    Trace.TraceError($"Invalid GNS message");
                    continue;
                }
                var name = gns.GnsMessageType.ToString();
                if (!counts.ContainsKey(name))
                    counts.Add(name, 0);
                counts[name]++;
#endif

                if (msg is RmcGnsMessage rmc)
                {
                    // RMC has no height info.... get from other message soon
                    Trace.Assert(rmc.FaaMode.HasValue); // todo - test? what if happens? ignore?
                    if (node != null)
                        yield return node;

                    node =
                        new(rmc.position, rmc.FaaMode.Value, rmc.Utc, rmc.Valid, rmc.groundSpeedKnots,
                        GnsSystem.UNKNOWN);
                }
                else if (msg is GgaGnsMessage gga)
                {
                    // GGA does not have year, month, day, field
                    if (node != null)
                    {
                        // RMC,hhmmss.ss
                        // GGA,hhmmss.ss
                        var n = node;
                        Check1(gga.position, n.Position, false);
                        Check2(gga.Utc, n.Utc, false);
                        Check4(gga.GpsFixQuality, n.Mode);

                        // geoid separation varies by location on earth, often around -34m in usa
                        //Trace.WriteLine($"Geoid: {gga.geoidSeparation} and Ortho: {gga.orthometricHeight}");

                        // put in height
                        node = n with { Position = new Location(n.Position.Latitude, n.Position.Longitude, gga.position.Height) };

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

                    if (node != null)
                    {
                        var n = node;
                        Check1(gll.position, n.Position, false);
                        Check2(gll.Utc, n.Utc, false);
                        Check3(gll.Valid, n.Valid);
                        Trace.Assert(gll.FaaMode.HasValue);
                        Check5(gll.FaaMode.Value, n.Mode, "GLL");

                        node = n with { System = gll.GnsSystem };

                    }

                    //g.position;
                    //g.FaaMode; // todo - describe
                    //g.Utc;
                    //g.Valid;
                    //g.GnsSystem; // 
                }
                else if (msg is VtgGnsMessage vtg)
                {
                    if (node != null)
                    {
                        var n = node;
                        Trace.Assert(vtg.FaaMode.HasValue);
                        Check5(vtg.FaaMode.Value, n.Mode, "VTG");
                        Check6(vtg.SpeedOverGroundKnots, n.GroundSpeedKnots);
                    }

                    // for some reason, the ratio of speeds is slightly different
                    //Trace.WriteLine($"VTG {vtg.SpeedOverGroundKnots/vtg.SpeedOverGroundKmPerHour}");

                }
            }


#if DEBUG
            foreach (var p in counts.OrderBy(p => p.Value))
            {
                Trace.WriteLine($"  {p.Value}: {p.Key}");
            }
#endif
            // todo - sanity check, remove missing items, check all fields make sense
            if (node != null)
                yield return node; // last node

            node = null;

            //foreach (var p in Nodes)
            //{
            //    if (p.Position.Height != 0)
            //        Trace.WriteLine(p.Position.Height);
            //}
            
            #region Helpers

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
                    t1 = t1.AddMonths(t2.Month - t1.Month);
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
                Trace.Assert(false, $"FAA modes {t1} and {t2} not compared");
            }

            void Check5(GnsMessage.FaaMode t1, GnsMessage.FaaMode t2, string msgType)
            {
                if (t1 == GnsMessage.FaaMode.Differential && t2 == GnsMessage.FaaMode.Differential && !differentialPaired)
                {
                    Trace.WriteLine("Double differential seen");
                    differentialPaired = true;
                }

                if (t1 != t2)
                {
                    //Trace.Assert(t1 == t2);
                    // get differential compared to rtk float and rtk integer sometimes
                    if (!differentialMissed || (t1 != GnsMessage.FaaMode.Differential && t2 != GnsMessage.FaaMode.Differential))
                    {
                        Trace.TraceWarning($"mismatched GPS type {t1} != {t2} from msg {msgType}");
                    }

                    if (t1 == GnsMessage.FaaMode.Differential || t2 == GnsMessage.FaaMode.Differential)
                        differentialMissed = true;
                }
            }

            // check doubles are bitwise exact
            void Check6(double v1, double v2)
            {
                Trace.Assert(v1 == v2); // check bitwise!
            }
            #endregion
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }
}
