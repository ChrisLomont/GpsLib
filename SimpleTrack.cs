using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Microsoft.VisualBasic;

// todo
// - add # satellites used in fix?
// - resolve difference in speeds seen? km/hr and land speed knots are not always the same
// - add heading info?


namespace Lomont.Gps
{
    /// <summary>
    /// keep track of GPS path items of interest
    /// merges info from a few messages to get the best track possible
    /// </summary>
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
        public record Node(Location Position, FaaMode Mode, DateTime Utc, bool Valid, double GroundSpeedKnots, GnsSystem System);


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
             *
             * From inspection, example had GLL (last group position),
             * then RMC (with new position),
             * then VTG
             * then GGA (same pos as RMC)
             *
             * Read these into local values, export one when enough consistent values read
             *
             * So we read RMC (same info as GLL?) for first check,
             *    then check GGA has same
             *    then check GLL has same
             *
             *    as sanity check.
             *
             * How this works:
             * 1. gather messages till enough present
             * 2. if all have consistent info, output a node saying so
             *
             */

            bool differentialPaired = false; // have not yet seen this
            bool differentialMissed = false; // have not yet seen this

#if DEBUG
            // debug types
            Dictionary<string, int> counts = new();
#endif

            // message types we aggregate 
            RmcGnsMessage rmc = null;
            GgaGnsMessage gga = null;
            GllGnsMessage gll = null;
            VtgGnsMessage vtg = null;

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
                // set each if possible cast
                if (msg is RmcGnsMessage m1)
                    rmc = m1;
                if (msg is GgaGnsMessage m2)
                    gga = m2;
                else if (msg is GllGnsMessage m3)
                    gll = m3;
                else if (msg is VtgGnsMessage m4)
                    vtg = m4;


                if (rmc != null && gga != null && gll != null && vtg != null)
                {
                    if (CompareLocations(gga.position,rmc.position) && CompareLocations(gga.position, gll.position))
                    { // candidate for message sync, check all fields

                        var success = true;
                        Trace.Assert(rmc.FaaMode.HasValue);
                        Trace.Assert(gll.FaaMode.HasValue);
                        Trace.Assert(vtg.FaaMode.HasValue);
                        success &= PrintOnFail(CompareDouble(rmc.groundSpeedKnots, vtg.SpeedOverGroundKnots),$"Inconsistent knots");
                        success &= PrintOnFail(CompareTime(rmc.Utc, gga.Utc),$"Inconsistent time RMC {rmc.Utc.ToString("yyyy'-'MM'-'dd'T'HH':'mm':'ss'.'fff'Z'")} to GGA {gga.Utc.ToString("yyyy'-'MM'-'dd'T'HH':'mm':'ss'.'fff'Z'")}");
                        success &= PrintOnFail(CompareTime(rmc.Utc, gll.Utc), $"Inconsistent time RMC {rmc.Utc.ToString("yyyy'-'MM'-'dd'T'HH':'mm':'ss'.'fff'Z'")} to GLL {gga.Utc.ToString("yyyy'-'MM'-'dd'T'HH':'mm':'ss'.'fff'Z'")}");
                        success &= PrintOnFail(CompareBool(rmc.Valid, gll.Valid),$"Inconsistent validity");
                        
                        // vtg and gll always seem to have Differential on the ZED-F9P
                        //success &= PrintOnFail(CompareFaaModes(rmc.FaaMode.Value, gll.FaaMode.Value, "GLL"),$"Inconsistent FAA Mode RMC {rmc.FaaMode.Value} GLL {gll.FaaMode.Value}");
                        //success &= PrintOnFail(CompareFaaModes(rmc.FaaMode.Value, vtg.FaaMode.Value,"VTG"), $"Inconsistent FAA Mode RMC {rmc.FaaMode.Value} VTG {vtg.FaaMode.Value}");

                        success &= PrintOnFail(CompareDouble(rmc.groundSpeedKnots, vtg.SpeedOverGroundKnots),$"Inconsistent knots");

                        Check4(gga.GpsFixQuality, rmc.FaaMode.Value);

                        // Notes:
                        // VTG FAA Mode almost never matches for some reason
                        // VTG ratio of speed over ground knots and KmPerHour has varying ratio for some reason
                        // GLL does not have year, month, day, field
                        // GGA does not have year, month, day, field
                        // GGA geoid separation varies by location on earth, often around -34m in usa
                        // Trace.WriteLine($"Geoid: {gga.geoidSeparation} and Ortho: {gga.orthometricHeight}");

                        if (success)
                        {  // output a node
                            yield return new Node(
                                gga.position, // Note: RMC does not have height
                                rmc.FaaMode.Value, rmc.Utc, rmc.Valid,
                                vtg.SpeedOverGroundKnots, 
                                rmc.GnsSystem
                                );

                            // reset all
                            rmc = null;
                            gll = null;
                            gga = null;
                            vtg = null;
                        }
                        else
                        {
                            Trace.TraceError($"Message inconsistency");
                        }

                        bool PrintOnFail(bool val, string msg)
                        {
                            if (!val) Trace.TraceError(msg);
                            return val;
                        }
                    }

                }
            }


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
            
