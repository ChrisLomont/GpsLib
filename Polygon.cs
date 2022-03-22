using System;
using System.Collections.Generic;
using Lomont.Numerical;

namespace Lomont.Gps
{
    /// <summary>
    /// Closed polygon of lat, long points
    /// Assume non self intersecting
    /// </summary>
    public class Polygon
    {
        public List<Location> Points { get; } = new List<Location>();

        /// <summary>
        /// Is it in bounds
        /// </summary>
        /// <returns></returns>
        public bool IsInside(Location pt)
        {
            Vec3 point = pt.ToVec3();
            var angle = 0.0;

            int pass = 0;
            while (true)
            {
                var dir = new Vec3(Math.Cos(angle), Math.Sin(angle), 0); // todo - not always robust - can double hit a vertex
                var numCrossings = 0;
                var solving = true; // assume solve still ok
                // loop over edges
                for (int i = 0; i < Points.Count && solving; ++i)
                {
                    int j = (i + 1) % Points.Count;

                    // Check if a ray in the positive x direction crosses the current edge.
                    var res = RayLineSegmentIntersection(point, dir, Points[i].ToVec3(), Points[j].ToVec3());
                    if (res == HitResult.Hits)
                        ++numCrossings;
                    else if (res == HitResult.Inconclusive)
                    {
                        // need new angle
                        solving = false;
                    }
                }

                if (solving)
                {
                    // made it out ok
                    return (numCrossings % 2) == 1;
                }

                angle += 0.1; // enough to move, never repeats, should hit proper solution
                ++pass; // prevent inf loop
                if (pass >= 10) return false; // bail
            }

        }

        enum HitResult
        {
            Misses,
            Hits,
            Inconclusive
        }

        /// <summary>
        /// See if ray hits line segment
        /// </summary>
        /// <param name="rayOrigin"></param>
        /// <param name="rayDirection"></param>
        /// <param name="endPoint1"></param>
        /// <param name="endPoint2"></param>
        /// <returns></returns>
        static HitResult RayLineSegmentIntersection(Vec3 rayOrigin, Vec3 rayDirection, Vec3 endPoint1, Vec3 endPoint2)
        {

            // ray is p1(t1) = o + d*t1
            // seg is p2(t2) = endpoint1 + (endpoint2-endpoint1)*t2
            //
            // let v1=o-p1, v2=e2-e1, v3=(-dy,dx) (perp), then
            // t1 = |v2 X v1|/(v2.v3)
            // t2 = (v1.v3)/(v2.v3)


            var perp = new Vec3(-rayDirection.Y, rayDirection.X);
            var v1 = rayOrigin - endPoint1;
            var v2 = endPoint2 - endPoint1;

            var denom = Vec3.Dot(v2, perp);
            if (Math.Abs(denom) < 0.00001)
                return HitResult.Inconclusive; // parallel, messy numerically to clean up

            // The length of this cross product can also be written as abs( aToB.x * aToO.y - aToO.x * aToB.y ).
            var t1 = Vec3.Cross(v2, v1).Length / denom;
            var t2 = Vec3.Dot(v1, perp) / denom;

            var eps = 0.001; // back off end points a bit

            var hits = t1 >= 0 && t2 >= 0+eps && t2 <= 1-eps;
            return hits ? HitResult.Hits : HitResult.Misses;
        }
    }
}
