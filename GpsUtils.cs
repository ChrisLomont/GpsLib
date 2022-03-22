using System;
using System.Linq;

namespace Lomont.Gps
{
    /// <summary>
    /// Some helper functions
    /// </summary>
    public static class GpsUtils
    {
        #region Conversions
        /// <summary>
        /// Convert decimal degrees to textual degrees, minutes, seconds
        /// </summary>
        /// <param name="degrees"></param>
        /// <returns></returns>
        public static string DegreesToDMS(double degrees)
        {
            // S 0°21'0" E
            var di = Math.Floor(degrees);
            var min = (degrees - di) * 60;
            var mi = Math.Floor(min);
            var sec = (min - mi) * 60;
            var si = Math.Round(sec);

            return $"{di}°{mi}'{si}\"";
        }

        /// <summary>
        /// Degrees to radians
        /// </summary>
        /// <param name="degrees"></param>
        /// <returns></returns>
        public static double DegreesToRadians(double degrees) => Math.PI * degrees / 180.0;

        /// <summary>
        /// Radians to degrees
        /// </summary>
        /// <param name="radians"></param>
        /// <returns></returns>
        public static double RadiansToDegrees(double radians) => 180 * radians / Math.PI;

        /// <summary>
        /// Feet to meters
        /// </summary>
        /// <param name="feet"></param>
        /// <returns></returns>
        public static double FeetToMeters(double feet) => feet / 3.28084;
        
        /// <summary>
        /// Meters to feet
        /// </summary>
        /// <param name="meters"></param>
        /// <returns></returns>
        public static double MetersToFeet(double meters) => meters * 3.28084;

        #endregion

        /// <summary>
        /// Given a min, max box to check, an error func for a test point, and
        /// max error allowed, compute a point that makes the error function meet the tolerance
        /// searches the max/min box
        /// </summary>
        /// <param name="minPt"></param>
        /// <param name="maxPt"></param>
        /// <param name="errorFunc"></param>
        /// <param name="maxErr"></param>
        /// <returns></returns>
        public static (Location pos, double err) Solve(Location minPt, Location maxPt, Func<Location,double> errorFunc, double maxErr = 0.001)
        {
            var (min,max) = Solver.Solve2D(
                p=>errorFunc(new Location(p)),
                minPt.ToVec3(),
                maxPt.ToVec3(),
                steps:10,
                tolerance:maxErr
            );

            var md = (min + max) / 2;
            return (new Location(md), (max - min).Length);
        }

        /// <summary>
        /// Compute point in direction
        /// </summary>
        /// <param name="pt"></param>
        /// <param name="delta"></param>
        /// <param name="distance"></param>
        /// <returns></returns>
        public static Location Direction(Location pt, Location delta, double distance, double err = 0.001)
        {
            while (Wgs84.Distance(pt, new Location(pt.ToVec3() + delta.ToVec3())) < distance)
                delta = new Location(delta.ToVec3()*2);
            while (Wgs84.Distance(pt, new Location(pt.ToVec3() + delta.ToVec3())) > distance)
                delta = new Location(delta.ToVec3() / 2);
            return new Location(pt.ToVec3() + delta.ToVec3());
        }

        /// <summary>
        /// Given center point and radius, compute a bounding box
        /// </summary>
        /// <param name="center"></param>
        /// <param name="radius"></param>
        /// <returns></returns>
        public static (Location minPt, Location maxPt) BoundingBoxFromCircle(Location center, double radius)
        {
            var up = Direction(center, new Location(0.01, 0), radius);
            var down = Direction(center, new Location(-0.01, 0), radius);
            var rt = Direction(center, new Location(0, 0.01), radius);
            var lf = Direction(center, new Location(0, -0.01), radius);
            return BoundingBoxFromPoints(up, down, rt, lf);
        }

        /// <summary>
        /// Get axis aligned bounding box
        /// </summary>
        /// <returns></returns>
        public static (Location minPt, Location maxPt) BoundingBoxFromPoints(params Location [] pts)
        {
            var minLat = pts.Min(p => p.Latitude);
            var maxLat = pts.Max(p => p.Latitude);
            var minLong = pts.Min(p => p.Longitude);
            var maxLong = pts.Max(p => p.Longitude);
            return (new Location(minLat, minLong), new Location(maxLat, maxLong));
        }

        /// <summary>
        /// Bearing in degrees, minutes seconds, and feet
        /// </summary>
        /// <returns></returns>
        public static (string bearing, double feet) BearingText(Location a, Location b)
        {
            var ft = GpsUtils.MetersToFeet(Wgs84.Distance(a, b));
            var df = Wgs84.Heading(a, b);
            return (GpsUtils.DegreesToDMS(df), ft);
        }

        /// <summary>
        /// Find a point the given distances from the three coords
        /// distances in meters
        /// Return point, and sum of three errors
        /// </summary>
        /// <param name="ptA"></param>
        /// <param name="ptB"></param>
        /// <param name="ptC"></param>
        /// <param name="radiusA"></param>
        /// <param name="radiusB"></param>
        /// <param name="radiusC"></param>
        public static (Location pos, double err) GetCircleIntersection(Location ptA, Location ptB, Location ptC, double radiusA, double radiusB, double radiusC)
        {
            var (minIn, maxIn) = BoundingBoxFromPoints(ptA, ptB, ptC);

            return Solve(minIn, maxIn, Err, 0.01);

            // error for lat, long
            double Err(Location pt)
            {
                var da = Math.Abs(Wgs84.Distance(ptA, pt) - radiusA);
                var db = Math.Abs(Wgs84.Distance(ptB, pt) - radiusB);
                var dc = Math.Abs(Wgs84.Distance(ptC, pt) - radiusC);
                return da + db + dc;
            }

        }



    }
}