#region Helpers

            bool CompareLocations(Location l1, Location l2)
            {
                var d = l1.ToVec3() - l2.ToVec3();
                //if (!compareHeight)
                    d.Z = 0; // assume not checking height
                //Trace.Assert(d.LengthSquared == 0);
                return d.LengthSquared == 0;
            }

            bool CompareTime(DateTime t1, DateTime t2)
            {
                var useDate = false;
                if (!useDate)
                {
                    t1 = t1.AddYears(t2.Year - t1.Year);
                    t1 = t1.AddDays(t2.Day - t1.Day);
                    t1 = t1.AddMonths(t2.Month - t1.Month);
                }

                var d = t1 - t2;
                //Trace.Assert(d.Ticks == 0);
                return d.Ticks == 0;
            }
            bool CompareBool(bool b1, bool b2)
            {
                Trace.Assert(b1 == b2);
                return b1 == b2;
            }
            
            void Check4(GpsFixQuality t2, FaaMode t1)
            {
                if (
                    (t1 == FaaMode.Estimated && t2 == GpsFixQuality.Estimated) ||
                    (t1 == FaaMode.Differential && t2 == GpsFixQuality.Differential) ||
                    (t1 == FaaMode.RtkInteger && t2 == GpsFixQuality.RealTimeKinematic) ||
                    (t1 == FaaMode.RtkFloat && t2 == GpsFixQuality.FloatRTK)
                    )
                {
                    return;
                }
                Trace.TraceWarning($"FAA modes {t1} and {t2} not compared");
            }

            bool CompareFaaModes(FaaMode t1, FaaMode t2, string msgType)
            {
                if (t1 == FaaMode.Differential && t2 == FaaMode.Differential && !differentialPaired)
                {
                    Trace.WriteLine("GPS double differential seen");
                    differentialPaired = true;
                    return false;
                }

                if (t1 != t2)
                {
                    //Trace.Assert(t1 == t2);
                    // get differential compared to rtk float and rtk integer sometimes
                    if (!differentialMissed || (t1 != FaaMode.Differential && t2 != FaaMode.Differential))
                    {
                        Trace.TraceWarning($"mismatched GPS type {t1} != {t2} from msg {msgType}");
                    }

                    if (t1 == FaaMode.Differential || t2 == FaaMode.Differential)
                        differentialMissed = true;
                    return false;
                }

                return true;
            }

            // check doubles are bitwise exact
            bool CompareDouble(double v1, double v2)
            {
                Trace.Assert(v1 == v2); // check bitwise!
                return v1 == v2;
            }
#endregion
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }
}
