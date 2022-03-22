using System;
using System.Collections.Generic;

namespace Lomont.Gps
{

    /// <summary>
    /// General GNS (GPS superset) messages
    /// </summary>
    public class GnsMessage : Message, IHasNaN
    {
        /*
        // good message info https://gpsd.gitlab.io/gpsd/NMEA.html

        "Most GPS sensors emit only RMC, GSA, GSV, GLL, VTG, and (rarely) ZDA. Newer ones conforming to NMEA 3.x may emit GBS as well. O"

        starts with '$' (other kinds possible...)
        5 letter address field (all caps?!)
        then multiple data fields comma separated
        end is '*' char, followed by 2 digit checksum
        which is the XOR of all characters between '$' and '*'
        CR LF end the message
         
$GNGSA,A,1,,,,,,,,,,,,,99.99,99.99,99.99,4*36
$GPGSV,2,1,05,03,,,35,04,,,43,16,,,42,26,,,35,1*67
$GPGSV,2,2,05,32,,,34,1*67
        trailing \r\n
         *
         */

        public string Text,Addr;
        public List<string> Fields;
        public GnsSystem GnsSystem = GnsSystem.UNKNOWN;
        public GnsMessageType GnsMessageType = GnsMessageType.UNKNOWN;

        public virtual bool HasNaN => false;

        #region Parsing

        public static double ParseDouble(string text)
        {
            var d = Double.NaN;
            if (!String.IsNullOrEmpty(text))
                d = Double.Parse(text);
            return d;
        }

        public static char ParseChar(string text, string allowed = null)
        {
            if (String.IsNullOrEmpty(text) || text.Length != 1)
                throw new ArgumentException($"{text} not one char in parse");
            if (allowed != null && !allowed.Contains(text))
                throw new ArgumentException($"{text} required in {allowed}");
            return text[0];
        }

        public static Location ParseLatLong(string latitudeText, string NS, string longitudeText, string EW, string heightText)
        {
            double DegFix(double datum)
            {
                var sign = datum >= 0 ? 1 : -1;
                var degMin = Math.Abs(datum) / 100.0; // to degrees.minutes
                var degrees = Math.Truncate(degMin);
                var minutes = degMin - degrees;
                var final = sign * (degrees + minutes / 0.60);
                return final;
            }

            // these of form mmdd.dddddddd
            var latitude = DegFix(ParseDouble(latitudeText));
            var longitude = DegFix(ParseDouble(longitudeText));
            var height = ParseDouble(heightText);
            if (String.IsNullOrEmpty(NS) || string.IsNullOrEmpty(EW) || NS.Length != 1 || EW.Length != 1)
            {
                latitude = longitude = height = Double.NaN; // invalid
            }
            else
            {
                if (NS == "S") latitude = -latitude;
                if (EW == "W") longitude = -longitude;
            }

            return new Location(latitude, longitude, height);
        }

        public static int ParseInt(string text, int min = Int32.MinValue, int max = Int32.MaxValue)
        {
            var v = Int32.Parse(text);
            if (v < min || max < v)
                throw new ArgumentOutOfRangeException($"{text} should be in {min},{max}");
            return v;
        }

        static string FaaText = "ADEFMNPRS";
        static FaaMode [] modes = 
        {
            FaaMode.Autonomous, // A
            FaaMode.Differential, // D
            FaaMode.Estimated, // E dead reckoning
            FaaMode.RtkFloat, // F
            FaaMode.Manual, // M
            FaaMode.NotValid, // N
            FaaMode.Precise,  // P
            FaaMode.RtkInteger,  // R
            FaaMode.Simulated,   // S
    };

        public static FaaMode? ParseFaaMode(string text)
        {
            if (String.IsNullOrEmpty(text) || text.Length != 1 || !FaaText.Contains(text))
                throw new ArgumentException($"{text} not one char in parse");
            return modes[FaaText.IndexOf(text[0])];
        }

        public static DateTime ParseUtc(string text)
        {
            // 213838.00
            var d = Double.Parse(text);
            var i = (int)Math.Truncate(d);
            var ms = (int)((d - i) * 1000);
            var s = i % 100;
            i /= 100;
            var m = i % 100;
            i /= 100;
            var h = i;
            var y = 1970; // todo - find correct baseline?
            var mon = 1;
            var day = 1;
            return new DateTime(y, mon, day, h, m, s, ms);
        }



        #endregion

