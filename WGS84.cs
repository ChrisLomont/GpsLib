using System;
using System.Diagnostics;
using System.Linq;
using static System.Math;
using static Lomont.Gps.GpsUtils;
using Lomont.Numerical;

namespace Lomont.Gps
{
    /// <summary>
    /// A common World Coordinate System
    ///
    /// represents a geoid
    /// most common in GNS 
    /// https://en.wikipedia.org/wiki/World_Geodetic_System
    /// </summary>
    public class Wgs84
    {
        // Center of coord system supposed to be earth's center of mass, uncertainty about 2cm
        // Zero meridian is the IERS Reference Meridian, 5.3 arc seconds (102m) east of the Greenwich meridian
        // oblate spheroid, 
        // equitorial radius a = a = 6378137 
        // flattening flattening f = 1/298.257223563
        // WGS gravitational constant  S 84 gravitational constant (mass of Earth’s atmosphere included) is GM = 3986004.418×108 m³/s².
        //  angular velocity of the Earth is defined to be ω = 72.92115×10−6 rad/s
        // polar semi-minor axis b which equals a × (1 − f) = 6356752.3142 m
        // first eccentricity squared, e² = 6.69437999014×10−3
        // WGS 84 uses the Earth Gravitational Model 2008 This geoid defines the nominal sea level surface by means of a spherical harmonics series of degree 360
        // WGS 84 currently uses the World Magnetic Model 2020

        const double a = 6378137;
        const double f = 1 / 298.257223563;
        const double b = a * (1 - f);
        const double e2 = 6.69437999014e-3; // eccentricity e squared, e = 1-b^2/a^2;


        /// <summary>
        /// Convert Lat/Long/ht to X,Y,Z in 3d meters
        /// </summary>
        /// <returns></returns>
        public static Vec3 GeodeticToEcef(Location position)
        {
            var p = DegreesToRadians(position.Latitude);
            var L = DegreesToRadians(position.Longitude);
            var h = position.Height;
            var s = Math.Sin(p);
            var Np = a / Math.Sqrt(1 - e2 * s * s);
            var x = (Np + h) * Math.Cos(p) * Math.Cos(L);
            var y = (Np + h) * Math.Cos(p) * Math.Sin(L);
            var z = (b * b * Np / (a * a) + h) * Math.Sin(p);
            return new Vec3(x, y, z);
        }

        public static double DegreesToRadians(double degrees)
        {
            return degrees * Math.PI / 180.0;
        }

        // geodetic (lat p, long L, height h) to ECEF coordinates
        // X = (N(p) + h) cos(p) cos(L)
        // Y = (N(p) + h) cos(p) sin(L)
        // Z = (b^2 N(p) / a^2 + h) sin(p)
        // N(p) = a/sqrt(1-e^2 sin^2(p))
        // e = 1-b^2/a^2
        //
        // ECEF to geodetic
        // L = arctan (Y/X)
        // Newton-Raphson:
        // k-1 - e^2 a k / sqrt(p^2+(1-e^2)Z^2 k^2) = 0
        // k = p tan(p)/Z
        // height then 
        // h = e^-2 (k^-1 - k0^-1) sqrt(p^2+Z^2 k^2)
        // k0 = (1-e^2)^-1
        //
        // can iterate:
        // k0 above if h close to 0
        // k_{i+1} = 1 + (p^2+1-e^2)Z^2k_i^3/(c_i - p^2)
        // c_i = (p^2 + (1-e^2)Z^2k_i^2)^(3/2)/(ae^2)

        // https://en.wikipedia.org/wiki/Geographic_coordinate_conversion

        // Vincenty's formulas
        // https://en.wikipedia.org/wiki/Vincenty%27s_formulae
        
