﻿/*
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
using Spatial4n.Distance;
using System;
using System.Diagnostics;

namespace Spatial4n.Shapes
{
    /// <summary>
    /// A line between two points with a buffer distance extending in every direction. By
    /// contrast, an un-buffered line covers no area and as such is extremely unlikely to intersect with
    /// a point. <see cref="BufferedLine"/> isn't yet aware of geodesics (e.g. the dateline); it operates in Euclidean
    /// space.
    /// </summary>
    // Spatial4n: Removed "INTERNAL" designation as per https://github.com/locationtech/spatial4j/issues/216#issuecomment-1035126797
    public class BufferedLine : IShape
    {
        private readonly IPoint pA, pB;
        private readonly double buf;
        private readonly IRectangle bbox;
        /// <summary>the primary line; passes through pA &amp; pB</summary>
#pragma warning disable CS0618 // Type or member is obsolete
        private readonly InfBufLine linePrimary;
        /// <summary>perpendicular to the primary line, centered between pA &amp; pB</summary>
        private readonly InfBufLine linePerp;
#pragma warning restore CS0618 // Type or member is obsolete

        /// <summary>
        /// Creates a buffered line from pA to pB. The buffer extends on both sides of
        /// the line, making the width 2x the buffer. The buffer extends out from
        /// pA &amp; pB, making the line in effect 2x the buffer longer than pA to pB.
        /// </summary>
        /// <param name="pA">start point</param>
        /// <param name="pB">end point</param>
        /// <param name="buf">the buffer distance in degrees</param>
        /// <param name="ctx"></param>
        /// <exception cref="ArgumentNullException"><paramref name="pA"/>, <paramref name="pB"/>, or <paramref name="ctx"/> is <c>null</c>.</exception>
        public BufferedLine(IPoint pA, IPoint pB, double buf, SpatialContext ctx)
        {
            if (pA is null)
                throw new ArgumentNullException(nameof(pA)); // spatial4n specific - use ArgumentNullException instead of NullReferenceException
            if (pB is null)
                throw new ArgumentNullException(nameof(pB)); // spatial4n specific - use ArgumentNullException instead of NullReferenceException
            if (ctx is null)
                throw new ArgumentNullException(nameof(ctx)); // spatial4n specific - use ArgumentNullException instead of NullReferenceException

            Debug.Assert(buf >= 0);//TODO support buf=0 via another class ?

            // If true, buf should bump-out from the pA & pB, in effect
            // extending the line a little.
            bool bufExtend = true;//TODO support false and make this a
                                  // parameter

            this.pA = pA;
            this.pB = pB;
            this.buf = buf;

            double deltaY = pB.Y - pA.Y;
            double deltaX = pB.X - pA.X;

            Point center = new Point(pA.X + deltaX / 2,
                pA.Y + deltaY / 2, null);

            double perpExtent = bufExtend ? buf : 0;

#pragma warning disable CS0618 // Type or member is obsolete
            if (deltaX == 0 && deltaY == 0)
            {
                linePrimary = new InfBufLine(0, center, buf);
                linePerp = new InfBufLine(double.PositiveInfinity, center, buf);
            }
            else
            {
                linePrimary = new InfBufLine(deltaY / deltaX, center, buf);
                double length = Math.Sqrt(deltaX * deltaX + deltaY * deltaY);
                linePerp = new InfBufLine(-deltaX / deltaY, center,
                    length / 2 + perpExtent);
            }
#pragma warning restore CS0618 // Type or member is obsolete

            double minY, maxY;
            double minX, maxX;
            if (deltaX == 0)
            { // vertical
                if (pA.Y <= pB.Y)
                {
                    minY = pA.Y;
                    maxY = pB.Y;
                }
                else
                {
                    minY = pB.Y;
                    maxY = pA.Y;
                }
                minX = pA.X - buf;
                maxX = pA.X + buf;
                minY = minY - perpExtent;
                maxY = maxY + perpExtent;

            }
            else
            {
                if (!bufExtend)
                {
                    throw new NotSupportedException("TODO");
                    //solve for B & A (C=buf), one is buf-x, other is buf-y.
                }

                //Given a right triangle of A, B, C sides, C (hypotenuse) ==
                // buf, and A + B == the bounding box offset from pA & pB in x & y.
                double bboxBuf = buf * (1 + Math.Abs(linePrimary.Slope))
                    * linePrimary.DistDenomInv;
                Debug.Assert(bboxBuf >= buf && bboxBuf <= buf * 1.5);

                if (pA.X <= pB.X)
                {
                    minX = pA.X - bboxBuf;
                    maxX = pB.X + bboxBuf;
                }
                else
                {
                    minX = pB.X - bboxBuf;
                    maxX = pA.X + bboxBuf;
                }
                if (pA.Y <= pB.Y)
                {
                    minY = pA.Y - bboxBuf;
                    maxY = pB.Y + bboxBuf;
                }
                else
                {
                    minY = pB.Y - bboxBuf;
                    maxY = pA.Y + bboxBuf;
                }

            }
            IRectangle bounds = ctx.WorldBounds;

            bbox = ctx.MakeRectangle(
                Math.Max(bounds.MinX, minX),
                Math.Min(bounds.MaxX, maxX),
                Math.Max(bounds.MinY, minY),
                Math.Min(bounds.MaxY, maxY));
        }

        public virtual bool IsEmpty => pA.IsEmpty;


        public virtual IShape GetBuffered(double distance, SpatialContext ctx)
        {
            return new BufferedLine(pA, pB, buf + distance, ctx);
        }

        /// <summary>
        /// Calls <see cref="DistanceUtils.CalcLonDegreesAtLat(double, double)"/> given <paramref name="pA"/>
        /// or <paramref name="pB"/>'s latitude;
        /// whichever is farthest. It's useful to expand a buffer of a line segment when used in
        /// a geospatial context to cover the desired area.
        /// </summary>
        /// <exception cref="ArgumentNullException"><paramref name="pA"/> or <paramref name="pB"/> is <c>null</c>.</exception>
        public static double ExpandBufForLongitudeSkew(IPoint pA, IPoint pB,
                                                       double buf)
        {
            if (pA is null)
                throw new ArgumentNullException(nameof(pA)); // spatial4n specific - use ArgumentNullException instead of NullReferenceException
            if (pB is null)
                throw new ArgumentNullException(nameof(pB)); // spatial4n specific - use ArgumentNullException instead of NullReferenceException

            double absA = Math.Abs(pA.Y);
            double absB = Math.Abs(pB.Y);
            double maxLat = Math.Max(absA, absB);
            double newBuf = DistanceUtils.CalcLonDegreesAtLat(maxLat, buf);
            //    if (newBuf + maxLat >= 90) {
            //      //TODO substitute spherical cap ?
            //    }
            Debug.Assert(newBuf >= buf);
            return newBuf;
        }


        public virtual SpatialRelation Relate(IShape other)
        {
            if (other is IPoint point)
                return Contains(point) ? SpatialRelation.Contains : SpatialRelation.Disjoint;
            if (other is IRectangle rectangle)
                return Relate(rectangle);
            throw new NotSupportedException();
        }

        /// <exception cref="ArgumentNullException"><paramref name="r"/> is <c>null</c>.</exception>
        public virtual SpatialRelation Relate(IRectangle r)
        {
            if (r is null)
                throw new ArgumentNullException(nameof(r));// spatial4n specific - use ArgumentNullException instead of NullReferenceException

            //Check BBox for disjoint & within.
            SpatialRelation bboxR = bbox.Relate(r);
            if (bboxR == SpatialRelation.Disjoint || bboxR == SpatialRelation.Within)
                return bboxR;
            //Either CONTAINS, INTERSECTS, or DISJOINT

            IPoint scratch = new Point(0, 0, null);
            IPoint prC = r.Center;
            SpatialRelation result = linePrimary.Relate(r, prC, scratch);
            if (result == SpatialRelation.Disjoint)
                return SpatialRelation.Disjoint;
            SpatialRelation resultOpp = linePerp.Relate(r, prC, scratch);
            if (resultOpp == SpatialRelation.Disjoint)
                return SpatialRelation.Disjoint;
            if (result == resultOpp)//either CONTAINS or INTERSECTS
                return result;
            return SpatialRelation.Intersects;
        }

        /// <exception cref="ArgumentNullException"><paramref name="p"/> is <c>null</c>.</exception>
        public virtual bool Contains(IPoint p)
        {
            if (p is null)
                throw new ArgumentNullException(nameof(p)); // spatial4n specific - use ArgumentNullException instead of NullReferenceException

            //TODO check bbox 1st?
            return linePrimary.Contains(p) && linePerp.Contains(p);
        }

        public virtual IRectangle BoundingBox => bbox;


        public virtual bool HasArea => buf > 0;


        public virtual double GetArea(SpatialContext? ctx)
        {
            return linePrimary.Buf * linePerp.Buf * 4;
        }


        public virtual IPoint Center => BoundingBox.Center;

        public virtual IPoint A => pA;

        public virtual IPoint B => pB;

        public virtual double Buf => buf;

        /// <summary>
        /// INTERNAL
        /// </summary>
        [Obsolete("This property will be removed from the public API in 0.5.0."), System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
        public virtual InfBufLine LinePrimary => linePrimary;

        /// <summary>
        /// INTERNAL
        /// </summary>
        [Obsolete("This property will be removed from the public API in 0.5.0."), System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
        public virtual InfBufLine LinePerp => linePerp;


        public override string ToString()
        {
            return "BufferedLine(" + pA + ", " + pB + " b=" + buf + ")";
        }


        public override bool Equals(object? o)
        {
            if (this == o) return true;
            if (o is null || GetType() != o.GetType()) return false;

            BufferedLine that = (BufferedLine)o;

            if (that.buf.CompareTo(buf) != 0) return false;
            if (!pA.Equals(that.pA)) return false;
            if (!pB.Equals(that.pB)) return false;

            return true;
        }


        public override int GetHashCode()
        {
            int result;
            long temp;
            result = pA.GetHashCode();
            result = 31 * result + pB.GetHashCode();
            temp = buf != +0.0d ? BitConverter.DoubleToInt64Bits(buf) : 0L;
            result = 31 * result + (int)(temp ^ (long)((ulong)temp >> 32));
            return result;
        }
    }
}
