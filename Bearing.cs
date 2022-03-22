using System;
using System.Text.RegularExpressions;

namespace Lomont.Gps
{
    /// <summary>
    /// Track a direction, can be added or subtracted from Location to get another Location
    /// todo - rename
    /// </summary>
    public class Bearing
    {
        public double Degrees { get; }
        public Bearing(double degrees)
        {
            Degrees = degrees;
        }

        static readonly Regex matchDirection = new Regex(
            "^(?<ns>N|S)[ ]*(?<d1>\\d+)°[ ]*(?<d2>\\d+)'[ ]*(?<d3>\\d+)(''|\")[ ]*(?<we>W|E)",
            RegexOptions.Compiled
            );

        /// <summary>
        /// Parse a direction like "N 00°16'28'' W" into a bearing
        /// </summary>
        /// <param name="text"></param>
        /// <returns></returns>
        public static Bearing Parse(string text)
        {
            var m = matchDirection.Match(text);
            if (!m.Success)
                throw new FormatException($"{text} bad format for a Bearing");
            var deg = Double.Parse(m.Groups["d1"].Value);
            var min = Double.Parse(m.Groups["d2"].Value) / 60.0;
            var sec = Double.Parse(m.Groups["d3"].Value) / 3600.0;
            var dd = deg + min + sec;
            return (m.Groups["ns"].Value, m.Groups["we"].Value) switch
            {
                ("N", "E") => new(dd),
                ("N", "W") => new(360.0 - dd),
                ("S", "E") => new(180 - dd),
                ("S", "W") => new(180 + dd),
                _ => throw new FormatException($"{text} bad format for a Bearing")
            };
        }
        public Bearing(Location start, Location end)
        {
            Degrees = Wgs84.Heading(start, end);
        }
        public static Bearing operator -(Bearing a, Bearing b)
        {
            return new Bearing(a.Degrees - b.Degrees);
        }
        public static Bearing operator +(Bearing a, Bearing b)
        {
            return new Bearing(a.Degrees + b.Degrees);
        }

        public static Bearing operator +(Bearing a, string t)
        {
            return a + Parse(t);
        }
        public static Bearing operator +(string t, Bearing a)
        {
            return a + t;
        }

        public override string ToString()
        {
            return $"{Degrees:F4}°";
        }
    }
}