        #region Types
        // FAA Mode Indicator
        // A = Autonomous mode
        // D = Differential Mode
        // E = Estimated (dead-reckoning) mode
        // F = RTK Float mode
        // M = Manual Input Mode
        // N = Data Not Valid
        // P = Precise (4.00 and later)
        // R = RTK Integer mode
        // S = Simulated Mode
        // fields must be empty when no data for it - some GPS units zero these out
        // Date and time in GPS is represented as number of weeks from the start of zero second of 6 January 1980, plus number of seconds into the week. 
        public enum FaaMode
        {
            Autonomous, // A
            Differential, // D
            Estimated, // E dead reckoning
            RtkFloat, // F
            Manual, // M
            NotValid, // N
            Precise,  // P
            RtkInteger,  // R
            Simulated,   // S
        }

        #endregion
    }


    // VTK - Track made good and ground speed
    //          1  2  3  4  5  6  7  8 9
    //          |  |  |  |  |  |  |  | |
    //  $--VTG,x.x,T,x.x,M,x.x,N,x.x,K*hh<CR><LF>
    // NMEA 2.3:
    //  $--VTG,x.x,T,x.x,M,x.x,N,x.x,K,m*hh<CR><LF>
    public class VtgGnsMessage : GnsMessage
    {

        public double CourseOverGroundDegreesTrue; // 0-359
        public double CourseOverGroundDegreesMagnetic; // 0-359
        public double SpeedOverGroundKnots; //0 to 99
        public double SpeedOverGroundKmPerHour; // 0 to 99
        public FaaMode? FaaMode;

        public static bool TryParse(string fieldsText, out VtgGnsMessage message)
        {
            message = null;
            if (String.IsNullOrEmpty(fieldsText))
                return false;
            try
            {
                var words = fieldsText.Split(',');
                if (words.Length != 9)
                    return false;

                message = new VtgGnsMessage
                {
                    CourseOverGroundDegreesTrue = ParseDouble(words[0]),
                    CourseOverGroundDegreesMagnetic= ParseDouble(words[2]),
                    SpeedOverGroundKnots= Double.Parse(words[4]),
                    SpeedOverGroundKmPerHour= Double.Parse(words[6]),
                    FaaMode = ParseFaaMode(words[8])
                };
                // todo - check faa mode in 9
            }
            catch (Exception ex)
            {
                return false;
            }
            return true;
        }
    }


    // GLL - Geographic Position - Latitude/Longitude
    // 	  1       2 3        4 5         6 7   8
    //    |       | |        | |         | |   |
    // $--GLL,llll.ll,a,yyyyy.yy,a,hhmmss.ss,a,m,*hh<CR><LF>
    public class GllGnsMessage : GnsMessage
    {

        public Location position;
        public DateTime Utc; // UTC time
        public bool Valid; // data was valid or invalid
        public FaaMode? FaaMode;

        // "4225.01231,N,08714.72123,W,221440.00,A,D"



        public static bool TryParse(string fieldsText, out GllGnsMessage message)
        {
            message = null;
            if (String.IsNullOrEmpty(fieldsText))
                return false;
            try
            {
                var words = fieldsText.Split(',');
                if (words.Length != 7)
                    return false;

                message = new GllGnsMessage
                {
                    position = ParseLatLong(words[0],words[1],words[2],words[3],"0"), // todo - height?
                    Utc = ParseUtc(words[4]),
                    Valid = ParseChar(words[5], "AV") == 'A',
                    FaaMode = ParseFaaMode(words[6])
                };
            }
            catch (Exception ex)
            {
                return false;
            }
            return true;
        }
    }

    // RMC - Recommended Minimum Navigation Information
    //                                                           12
    //        1         2 3       4 5        6  7   8   9    10 11|  13
    //        |         | |       | |        |  |   |   |    |  | |   |
    // $--RMC,hhmmss.ss,A,llll.ll,a,yyyyy.yy,a,x.x,x.x,xxxx,x.x,a,m,* hh<CR><LF>
    public class RmcGnsMessage : GnsMessage, IHasNaN
    {

        public DateTime Utc; // UTC time
        public bool Valid; // A = valid, V = warning
        public Location position;
        public double groundSpeedKnots; // NaN if missing
        // degrees gound...
        public string Date; // ddmmyy
        // magnetic variation....
        // E or W
        public FaaMode? FaaMode;
        public static bool TryParse(string fieldsText, out RmcGnsMessage message)
        {
            message = null;
            if (String.IsNullOrEmpty(fieldsText))
                return false;
            try
            {
                var words = fieldsText.Split(',');
                if (words.Length != 12 && words.Length != 13)
                    return false;

                message = new RmcGnsMessage
                {
                    Utc = ParseUtc(words[0]),
                    Valid = ParseChar(words[1], "AV") == 'A',
                    position = ParseLatLong(words[2],words[3],words[4],words[5], "0"), // todo - height?
                    groundSpeedKnots = ParseDouble(words[6]),
                    // todo - other fields
                    Date = words[8],
                    FaaMode = ParseFaaMode(words[11])
                };

                // 
                if (!String.IsNullOrEmpty(message.Date) && message.Date.Length == 6)
                { // fill in ddmmyy for date Utc
                    var ddmmyy = Int32.Parse(message.Date);
                    var year = ddmmyy % 100;
                    var month = (ddmmyy / 100) % 100;
                    var day = ddmmyy / (100 * 100);

                    if (year < 70) 
                        year += 2000;
                    else 
                        year += 1900;

                    var u = message.Utc;
                    message.Utc = new DateTime(
                        year,month,day,
                        u.Hour, u.Minute, u.Second
                        );
                    // Console.WriteLine($"RMC message date: {message.Utc}");
                }
            }
            catch (Exception ex)
            {
                return false;
            }
            return true;
        }
    }

