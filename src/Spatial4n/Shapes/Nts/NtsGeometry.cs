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

using GeoAPI.Geometries;
using GeoAPI.Geometries.Prepared;
using NetTopologySuite.Geometries;
using NetTopologySuite.Geometries.Prepared;
using NetTopologySuite.Operation.Union;
using NetTopologySuite.Operation.Valid;
using Spatial4n.Context;
using Spatial4n.Context.Nts;
using Spatial4n.Exceptions;
using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace Spatial4n.Shapes.Nts
{
    /// <summary>
    /// Wraps a NTS <see cref="IGeometry"/> (i.e. may be a polygon or basically anything).
    /// NTS's does a great deal of the hard work, but there is work here in handling
    /// dateline wrap.
    /// </summary>
    public class NtsGeometry : IShape
    {
        [Obsolete("Set AssertValidate to true or false rather than configuring an environment variable. This static field will be removed in 0.5.0."), System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
        public static readonly string SYSPROP_ASSERT_VALIDATE = "spatial4j.NtsGeometry.assertValidate";

        /// <summary>
        /// When set to <c>false</c>, the automatic validation on instances of <see cref="NtsGeometry"/> will be disabled.
        /// The default setting is <c>true</c>.
        /// </summary>
        public static bool AssertValidate { get; set; } = true;

        private readonly IGeometry geom;//cannot be a direct instance of GeometryCollection as it doesn't support relate()
        private readonly bool _hasArea;
        private readonly IRectangle bbox;
        protected readonly NtsSpatialContext ctx;
        protected IPreparedGeometry? preparedGeometry;
        protected bool validated = false;

        /// <summary>
        /// Initializes a new instance of <see cref="NtsGeometry"/>.
        /// </summary>
        /// <param name="geom">The geometry.</param>
        /// <param name="ctx">The spatial context.</param>
        /// <param name="dateline180Check">Unwraps the geometry across the dateline so it exceeds the standard geo bounds (-180 to +180).</param>
        /// <param name="allowMultiOverlap">If given multiple overlapping polygons, fix it by union.</param>
        /// <exception cref="ArgumentNullException"><paramref name="geom"/> or <paramref name="ctx"/> is <c>null</c>.</exception>
        /// <exception cref="ArgumentException"><paramref name="geom"/> is a <see cref="GeometryCollection"/>. <see cref="NtsGeometry"/>
        /// does not support <see cref="GeometryCollection"/> but does support its subclasses.</exception>
        public NtsGeometry(IGeometry geom, NtsSpatialContext ctx, bool dateline180Check, bool allowMultiOverlap)
        {
            if (geom is null)
                throw new ArgumentNullException(nameof(geom)); // spatial4n specific - use ArgumentNullException instead of NullReferenceException

            this.ctx = ctx ?? throw new ArgumentNullException(nameof(ctx)); // spatial4n specific - use ArgumentNullException instead of NullReferenceException

            //GeometryCollection isn't supported in relate()
            if (geom.GetType() == typeof(GeometryCollection))
                throw new ArgumentException("NtsGeometry does not support GeometryCollection but does support its subclasses.");

            //NOTE: All this logic is fairly expensive. There are some short-circuit checks though.
            if (ctx.IsGeo)
            {
                //Unwraps the geometry across the dateline so it exceeds the standard geo bounds (-180 to +180).
                if (dateline180Check)
                    UnwrapDateline(geom); //potentially modifies geom
                //If given multiple overlapping polygons, fix it by union
                if (allowMultiOverlap)
                    geom = UnionGeometryCollection(geom); //returns same or new geom

                //Cuts an unwrapped geometry back into overlaid pages in the standard geo bounds.
                geom = CutUnwrappedGeomInto360(geom); //returns same or new geom

                // Spatial4n specific - keep the validation in place to warn users if they do something wrong (these were asserts in Java)
                if (geom.EnvelopeInternal.Width > 360)
                    throw new InvalidShapeException("EnvelopeInternal.Width must be less than or equal to 360.");
                if (geom.GetType() == typeof(GeometryCollection))
                    throw new ArgumentException("NtsGeometry does not support GeometryCollection but does support its subclasses."); //double check

                //Compute bbox
                bbox = ComputeGeoBBox(geom);
            }
            else
            {//not geo
                if (allowMultiOverlap)
                    geom = UnionGeometryCollection(geom);//returns same or new geom
                Envelope env = geom.EnvelopeInternal;
                bbox = new Rectangle(env.MinX, env.MaxX, env.MinY, env.MaxY, ctx);
            }
            var _ = geom.EnvelopeInternal;//ensure envelope is cached internally, which is lazy evaluated. Keeps this thread-safe.

            this.geom = geom;
            DoAssertValidate();//kinda expensive but caches valid state - in Spatial4n we have to make this available in the compile by not using Debug.Assert().

            this._hasArea = !((geom is ILineal) || (geom is IPuntal));
        }

        /// <summary>
        /// called via assertion
        /// </summary>
        private void DoAssertValidate()
        {
            if (AssertValidate)
                Validate();
        }

        /// <summary>
        /// Validates the shape, throwing a descriptive error if it isn't valid. Note that this
        /// is usually called automatically by default, but that can be disabled by setting <see cref="AssertValidate"/>
        /// to <c>false</c>.
        /// </summary>
        /// <exception cref="InvalidShapeException">with descriptive error if the shape isn't valid.</exception>
        public virtual void Validate()
        {
            if (!validated)
            {
                IsValidOp isValidOp = new IsValidOp(geom);
                if (!isValidOp.IsValid)
                    throw new InvalidShapeException(isValidOp.ValidationError.ToString());
                validated = true;
            }
        }

        /// <summary>
        /// Adds an index to this class internally to compute spatial relations faster. In NTS this
        /// is called a <see cref="IPreparedGeometry"/>.  This
        /// isn't done by default because it takes some time to do the optimization, and it uses more
        /// memory.  Calling this method isn't thread-safe so be careful when this is done. If it was
        /// already indexed then nothing happens.
        /// </summary>
        public virtual void Index()
        {
            if (preparedGeometry is null)
                preparedGeometry = PreparedGeometryFactory.Prepare(geom);
        }


        public virtual bool IsEmpty => geom.IsEmpty;

        /// <summary>
        /// Given <paramref name="geoms"/> which has already been checked for being in world
        /// bounds, return the minimal longitude range of the bounding box.
        /// </summary>
        /// <exception cref="ArgumentNullException"><paramref name="geoms"/> is <c>null</c>.</exception>
        protected virtual IRectangle ComputeGeoBBox(IGeometry geoms)
        {
            if (geoms is null)
                throw new ArgumentNullException(nameof(geoms)); // spatial4n specific - use ArgumentNullException instead of NullReferenceException

            if (geoms.IsEmpty)
                return new Rectangle(double.NaN, double.NaN, double.NaN, double.NaN, ctx);
            Envelope env = geoms.EnvelopeInternal;//for minY & maxY (simple)
            if (env.Width > 180 && geoms.NumGeometries > 1)
            {
                // This is ShapeCollection's bbox algorithm
#pragma warning disable CS0618 // Type or member is obsolete
                Range? xRange = null;
#pragma warning restore CS0618 // Type or member is obsolete
                for (int i = 0; i < geoms.NumGeometries; i++)
                {
                    Envelope envI = geoms.GetGeometryN(i).EnvelopeInternal;
#pragma warning disable CS0618 // Type or member is obsolete
                    Range xRange2 = new Range.LongitudeRange(envI.MinX, envI.MaxX);
#pragma warning restore CS0618 // Type or member is obsolete
                    if (xRange is null)
                    {
                        xRange = xRange2;
                    }
                    else
                    {
                        xRange = xRange.ExpandTo(xRange2);
                    }
#pragma warning disable CS0618 // Type or member is obsolete
                    if (xRange == Range.LongitudeRange.WORLD_180E180W)
#pragma warning restore CS0618 // Type or member is obsolete
                        break; // can't grow any bigger
                }
                return new Rectangle(xRange!.Min, xRange.Max, env.MinY, env.MaxY, ctx);
            }
            else
            {
                return new Rectangle(env.MinX, env.MaxX, env.MinY, env.MaxY, ctx);
            }
        }

        /// <exception cref="ArgumentNullException"><paramref name="ctx"/> is <c>null</c>.</exception>
        public virtual IShape GetBuffered(double distance, SpatialContext ctx)
        {
            if (ctx is null)
                throw new ArgumentNullException(nameof(ctx)); // spatial4n specific - use ArgumentNullException instead of NullReferenceException

            //TODO doesn't work correctly across the dateline. The buffering needs to happen
            // when it's transiently unrolled, prior to being sliced.
            return this.ctx.MakeShape(geom.Buffer(distance), true, true);
        }

        public virtual bool HasArea => _hasArea;

        public virtual double GetArea(SpatialContext? ctx)
        {
            double geomArea = geom.Area;
            if (ctx is null || geomArea == 0)
                return geomArea;
            //Use the area proportional to how filled the bbox is.
            double bboxArea = BoundingBox.GetArea(null);//plain 2d area
            Debug.Assert(bboxArea >= geomArea);
            double filledRatio = geomArea / bboxArea;
            return BoundingBox.GetArea(ctx) * filledRatio;
            // (Future: if we know we use an equal-area projection then we don't need to
            //  estimate)
        }

        public virtual IRectangle BoundingBox => bbox;

        public virtual IPoint Center
        {
            get
            {
                if (IsEmpty) //geom.getCentroid is null
#pragma warning disable CS8600 // Converting null literal or possible null value to non-nullable type.
                    return new NtsPoint(ctx.GeometryFactory.CreatePoint((Coordinate)null), ctx);
#pragma warning restore CS8600 // Converting null literal or possible null value to non-nullable type.
                return new NtsPoint(geom.Centroid, ctx);
            }
        }

        /// <exception cref="ArgumentNullException"><paramref name="other"/> is <c>null</c>.</exception>
        public virtual SpatialRelation Relate(IShape other)
        {
            if (other is null)
                throw new ArgumentNullException(nameof(other)); // spatial4n specific - use ArgumentNullException instead of NullReferenceException

            if (other is IPoint point)
                return Relate(point);
            else if (other is IRectangle rectangle)
                return Relate(rectangle);
            else if (other is ICircle circle)
                return Relate(circle);
            else if (other is NtsGeometry geometry)
                return Relate(geometry);
            else if (other is BufferedLineString)
                throw new NotSupportedException("Can't use BufferedLineString with NtsGeometry");
            return other.Relate(this).Transpose();
        }

        /// <exception cref="ArgumentNullException"><paramref name="pt"/> is <c>null</c>.</exception>
        public virtual SpatialRelation Relate(IPoint pt)
        {
            if (pt is null)
                throw new ArgumentNullException(nameof(pt)); // spatial4n specific - use ArgumentNullException instead of NullReferenceException

            if (!BoundingBox.Relate(pt).Intersects())
                return SpatialRelation.Disjoint;
            IGeometry ptGeom;
            if (pt is NtsPoint ntsPoint)
                ptGeom = ntsPoint.Geometry;
            else
                ptGeom = ctx.GeometryFactory.CreatePoint(new Coordinate(pt.X, pt.Y));
            return Relate(ptGeom);//is point-optimized
        }

        /// <exception cref="ArgumentNullException"><paramref name="rectangle"/> is <c>null</c>.</exception>
        public virtual SpatialRelation Relate(IRectangle rectangle)
        {
            if (rectangle is null)
                throw new ArgumentNullException(nameof(rectangle)); // spatial4n specific - use ArgumentNullException instead of NullReferenceException

            SpatialRelation bboxR = bbox.Relate(rectangle);
            if (bboxR == SpatialRelation.Within || bboxR == SpatialRelation.Disjoint)
                return bboxR;
            // FYI, the right answer could still be DISJOINT or WITHIN, but we don't know yet.
            return Relate(ctx.GetGeometryFrom(rectangle));
        }

        /// <exception cref="ArgumentNullException"><paramref name="circle"/> is <c>null</c>.</exception>
        public virtual SpatialRelation Relate(ICircle circle)
        {
            if (circle is null)
                throw new ArgumentNullException(nameof(circle)); // spatial4n specific - use ArgumentNullException instead of NullReferenceException

            SpatialRelation bboxR = bbox.Relate(circle);
            if (bboxR == SpatialRelation.Within || bboxR == SpatialRelation.Disjoint)
                return bboxR;

            //Test each point to see how many of them are outside of the circle.
            //TODO consider instead using geom.apply(CoordinateSequenceFilter) -- maybe faster since avoids Coordinate[] allocation
            Coordinate[] coords = geom.Coordinates;
            int outside = 0;
            int i = 0;
            foreach (Coordinate coord in coords)
            {
                i++;
                SpatialRelation sect = circle.Relate(new Point(coord.X, coord.Y, ctx));
                if (sect == SpatialRelation.Disjoint)
                    outside++;
                if (i != outside && outside != 0)//short circuit: partially outside, partially inside
                    return SpatialRelation.Intersects;
            }
            if (i == outside)
            {
                return (Relate(circle.Center) == SpatialRelation.Disjoint)
                    ? SpatialRelation.Disjoint : SpatialRelation.Contains;
            }
            Debug.Assert(outside == 0);
            return SpatialRelation.Within;
        }

        /// <exception cref="ArgumentNullException"><paramref name="ntsGeometry"/> is <c>null</c>.</exception>
        public virtual SpatialRelation Relate(NtsGeometry ntsGeometry)
        {
            if (ntsGeometry is null)
                throw new ArgumentNullException(nameof(ntsGeometry)); // spatial4n specific - use ArgumentNullException instead of NullReferenceException

            //don't bother checking bbox since geom.relate() does this already
            return Relate(ntsGeometry.geom);
        }

        /// <exception cref="ArgumentNullException"><paramref name="oGeom"/> is <c>null</c>.</exception>
        protected virtual SpatialRelation Relate(IGeometry oGeom)
        {
            if (oGeom is null)
                throw new ArgumentNullException(nameof(oGeom)); // spatial4n specific - use ArgumentNullException instead of NullReferenceException

            //see http://docs.geotools.org/latest/userguide/library/jts/dim9.html#preparedgeometry
            if (oGeom is GeoAPI.Geometries.IPoint) // TODO: This may not be the correct data type....
            {
                if (preparedGeometry != null)
                    return preparedGeometry.Disjoint(oGeom) ? SpatialRelation.Disjoint : SpatialRelation.Contains;
                return geom.Disjoint(oGeom) ? SpatialRelation.Disjoint : SpatialRelation.Contains;
            }
            if (preparedGeometry is null)
                return IntersectionMatrixToSpatialRelation(geom.Relate(oGeom));
            else if (preparedGeometry.Covers(oGeom))
                return SpatialRelation.Contains;
            else if (preparedGeometry.CoveredBy(oGeom))
                return SpatialRelation.Within;
            else if (preparedGeometry.Intersects(oGeom))
                return SpatialRelation.Intersects;
            return SpatialRelation.Disjoint;
        }

        /// <exception cref="ArgumentNullException"><paramref name="matrix"/> is <c>null</c>.</exception>
        public static SpatialRelation IntersectionMatrixToSpatialRelation(IntersectionMatrix matrix)
        {
            if (matrix is null)
                throw new ArgumentNullException(nameof(matrix)); // spatial4n specific - use ArgumentNullException instead of NullReferenceException

            //As indicated in SpatialRelation javadocs, Spatial4j CONTAINS & WITHIN are
            // OGC's COVERS & COVEREDBY
            if (matrix.IsCovers())
                return SpatialRelation.Contains;
            else if (matrix.IsCoveredBy())
                return SpatialRelation.Within;
            else if (matrix.IsDisjoint())
                return SpatialRelation.Disjoint;
            return SpatialRelation.Intersects;
        }

        public override string ToString()
        {
            return geom.ToString();
        }

        public override bool Equals(object? o)
        {
            if (this == o) return true;
            if (o is null || GetType() != o.GetType()) return false;

            var that = (NtsGeometry)o;
            return geom.EqualsExact(that.geom);//fast equality for normalized geometries
        }

        public override int GetHashCode()
        {
            //FYI if geometry.equalsExact(that.geometry), then their envelopes are the same.
            return geom.EnvelopeInternal.GetHashCode();
        }

        public virtual IGeometry Geometry => geom;

        private class S4nGeometryFilter : IGeometryFilter
        {
            private readonly int[] crossings;

            public S4nGeometryFilter(int[] crossings)
            {
                this.crossings = crossings;
            }

            public void Filter(IGeometry geom)
            {
                int cross; // Spatial4n: Removed unnecessary assignment
                if (geom is LineString)
                {
                    //note: LinearRing extends LineString
                    if (geom.EnvelopeInternal.Width < 180)
                        return; //can't possibly cross the dateline
                    cross = UnwrapDateline((LineString)geom);
                }
                else if (geom is Polygon)
                {
                    if (geom.EnvelopeInternal.Width < 180)
                        return; //can't possibly cross the dateline
                    cross = UnwrapDateline((Polygon)geom);
                }
                else
                    return;
                crossings[0] = Math.Max(crossings[0], cross);
            }
        }

        /// <summary>
        /// If <paramref name="geom"/> spans the dateline, then this modifies it to be a
        /// valid NTS geometry that extends to the right of the standard -180 to +180
        /// width such that some points are greater than +180 but some remain less.
        /// Takes care to invoke <see cref="IGeometry.GeometryChanged()"/>
        /// if needed.
        /// </summary>
        /// <param name="geom"></param>
        /// <returns>The number of times the geometry spans the dateline.  >= 0</returns>
        /// <exception cref="ArgumentNullException"><paramref name="geom"/> is <c>null</c>.</exception>
        private static int UnwrapDateline(IGeometry geom)
        {
            if (geom is null)
                throw new ArgumentNullException(nameof(geom)); // spatial4n specific - use ArgumentNullException instead of NullReferenceException

            if (geom.EnvelopeInternal.Width < 180)
                return 0;//can't possibly cross the dateline
            int[] crossings = { 0 };//an array so that an inner class can modify it.
            geom.Apply(new S4nGeometryFilter(crossings));

            return crossings[0];
        }

        /// <summary>See <see cref="UnwrapDateline(IGeometry)"/>.</summary>
        /// <exception cref="ArgumentNullException"><paramref name="poly"/> is <c>null</c>.</exception>
        private static int UnwrapDateline(Polygon poly)
        {
            if (poly is null)
                throw new ArgumentNullException(nameof(poly)); // spatial4n specific - use ArgumentNullException instead of NullReferenceException

            var exteriorRing = poly.ExteriorRing;
            int cross = UnwrapDateline(exteriorRing);
            if (cross > 0)
            {
                for (int i = 0; i < poly.NumInteriorRings; i++)
                {
                    var innerLineString = poly.GetInteriorRingN(i);
                    UnwrapDateline(innerLineString);
                    for (int shiftCount = 0; !exteriorRing.Contains(innerLineString); shiftCount++)
                    {
                        if (shiftCount > cross)
                            throw new ArgumentException("The inner ring doesn't appear to be within the exterior: "
                                + exteriorRing + " inner: " + innerLineString);
                        ShiftGeomByX(innerLineString, 360);
                    }
                }
                poly.GeometryChanged();
            }
            return cross;
        }

        /// <summary>See <see cref="UnwrapDateline(IGeometry)"/>.</summary>
        /// <exception cref="ArgumentNullException"><paramref name="lineString"/> is <c>null</c>.</exception>
        private static int UnwrapDateline(ILineString lineString)
        {
            if (lineString is null)
                throw new ArgumentNullException(nameof(lineString)); // spatial4n specific - use ArgumentNullException instead of NullReferenceException

            var cseq = lineString.CoordinateSequence;
            int size = cseq.Count;
            if (size <= 1)
                return 0;

            int shiftX = 0;//invariant: == shiftXPage*360
            int shiftXPage = 0;
            int shiftXPageMin = 0/* <= 0 */, shiftXPageMax = 0; /* >= 0 */
            double prevX = cseq.GetX(0);
            for (int i = 1; i < size; i++)
            {
                double thisX_orig = cseq.GetX(i);
                Debug.Assert(thisX_orig >= -180 && thisX_orig <= 180);// : "X not in geo bounds";
                double thisX = thisX_orig + shiftX;
                if (prevX - thisX > 180)
                {//cross dateline from left to right
                    thisX += 360;
                    shiftX += 360;
                    shiftXPage += 1;
                    shiftXPageMax = Math.Max(shiftXPageMax, shiftXPage);
                }
                else if (thisX - prevX > 180)
                {//cross dateline from right to left
                    thisX -= 360;
                    shiftX -= 360;
                    shiftXPage -= 1;
                    shiftXPageMin = Math.Min(shiftXPageMin, shiftXPage);
                }
                if (shiftXPage != 0)
                    cseq.SetOrdinate(i, Ordinate.X, thisX);
                prevX = thisX;
            }
            if (lineString is LinearRing)
            {
                Debug.Assert(cseq.GetCoordinate(0).Equals(cseq.GetCoordinate(size - 1)));
                Debug.Assert(shiftXPage == 0);//starts and ends at 0
            }
            Debug.Assert(shiftXPageMax >= 0 && shiftXPageMin <= 0);
            //Unfortunately we are shifting again; it'd be nice to be smarter and shift once
            ShiftGeomByX(lineString, shiftXPageMin * -360);
            int crossings = shiftXPageMax - shiftXPageMin;
            if (crossings > 0)
                lineString.GeometryChanged();
            return crossings;
        }

        private class S4nCoordinateSequenceFilter : ICoordinateSequenceFilter
        {
            private readonly int _xShift;

            public S4nCoordinateSequenceFilter(int xShift)
            {
                _xShift = xShift;
            }

            public void Filter(ICoordinateSequence seq, int i)
            {
                seq.SetOrdinate(i, Ordinate.X, seq.GetX(i) + _xShift);
            }

            public bool Done => false;

            public bool GeometryChanged => true;
        };

        private static void ShiftGeomByX(IGeometry geom, int xShift)
        {
            if (xShift == 0)
                return;
            geom.Apply(new S4nCoordinateSequenceFilter(xShift));
        }

        private static IGeometry UnionGeometryCollection(IGeometry geom)
        {
            if (geom is GeometryCollection)
            {
                return geom.Union();
            }
            return geom;
        }

        /// <summary>
        /// This "pages" through standard geo boundaries offset by multiples of 360
        /// longitudinally that intersect geom, and the intersecting results of a page
        /// and the geom are shifted into the standard -180 to +180 and added to a new
        /// geometry that is returned.
        /// </summary>
        /// <exception cref="ArgumentNullException"><paramref name="geom"/> is <c>null</c>.</exception>
        private static IGeometry CutUnwrappedGeomInto360(IGeometry geom)
        {
            if (geom is null)
                throw new ArgumentNullException(nameof(geom)); // spatial4n specific - use ArgumentNullException instead of NullReferenceException

            Envelope geomEnv = geom.EnvelopeInternal;
            if (geomEnv.MinX >= -180 && geomEnv.MaxX <= 180)
                return geom;
            Debug.Assert(geom.IsValid);

            //TODO support geom's that start at negative pages; will avoid need to previously shift in unwrapDateline(geom).
            var geomList = new List<IGeometry>();
            //page 0 is the standard -180 to 180 range
            for (int page = 0; true; page++)
            {
                double minX = -180 + page * 360;
                if (geomEnv.MaxX <= minX)
                    break;
                var rect = (Geometry)geom.Factory.ToGeometry(new Envelope(minX, minX + 360, -90, 90));
                Debug.Assert(rect.IsValid);
                var pageGeom = (Geometry)rect.Intersection(geom);//NTS is doing some hard work
                Debug.Assert(pageGeom.IsValid);

                ShiftGeomByX(pageGeom, page * -360);
                geomList.Add(pageGeom);
            }
            return UnaryUnionOp.Union(geomList);
        }

        //  private static Geometry removePolyHoles(Geometry geom) {
        //    //TODO this does a deep copy of geom even if no changes needed; be smarter
        //    GeometryTransformer gTrans = new GeometryTransformer() {
        //      @Override
        //      protected Geometry transformPolygon(Polygon geom, Geometry parent) {
        //        if (geom.getNumInteriorRing() == 0)
        //          return geom;
        //        return factory.createPolygon((LinearRing) geom.getExteriorRing(),null);
        //      }
        //    };
        //    return gTrans.transform(geom);
        //  }
        //
        //  private static Geometry snapAndClean(Geometry geom) {
        //    return new GeometrySnapper(geom).snapToSelf(GeometrySnapper.computeOverlaySnapTolerance(geom), true);
        //  }
    }
}