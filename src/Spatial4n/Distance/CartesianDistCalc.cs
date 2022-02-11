/*
 * Licensed to the Apache Software Foundation (ASF) under one or more
 * contributor license agreements.  See the NOTICE file distributed with
 * this work for additional information regarding copyright ownership.
 * The ASF licenses this file to You under the Apache License, Version 2.0
 * (the "License"); you may not use this file except in compliance with
 * the License.  You may obtain a copy of the License at
 *
 *     http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */
using Spatial4n.Context;
using Spatial4n.Shapes;
using System;

namespace Spatial4n.Distance
{
    /// <summary>
    /// Calculates based on Euclidean / Cartesian 2d plane.
    /// </summary>
    public class CartesianDistanceCalculator
        : AbstractDistanceCalculator
    {
        private readonly bool squared;

        public CartesianDistanceCalculator()
        {
            squared = false;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="squared">
        /// Set to true to have <see cref="AbstractDistanceCalculator.Distance(IPoint, IPoint)"/>
        /// return the square of the correct answer. This is a
        /// performance optimization used when sorting in which the
        /// actual distance doesn't matter so long as the sort order is
        /// consistent.
        /// </param>
        public CartesianDistanceCalculator(bool squared)
        {
            this.squared = squared;
        }

        /// <exception cref="ArgumentNullException"><paramref name="from"/> is <c>null</c>.</exception>
        public override double Distance(IPoint from, double toX, double toY)
        {
            if (from is null)
                throw new ArgumentNullException(nameof(from)); // spatial4n specific - use ArgumentNullException instead of NullReferenceException

            double deltaX = from.X - toX;
            double deltaY = from.Y - toY;
            double xSquaredPlusYSquared = deltaX * deltaX + deltaY * deltaY;

            if (squared)
                return xSquaredPlusYSquared;

            return Math.Sqrt(xSquaredPlusYSquared);
        }

        /// <exception cref="ArgumentNullException"><paramref name="from"/> is <c>null</c>.</exception>
        public override bool Within(IPoint from, double toX, double toY, double distance)
        {
            if (from is null)
                throw new ArgumentNullException(nameof(from)); // spatial4n specific - use ArgumentNullException instead of NullReferenceException

            double deltaX = from.X - toX;
            double deltaY = from.Y - toY;
            return deltaX * deltaX + deltaY * deltaY <= distance * distance;
        }

        /// <exception cref="ArgumentNullException"><paramref name="from"/> is <c>null</c> or <paramref name="ctx"/> and <paramref name="reuse"/> are both <c>null</c>.
        /// <paramref name="ctx"/> must be non-<c>null</c> if <paramref name="reuse"/> is <c>null</c>.</exception>
        public override IPoint PointOnBearing(IPoint from, double distDEG, double bearingDEG, SpatialContext ctx, IPoint? reuse)
        {
            if (from is null)
                throw new ArgumentNullException(nameof(from)); // spatial4n specific - use ArgumentNullException instead of NullReferenceException

            if (distDEG == 0)
            {
                if (reuse is null)
                    return from;
                reuse.Reset(from.X, from.Y);
                return reuse;
            }
            double bearingRAD = DistanceUtils.ToRadians(bearingDEG);
            double x = from.X + Math.Sin(bearingRAD) * distDEG;
            double y = from.Y + Math.Cos(bearingRAD) * distDEG;
            if (reuse is null)
            {
                if (ctx is null)
                    throw new ArgumentNullException(nameof(ctx)); // spatial4n specific - use ArgumentNullException instead of NullReferenceException

                return ctx.MakePoint(x, y);
            }
            else
            {
                reuse.Reset(x, y);
                return reuse;
            }
        }

        /// <exception cref="ArgumentNullException"><paramref name="from"/> is <c>null</c> or <paramref name="ctx"/> and <paramref name="reuse"/> are both <c>null</c>.
        /// <paramref name="ctx"/> must be non-<c>null</c> if <paramref name="reuse"/> is <c>null</c>.</exception>
        public override IRectangle CalcBoxByDistFromPt(IPoint from, double distDEG, SpatialContext ctx, IRectangle? reuse)
        {
            if (from is null)
                throw new ArgumentNullException(nameof(from)); // spatial4n specific - use ArgumentNullException instead of NullReferenceException

            double minX = from.X - distDEG;
            double maxX = from.X + distDEG;
            double minY = from.Y - distDEG;
            double maxY = from.Y + distDEG;
            if (reuse is null)
            {
                if (ctx is null)
                    throw new ArgumentNullException(nameof(ctx)); // spatial4n specific - use ArgumentNullException instead of NullReferenceException

                return ctx.MakeRectangle(minX, maxX, minY, maxY);
            }
            else
            {
                reuse.Reset(minX, maxX, minY, maxY);
                return reuse;
            }
        }

        /// <exception cref="ArgumentNullException"><paramref name="from"/> is <c>null</c>.</exception>
        public override double CalcBoxByDistFromPt_yHorizAxisDEG(IPoint from, double distDEG, SpatialContext ctx)
        {
            if (from is null)
                throw new ArgumentNullException(nameof(from)); // spatial4n specific - use ArgumentNullException instead of NullReferenceException

            return from.Y;
        }

        /// <exception cref="ArgumentNullException"><paramref name="rect"/> is <c>null</c>.</exception>
        public override double Area(IRectangle rect)
        {
            if (rect is null)
                throw new ArgumentNullException(nameof(rect)); // spatial4n specific - use ArgumentNullException instead of NullReferenceException

            return rect.GetArea(null);
        }

        /// <exception cref="ArgumentNullException"><paramref name="circle"/> is <c>null</c>.</exception>
        public override double Area(ICircle circle)
        {
            if (circle is null)
                throw new ArgumentNullException(nameof(circle)); // spatial4n specific - use ArgumentNullException instead of NullReferenceException

            return circle.GetArea(null);
        }

        public override bool Equals(object? o)
        {
            if (this == o) return true;
            if (o is null || GetType() != o.GetType()) return false;

            if (!(o is CartesianDistanceCalculator that)) return false;
            return squared == that.squared;
        }

        public override int GetHashCode()
        {
            return (squared ? 1 : 0);
        }
    }
}