        /// <summary>
        /// Given two locations, find their azimuths a1 and a2 and distances between them
        /// Fails on same point, gets NaN - todo fix 
        /// </summary>
        /// <param name="p1"></param>
        /// <param name="p2"></param>
        /// <returns></returns>
        public static (double aizmuth1, double aizmuth2, double distance) VincentyInverseDistance(Location p1, Location p2)
        {
            var (Φ1, L1) = (GpsUtils.DegreesToRadians(p1.Latitude), GpsUtils.DegreesToRadians(p1.Longitude));
            var (Φ2, L2) = (GpsUtils.DegreesToRadians(p2.Latitude), GpsUtils.DegreesToRadians(p2.Longitude));

            var U1 = Atan((1 - f) * Tan(Φ1));
            var U2 = Atan((1 - f) * Tan(Φ2));
            var L = L2 - L1;
            var λ = L; // initial value

            var tolerance = 1e-12; // gives about 0.06mm accuracy on Earth
            var λt = λ; // next iteration
            
            double c2α = 0.0, cσ = 0, sσ = 0.0, c2αm = 0.0, σ=0;
            double cλ, sλ;
            var (cu1, cu2) = (Cos(U1), Cos(U2));
            var (su1, su2) = (Sin(U1), Sin(U2));

            // iterate till converges
            do
            {
                λ = λt;

                (cλ, sλ) = (Cos(λ), Sin(λ));

                var t1 = cu2 * sλ;
                var t2 = cu1 * su2 - su1 * cu2 * cλ;

                sσ = Sqrt(t1 * t1 + t2 * t2);
                cσ = su1 * su2 + cu1 * cu2 * cλ;
                σ = Atan2(sσ, cσ);
                var sα = cu1 * cu2 * sλ / sσ;
                c2α = 1 - sα * sα;
                c2αm = cσ - 2 * su1*su2 / c2α;
                var C = f / 16.0 * c2α * (4 + f * (4 - 3 * c2α));
                λt = L + (1 - C) * f * sα * (σ + C * sσ * (c2αm + C * cσ * (-1 + 2 * c2αm * c2αm)));
            } while (Abs(λ - λt) > tolerance);

            var u2 = (a*a-b*b) / (b*b) * c2α;
            var A = 1 + u2 / 16384.0 * (4096 + u2 * (-768 + u2 * (320 - 175 * u2)));
            var B = u2 / 1024 * (256 + u2 * (-128 + u2 * (74 - 47 * u2)));

            var Δσ = B * sσ * (c2αm + B / 4 * (cσ * (-1 + 2 * c2αm * c2αm) -
                                               B / 6 * c2αm * (-3 + 4 * sσ * sσ) * (-3 + 4 * c2αm * c2αm)));
            var s = b * A * (σ - Δσ);
            var α1 = Atan2(cu2 * sλ, cu1 * su2 - su1 * cu2 * cλ);
            var α2 = Atan2(cu1 * sλ, -su1 * cu2 + cu1* su2* cλ);
            return (α1, α2, s);
        }

        /// <summary>
        /// Given a location, an aizmuth (bearing, in degrees), and a distance,
        /// compute the resulting location and aizmuth
        /// </summary>
        /// <param name="p1"></param>
        /// <param name="aizmuth"></param>
        /// <returns></returns>
        public static (Location location, double aizmuthDegrees) VincentyDirectLocation(Location p1, double aizmuthDegrees, double distanceMeters)
        { // https://en.wikipedia.org/wiki/Vincenty%27s_formulae
            var (Φ1, L1) = (GpsUtils.DegreesToRadians(p1.Latitude), GpsUtils.DegreesToRadians(p1.Longitude));
            var s = distanceMeters;
            var α1 = GpsUtils.DegreesToRadians(aizmuthDegrees);
            var (cα1, sα1) = (Cos(α1),Sin(α1));
            var U1 = Atan((1 - f) * Tan(Φ1));
            var (cu1, su1) = (Cos(U1),Sin(U1));
            var σ1 = Atan2(Tan(U1), cα1);
            
            var sα = cu1 * sα1;
            var c2α = 1 - sα * sα;

            var u2 = c2α * (a * a - b * b) / (b * b);
            var A = 1 + u2 / 16384.0 * (4096 + u2 * (-768 + u2 * (320 - 175 * u2)));
            var B = u2 / 1024 * (256 + u2 * (-128 + u2 * (74 - 47 * u2)));

            var σ = s / (b * A);
            var σt = σ;
            double cσ, sσ, c2m;

            var tolerance = 1e-12; // todo?
            do
            {
                σ = σt;
                var tσm = 2*σ1 + σ;
                c2m = Cos(tσm);
                sσ = Sin(σ);
                cσ = Cos(σ);
                var Δσ = B * sσ * (c2m + B / 4 *
                    (cσ * (-1 + 2 * c2m * c2m) - B / 6 * c2m * (-4 + 4 * sσ * sσ) * (-3 + 4 * c2m * c2m)));
                σt = s / (b * A) + Δσ;
            } while (Abs(σ - σt) > tolerance);

            σ = σt; // choose last iteration

            var t2 = su1 * sσ - cu1 * cσ * cα1;
            var Φ2 = Atan2(su1 * cσ + cu1 * sσ * cα1, (1 - f) * Sqrt(sα * sα + t2 * t2));
            var λ = Atan2(sσ * sα1, cu1 * cσ - su1 * sσ * cα1);
            var C = f / 16 * c2α * (4 + f * (4 - 3 * c2α));
            var L = λ - (1 - C) * f * sα * (σ + C * sσ * (c2m + C * cσ * (1 - 2 * c2m * c2m)));
            var L2 = L + L1;
            var α2 = Atan2(sα, -su1 * sσ + cu1 * cσ * cα1);

            return (new Location(RadiansToDegrees(Φ2), RadiansToDegrees(L2)),RadiansToDegrees(α2));
        }



