using System.Diagnostics;
using static System.Math;

namespace Lomont.Gps
{
    /// <summary>
    /// Code to make simple Gps tasks short to code
    /// </summary>
    public static class SimpleGps
    {

        public static Location Location(double lat, double lng, double htM = 0.0) => new(lat, lng, htM);

        public static bool Approx(double d1, double d2, double tolerance = 0.0001) => Abs(d1 - d2) < tolerance;

        public static bool Approx(Location a, Location b, double tolerance = 0.0001) => Approx(Distance(a, b), 0, tolerance);

        public static void Assert(bool success)
        {
            Trace.Assert(success);
        }

        /// <summary>
        /// Angle in degrees from p1 to p2 to p3
        /// </summary>
        /// <param name="p1"></param>
        /// <param name="p2"></param>
        /// <param name="p3"></param>
        public static double Angle(Location p1, Location p2, Location p3) => (Bearing(p2, p1) - Bearing(p3, p2)).Degrees;


        public static Bearing Bearing(Location start, Location end) => new(start, end);

        /// <summary>
        /// Parse string like @"N 0°16'28''W" into bearing value
        /// </summary>
        /// <param name="bearing"></param>
        /// <returns></returns>
        public static Bearing Bearing(string bearing) => Gps.Bearing.Parse(bearing);

        public static Bearing Bearing(double degrees) => new Bearing(degrees);

        public static LocationDelta Direction(double distanceM, Bearing b) => new(distanceM, b);

        public static double DistanceToLine(Location p1, Location p2, Location testPoint) =>
            Wgs84.DistanceToLine(p1, p2, testPoint);

        /// <summary>
        /// Interpolate between 0=start to 1=end.
        /// </summary>
        /// <param name="start"></param>
        /// <param name="end"></param>
        /// <param name="ratio"></param>
        /// <returns></returns>
        public static Location Interpolate(Location start, Location end, double ratio) => Wgs84.Interpolate(start, end, ratio);

        /// <summary>
        /// compute area in sq meters
        /// </summary>
        /// <param name="points"></param>
        /// <returns></returns>
        public static double Area(params Location[] points) => Wgs84.Area(points);


        /// <summary>
        /// Feet to meters (SI internals)
        /// </summary>
        /// <param name="feet"></param>
        /// <returns></returns>
        public static double Feet(double feet) => GpsUtils.FeetToMeters(feet);

        /// <summary>
        /// Inches to meters
        /// </summary>
        /// <param name="inches"></param>
        /// <returns></returns>
        public static double Inch(double inches) => Feet(inches/12.0);

        /// <summary>
        /// Acres to sq meters (SI internals)
        /// </summary>
        /// <param name="acres"></param>
        /// <returns></returns>
        public static double Acres(double acres) => acres * 4046.86;

        public static double RelativeError(double sample, double truth) => Abs((sample - truth) / truth);

        public static double Distance(Location p1, Location p2) => Wgs84.Distance(p1, p2);

    }
}