    // GGA, // GGA - Global Positioning System Fix Data
    //                                                     11
    //        1         2       3 4        5 6 7  8   9  10 |  12 13  14   15
    //        |         |       | |        | | |  |   |   | |   | |   |    |
    // $--GGA,hhmmss.ss,llll.ll,a,yyyyy.yy,a,x,xx,x.x,x.x,M,x.x,M,x.x,xxxx*hh<CR><LF>
    //
    // $GPGGA,151924,3723.454444,N,12202.269777,W,2,09,1.9,–17.49,M,–25.67,M,1,0000*57
    public class GgaGnsMessage : GnsMessage
    {
        public override bool HasNaN =>
            position.HasNaN &&
            Double.IsNaN(HDOP) &&
            Double.IsNaN(orthometricHeight) &&
            Double.IsNaN(geoidSeparation)
        ;

        public DateTime Utc; // UTC time
        public Location position;

        public GpsFixQuality GpsFixQuality;
        public int SvCount; // 00 through 24+
        // https://gis.stackexchange.com/questions/174046/relation-between-geoidal-separation-and-antenna-altitude-in-gga-sentence
        public double HDOP; // horizontal dilution of precision, basically error in Z above ground, NaN if missing
        // elevation usually orthometricHeight
        // have height above ellipsoid = orthometric height + geoidal separation
        public double orthometricHeight; // MSL reference, field 10 'M' means meters
        public double geoidSeparation; // field 12 'M' means meters
        public double dgpsAge; // not valid if DGPS not used
        public int refStationId; // 0000-4095, null when ref ID is selected and no corrections received

        public static bool TryParse(string fieldsText, out GgaGnsMessage message)
        {
            message = null;
            if (String.IsNullOrEmpty(fieldsText))
                return false;
            try
            {
                var words = fieldsText.Split(',');
                if (words.Length != 14)
                    return false;

                if (words[9] != "M" && words[8] != "")
                    return false;
                if (words[11] != "M" && words[10] != "")
                    return false;

                var quality = (GpsFixQuality) ParseInt(words[5], 0, 8);
                message = new GgaGnsMessage
                {
                    Utc = ParseUtc(words[0]),
                    position = ParseLatLong(words[1],words[2],words[3],words[4], words[8]),
                    GpsFixQuality = quality,
                    SvCount =  ParseInt(words[6]),
                    HDOP = ParseDouble(words[7]),
                    orthometricHeight = ParseDouble(words[8]),
                    geoidSeparation   = ParseDouble(words[10]),
                    dgpsAge = quality == GpsFixQuality.Differential?ParseDouble(words[12]):0.0,
                    refStationId = words[13]==""?0:ParseInt(words[13])

                    // todo - other fields - lots are useful
                };
            }
            catch (Exception ex)
            {
                return false;
            }
            return true;
        }
    }

    public enum GnsSystem
    {
        UNKNOWN,
        GPS,     // GP messages
        GLONASS, // GL Messages
        BEIDOU,  // GB, BD - China
        GALILEO, // GA - 
        COMBO,   // GN - system combination (!?)
    }

    public enum GnsMessageType
    {
        UNKNOWN,
        GSV, // satellites in view
        GSA, // GPS DOP and active satellites - gives PDOP, HDOP, VDOP
        VTG, // VTG - Track made good and Ground speed

        GLL, // GLL - Geographic Position - Latitude/Longitude
        RMC, // RMC - Recommended Minimum Navigation Information
        GGA, // GGA - Global Positioning System Fix Data

        TXT  // TXT - Text messages
    }


    // in GGA message
    public enum GpsFixQuality
    {
        NoFixAvailable = 0,
        GPS,
        Differential,
        PPS,
        RealTimeKinematic = 4,
        FloatRTK = 5,
        Estimated,
        Manual,
        Simulation
    }


}
