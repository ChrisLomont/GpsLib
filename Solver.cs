using System;
using Lomont.Numerical;

namespace Lomont.Gps
{

    /// <summary>
    /// Simple numerical optimizers
    /// </summary>
    public static class Solver
    {
        /// <summary>
        /// Find minimum of function over 1D interval by refined subdivision
        /// Function should be somewhat well behaved
        /// </summary>
        /// <param name="function">Get value of function for parameter</param>
        /// <param name="min">Min interval endpoint to search</param>
        /// <param name="max">Max interval endpoint to search</param>
        /// <param name="steps">Steps to subdivide intervals into</param>
        /// <param name="tolerance">Error tolerance desired</param>
        /// <param name="maxPasses">Maximum passes to try</param>
        /// <returns>Bounds min,max on best interval found</returns>
        public static (double min,double max) Solve1D(
            Func<double,double> function,
            double min = -1.0,
            double max = 1.0,  
            int steps = 10, 
            double tolerance = 1e-10,
            int maxPasses = Int32.MaxValue
            )
        {
            var passes = 0;

            // iterate over space, solve
            do
            {
                var del = (max - min) / steps;
                // get best point
                var bestParameter = 0.0;
                var bestDist = double.MaxValue;
                for (var i = 0; i < steps; ++i)
                {
                    var pt = (i + 0.5) * del + min;
                    var val = function(pt);
                    ++passes;
                    if (passes >= maxPasses)
                        break; // bail out

                    if (val < bestDist)
                    {
                        bestDist = val;
                        bestParameter = pt;
                    }
                }

                // scale space for another pass
                min = bestParameter - 2 * del;
                max = bestParameter + 2 * del;
            } while (max - min > tolerance);

            return (min, max);
        }

        /// <summary>
        /// Find minimum of function over 2D region by refined subdivision
        /// Function should be somewhat well behaved.
        /// vector z values should be 0
        /// </summary>
        /// <param name="function">Get value of function for parameter</param>
        /// <param name="min">Min axis aligned corner to search</param>
        /// <param name="max">Max axis aligned corner to search</param>
        /// <param name="steps">Steps to subdivide rectangles into</param>
        /// <param name="tolerance">Error tolerance desired</param>
        /// <param name="maxPasses">Maximum passes to try</param>
        /// <returns>Bounds min,max on best interval found</returns>
        public static (Vec3 min, Vec3 max) Solve2D(
            Func<Vec3, double> function,
            Vec3 min,
            Vec3 max,
            int steps = 10,
            double tolerance = 1e-10,
            int maxPasses = Int32.MaxValue
            )
        {
            var passes = 0;

            // iterate over space, solve
            do
            {
                var del = (max - min) / steps;
                // get best point
                var bestParameter = new Vec3();
                var bestDist = double.MaxValue;
                for (var i = 0; i < steps; ++i)
                for (var j = 0; j < steps; ++j)
                {
                    var x = (i + 0.5) * del.X + min.X;
                    var y = (j + 0.5) * del.Y + min.Y;
                    var pt = new Vec3(x, y, 0);

                    var value = function(pt);
                    ++passes;
                    if (passes >= maxPasses)
                        break; // bail out

                    if (value < bestDist)
                    {
                        bestDist = value;
                        bestParameter = pt;
                    }
                }

                // scale space for another pass
                min = bestParameter - 2 * (max - min) / steps;
                max = bestParameter + 2 * (max - min) / steps;
            } while ((max-min).Length > tolerance);

            return (min, max);
        }

    }
}
