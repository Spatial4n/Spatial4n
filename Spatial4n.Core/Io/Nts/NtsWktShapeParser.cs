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
using NetTopologySuite.Algorithm;
using NetTopologySuite.Geometries;
using Spatial4n.Core.Context.Nts;
using Spatial4n.Core.Shapes;
using Spatial4n.Core.Shapes.Nts;
using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace Spatial4n.Core.Io.Nts
{
    public class NtsWktShapeParser : WktShapeParser
    {
        protected readonly NtsSpatialContext m_ctx;

        protected readonly DatelineRule m_datelineRule;
        protected readonly ValidationRule m_validationRule;
        protected readonly bool m_autoIndex;

        public NtsWktShapeParser(NtsSpatialContext ctx, NtsSpatialContextFactory factory)
                  : base(ctx, factory)
        {
            this.m_ctx = ctx;
            this.m_datelineRule = factory.datelineRule;
            this.m_validationRule = factory.validationRule;
            this.m_autoIndex = factory.autoIndex;
        }

        /** @see NtsWktShapeParser.ValidationRule */
        public ValidationRule ValidationRule
        {
            get { return m_validationRule; }
        }

        /**
         * NtsGeometry shapes are automatically validated when {@link #getValidationRule()} isn't
         * {@code none}.
         */
        public bool IsAutoValidate
        {
            get { return m_validationRule != Nts.ValidationRule.None; }
        }

        /**
         * If NtsGeometry shapes should be automatically prepared (i.e. optimized) when read via WKT.
         * @see com.spatial4j.core.shape.jts.NtsGeometry#index()
         */
        public bool IsAutoIndex
        {
            get { return m_autoIndex; }
        }


        /** @see DatelineRule */
        public DatelineRule DatelineRule
        {
            get { return m_datelineRule; }
        }

        protected internal override IShape ParseShapeByType(WktShapeParser.State state, string shapeType)
        {
            if (shapeType.Equals("POLYGON", StringComparison.OrdinalIgnoreCase))
            {
                return ParsePolygonShape(state);
            }
            else if (shapeType.Equals("MULTIPOLYGON", StringComparison.OrdinalIgnoreCase))
            {
                return ParseMulitPolygonShape(state);
            }
            return base.ParseShapeByType(state, shapeType);
        }

        /** Bypasses {@link NtsSpatialContext#makeLineString(java.util.List)} so that we can more
         * efficiently get the LineString without creating a {@code List<Point>}.
         */
        protected override IShape ParseLineStringShape(WktShapeParser.State state)
        {
            if (!m_ctx.UseNtsLineString)
                return base.ParseLineStringShape(state);

            if (state.NextIfEmptyAndSkipZM())
                return m_ctx.MakeLineString(new List<Shapes.IPoint>());

            GeometryFactory geometryFactory = m_ctx.GeometryFactory;

            Coordinate[] coordinates = CoordinateSequence(state);
            return MakeShapeFromGeometry(geometryFactory.CreateLineString(coordinates));
        }

        /**
         * Parses a POLYGON shape from the raw string. It might return a {@link com.spatial4j.core.shape.Rectangle}
         * if the polygon is one.
         * <pre>
         *   coordinateSequenceList
         * </pre>
         */
        protected virtual IShape ParsePolygonShape(WktShapeParser.State state)
        {
            IGeometry geometry;
            if (state.NextIfEmptyAndSkipZM())
            {
                GeometryFactory geometryFactory = m_ctx.GeometryFactory;
                geometry = geometryFactory.CreatePolygon(geometryFactory.CreateLinearRing(
                    new Coordinate[] { }), null);
            }
            else
            {
                geometry = Polygon(state);
                if (geometry.IsRectangle)
                {
                    //TODO although, might want to never convert if there's a semantic difference (e.g. geodetically)
                    return MakeRectFromPoly(geometry);
                }
            }
            return MakeShapeFromGeometry(geometry);
        }

        protected IRectangle MakeRectFromPoly(IGeometry geometry)
        {
            Debug.Assert(geometry.IsRectangle);
            Envelope env = geometry.EnvelopeInternal;
            bool crossesDateline = false;
            if (m_ctx.IsGeo && m_datelineRule != Nts.DatelineRule.None)
            {
                if (m_datelineRule == Nts.DatelineRule.CcwRect)
                {
                    // If NTS says it is clockwise, then it's actually a dateline crossing rectangle.
                    crossesDateline = !CGAlgorithms.IsCCW(geometry.Coordinates);
                }
                else
                {
                    crossesDateline = env.Width > 180;
                }
            }
            if (crossesDateline)
                return m_ctx.MakeRectangle(env.MaxX, env.MinX, env.MinY, env.MaxY);
            else
                return m_ctx.MakeRectangle(env.MinX, env.MaxX, env.MinY, env.MaxY);
        }

        /**
         * Reads a polygon, returning a NTS polygon.
         */
        protected IPolygon Polygon(WktShapeParser.State state)
        {
            GeometryFactory geometryFactory = m_ctx.GeometryFactory;

            List<Coordinate[]> coordinateSequenceList = CoordinateSequenceList(state);

            ILinearRing shell = geometryFactory.CreateLinearRing
        (coordinateSequenceList[0]);

            ILinearRing[] holes = null;
            if (coordinateSequenceList.Count > 1)
            {
                holes = new ILinearRing[coordinateSequenceList.Count - 1];
                for (int i = 1; i < coordinateSequenceList.Count; i++)
                {
                    holes[i - 1] = geometryFactory.CreateLinearRing(coordinateSequenceList[i]);
                }
            }
            return geometryFactory.CreatePolygon(shell, holes);
        }

        /**
         * Parses a MULTIPOLYGON shape from the raw string.
         * <pre>
         *   '(' polygon (',' polygon )* ')'
         * </pre>
         */
        protected IShape ParseMulitPolygonShape(WktShapeParser.State state)
        {
            if (state.NextIfEmptyAndSkipZM())
                return m_ctx.MakeCollection(new List<IShape>());

            List<IShape> polygons = new List<IShape>();
            state.NextExpect('(');
            do
            {
                polygons.Add(ParsePolygonShape(state));
            } while (state.NextIf(','));
            state.NextExpect(')');

            return m_ctx.MakeCollection(polygons);
        }


        /**
         * Reads a list of NTS Coordinate sequences from the current position.
         * <pre>
         *   '(' coordinateSequence (',' coordinateSequence )* ')'
         * </pre>
         */
        protected List<Coordinate[]> CoordinateSequenceList(WktShapeParser.State state)
        {
            List<Coordinate[]> sequenceList = new List<Coordinate[]>();
            state.NextExpect('(');
            do
            {
                sequenceList.Add(CoordinateSequence(state));
            } while (state.NextIf(','));
            state.NextExpect(')');
            return sequenceList;
        }

        /**
         * Reads a NTS Coordinate sequence from the current position.
         * <pre>
         *   '(' coordinate (',' coordinate )* ')'
         * </pre>
         */
        protected Coordinate[] CoordinateSequence(WktShapeParser.State state)
        {
            List<Coordinate> sequence = new List<Coordinate>();
            state.NextExpect('(');
            do
            {
                sequence.Add(Coordinate(state));
            } while (state.NextIf(','));
            state.NextExpect(')');
            return sequence.ToArray(/*new Coordinate[sequence.Count]*/);
        }

        /**
         * Reads a {@link com.vividsolutions.jts.geom.Coordinate} from the current position.
         * It's akin to {@link #point(com.spatial4j.core.io.WktShapeParser.State)} but for
         * a NTS Coordinate.  Only the first 2 numbers are parsed; any remaining are ignored.
         */
        protected Coordinate Coordinate(WktShapeParser.State state)
        {
            double x = m_ctx.NormX(state.NextDouble());
            m_ctx.VerifyX(x);
            double y = m_ctx.NormY(state.NextDouble());
            m_ctx.VerifyY(y);
            state.SkipNextDoubles();
            return new Coordinate(x, y);
        }

        protected override double NormDist(double v)
        {
            return m_ctx.GeometryFactory.PrecisionModel.MakePrecise(v);
        }

        /** Creates the NtsGeometry, potentially validating, repairing, and preparing. */
        protected NtsGeometry MakeShapeFromGeometry(IGeometry geometry)
        {
            bool dateline180Check = DatelineRule != Nts.DatelineRule.None;
            NtsGeometry ntsGeom;
            try
            {
                ntsGeom = m_ctx.MakeShape(geometry, dateline180Check, m_ctx.IsAllowMultiOverlap);
                if (IsAutoValidate)
                    ntsGeom.Validate();
            }
            catch (ApplicationException e)
            {
                //repair:
                if (m_validationRule == Nts.ValidationRule.RepairConvexHull)
                {
                    ntsGeom = m_ctx.MakeShape(geometry.ConvexHull(), dateline180Check, m_ctx.IsAllowMultiOverlap);
                }
                else if (m_validationRule == Nts.ValidationRule.RepairBuffer0)
                {
                    ntsGeom = m_ctx.MakeShape(geometry.Buffer(0), dateline180Check, m_ctx.IsAllowMultiOverlap);
                }
                else
                {
                    //TODO there are other smarter things we could do like repairing inner holes and subtracting
                    //  from outer repaired shell; but we needn't try too hard.
                    throw e;
                }
            }
            if (IsAutoIndex)
                ntsGeom.Index();
            return ntsGeom;
        }
    }

    /**
     * Indicates the algorithm used to process NTS Polygons and NTS LineStrings for detecting dateline
     * crossings. It only applies when geo=true.
     */
    public enum DatelineRule
    {
        /** No polygon will cross the dateline. */
        None,

        /** Adjacent points with an x (longitude) difference that spans more than half
         * way around the globe will be interpreted as going the other (shorter) way, and thus cross the
         * dateline.
         */
        Width180,//TODO is there a better name that doesn't have '180' in it?

        /** For rectangular polygons, the point order is interpreted as being counter-clockwise (CCW).
         * However, non-rectangular polygons or other shapes aren't processed this way; they use the
         * {@link #width180} rule instead. The CCW rule is specified by OGC Simple Features
         * Specification v. 1.2.0 section 6.1.11.1.
         */
        CcwRect
    }

    /** Indicates how NTS geometries (notably polygons but applies to other geometries too) are
     * validated (if at all) and repaired (if at all).
     */
    public enum ValidationRule
    {
        /** Geometries will not be validated (because it's kinda expensive to calculate). You may or may
         * not ultimately get an error at some point; results are undefined. However, note that
         * coordinates will still be validated for falling within the world boundaries.
         * @see com.vividsolutions.jts.geom.Geometry#isValid(). */
        None,

        /** Geometries will be explicitly validated on creation, possibly resulting in an exception:
         * {@link com.spatial4j.core.exception.InvalidShapeException}. */
        Error,

        /** Invalid Geometries are repaired by taking the convex hull. The result will very likely be a
         * larger shape that matches false-positives, but no false-negatives.
         * See {@link com.vividsolutions.jts.geom.Geometry#convexHull()}. */
        RepairConvexHull,

        /** Invalid polygons are repaired using the {@code buffer(0)} technique. From the <a
         * href="http://tsusiatsoftware.net/jts/jts-faq/jts-faq.html">JTS FAQ</a>:
         * <p>The buffer operation is fairly insensitive to topological invalidity, and the act of
         * computing the buffer can often resolve minor issues such as self-intersecting rings. However,
         * in some situations the computed result may not be what is desired (i.e. the buffer operation
         * may be "confused" by certain topologies, and fail to produce a result which is close to the
         * original. An example where this can happen is a "bow-tie: or "figure-8" polygon, with one
         * very small lobe and one large one. Depending on the orientations of the lobes, the buffer(0)
         * operation may keep the small lobe and discard the "valid" large lobe).
         * </p> */
        RepairBuffer0
    }
}
