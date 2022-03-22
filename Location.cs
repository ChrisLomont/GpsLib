using System;
using Lomont.Numerical;

namespace Lomont.Gps
{
    /// <summary>
    /// Represent a location for global positioning
    /// Holds latitude, longitude, and height
    /// </summary>
    public struct Location : IHasNaN
    {
        /// <summary>
        /// Convert a 3 vector (x,y,z) to this as (latitude, longitude, height)
        /// </summary>
        /// <param name="v"></param>
        public Location(Vec3 v) : this(v.X,v.Y,v.Z)
        {
        }

        /// <summary>
        /// Get vector3 as lat, long, height
        /// </summary>
        /// <returns></returns>
        public Vec3 ToVec3()
        {
            return new Vec3(Latitude,Longitude, Height);
        }

        public Location(double latitude, double longitude, double height = 0)
        {
            Latitude = latitude;
            Longitude = longitude;
            Height = height;
        }

        public void Deconstruct(out double latitude, out double longitude)
        {
            latitude = Latitude;
            longitude = Longitude;
        }

        /// <summary>
        /// Are any entries missing (represented as NaN)?
        /// </summary>
        public bool HasNaN =>
            Double.IsNaN(Latitude) &&
            Double.IsNaN(Longitude) &&
            Double.IsNaN(Height)
        ;

        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public double Height { get; set; }

        public static LocationDelta operator -(Location start, Location end) => new LocationDelta(start, end);

        public static Location operator +(Location start, LocationDelta d)
        {
            var p = Wgs84.VincentyDirectLocation(start, d.Degrees, d.Length);
            return p.location;
        }


        public override string ToString()
        {
            return $"[{Latitude},{Longitude},{Height}]";
        }
    }
}