        /// <summary>
        /// Distance in meters between two lat/long/height positions.
        /// Ignores height.
        /// </summary>
        /// <returns></returns>
        public static double Distance(Location position1, Location position2)
        {   // todo - this naive, ok for flat, else use (maybe?)
            // https://en.wikipedia.org/wiki/Vincenty%27s_formulae
            //var c1 = GeodeticToEcef(position1);
            //var c2 = GeodeticToEcef(position2);
            //c1.z = c2.z = 0; // todo - how else to treat distance?
            //return (c1 - c2).Length;
            var (_, _, d) = VincentyInverseDistance(position1,position2);
            return d;
        }

        /// <summary>
        /// Distance in meters between two lat/long pairs
        /// </summary>
        /// <param name="lat1"></param>
        /// <param name="lng1"></param>
        /// <param name="lat2"></param>
        /// <param name="lng2"></param>
        /// <returns></returns>
        public static double Distance(double lat1, double lng1, double lat2, double lng2)
        {
            return Distance(new Location(lat1, lng1), new Location(lat2, lng2));
        }

        /// <summary>
        /// WGS 84 distance in meters to line
        /// Treats region as planar
        /// </summary>
        /// <returns></returns>
        public static double DistanceToLine(Location endpoint1, Location endPoint2, Location samplePoint)
        {
            var a = endpoint1.ToVec3();
            var b = endPoint2.ToVec3();
            var c = samplePoint.ToVec3();
            a.Z = b.Z = c.Z = 0; // zero out height, makes 2D
            // closest point d is lin combo d = ka + (1-k)b
            // k then (c-b).(a-b)/(a-b).(a-b)
            var k = Vec3.Dot(c - b, a - b) / Vec3.Dot(a - b, a - b);
            var d = k * a + (1 - k) * b;
            var d1 = new Location(d);
            d1.Height = (endPoint2.Height + endpoint1.Height)/2; // sloppy, but better than not using a height
            return Wgs84.Distance(samplePoint, d1);
        }


        /// <summary>
        /// Angle in degrees going from ptA to ptB
        /// in [0,2pi)
        /// is degrees where N is 0, E is 90, etc...
        /// </summary>
        /// <param name="ptA"></param>
        /// <param name="ptB"></param>
        /// <returns></returns>
        public static double Heading(Location ptA, Location ptB)
        {
            // this algo maybe too simplistic, spherical?
            // todo - make better, using Algorithms for geodesics, Karney, 2013
            // algo in https://www.igismap.com/formula-to-find-bearing-or-heading-angle-between-two-points-latitude-longitude/

            var ta = GpsUtils.DegreesToRadians(ptA.Latitude);
            var tb = GpsUtils.DegreesToRadians(ptB.Latitude);
            var la = GpsUtils.DegreesToRadians(ptA.Longitude);
            var lb = GpsUtils.DegreesToRadians(ptB.Longitude);

            var dL = lb - la;
            var X = Math.Cos(tb) * Math.Sin(dL);
            var Y = Math.Cos(ta) * Math.Sin(tb) - Math.Sin(ta) * Math.Cos(tb) * Math.Cos(dL);
            var beta = Math.Atan2(X, Y); // radians
            var deg = GpsUtils.RadiansToDegrees(beta);

            // todo - make faster clamp - these all needed to handle small numerical noise
            while (deg < 0) deg += 360.0;
            while (360.0 <= deg) deg -= 360.0;
            deg = Math.Clamp(deg, 0, 360.0);
            if (deg == 360.0) deg = 0.0;
            Debug.Assert(0 <= deg && deg < 360.0);
            return deg;
        }

        /// <summary>
        /// Compute area enclosed by points
        /// todo: This is a decent approximation, could be better
        /// </summary>
        /// <param name="points"></param>
        /// <returns></returns>
        public static double Area(params Location[] points)
        {
            // see https://stackoverflow.com/questions/1340223/calculating-area-enclosed-by-arbitrary-polygon-on-earths-surface
            // see https://math.stackexchange.com/questions/3207981/caculate-area-of-polygon-in-3d
            var ai = points.Select(GeodeticToEcef).ToList();
            var s = new Vec3();
            var n = ai.Count;
            for (var i = 0; i < n; ++i)
                s += Vec3.Cross(ai[i], ai[(i + 1) % n]);
            return s.Length / 2;
        }

        /// <summary>
        /// Interpolate between 0=start to 1=end.
        /// </summary>
        /// <param name="start"></param>
        /// <param name="end"></param>
        /// <param name="ratio"></param>
        /// <returns></returns>
        public static Location Interpolate(Location start, Location end, double ratio) =>
            start + new LocationDelta(Distance(start, end) * ratio, new Bearing(start, end));

    }
}
