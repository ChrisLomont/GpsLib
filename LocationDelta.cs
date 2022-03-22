
namespace Lomont.Gps
{
    /// <summary>
    /// Location delta
    /// todo - to another file
    /// </summary>
    public class LocationDelta : Bearing
    {
        public LocationDelta(double distanceM, string bearing) : base(Bearing.Parse(bearing).Degrees)
        {
            Length = distanceM;
        }

        public LocationDelta(double distanceM, Bearing bearing) : base(bearing.Degrees)
        {
            Length = distanceM;
        }

        /// <summary>
        /// Length in meters
        /// </summary>
        public double Length { get; }

        public LocationDelta(Location start, Location end) : base(Wgs84.Heading(start, end))
        {
            Length = Wgs84.Distance(start, end);
        }

        public override string ToString()
        {
            return $"{Degrees:F4}° {Length}m";
        }
    }
}
