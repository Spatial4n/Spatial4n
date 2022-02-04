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

using GeoAPI.Geometries;
using NetTopologySuite.Algorithm;
using NetTopologySuite.Geometries;
using System;
using System.Collections.Generic;
using System.Diagnostics;

#if LEGACY_NAMESPACE
using Spatial4n.Core.Context.Nts;
using Spatial4n.Core.Shapes;
using Spatial4n.Core.Shapes.Nts;

namespace Spatial4n.Core.IO.Nts
#else
using Spatial4n.Context.Nts;
using Spatial4n.Shapes;
using Spatial4n.Shapes.Nts;

namespace Spatial4n.IO.Nts
#endif
{
    /// <summary>
    /// Extends <see cref="WktShapeParser"/> adding support for polygons, using NTS.
    /// </summary>
#if LEGACY_NAMESPACE
    [Obsolete("Use Spatial4n.IO.Nts.NtsWktShapeParser instead. This class will be removed in 0.5.0."), System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
#endif
    public class NtsWktShapeParser : WktShapeParser
    {
        new protected readonly NtsSpatialContext m_ctx;

        protected readonly DatelineRule m_datelineRule;
        protected readonly ValidationRule m_validationRule;
        protected readonly bool m_autoIndex;

        /// <summary>
        /// Initializes a new instance of <see cref="NtsWktShapeParser"/>.
        /// </summary>
        /// <param name="ctx">The spatial context.</param>
        /// <param name="factory">The context factory.</param>
        /// <exception cref="ArgumentNullException"><paramref name="ctx"/> or <paramref name="factory"/> is <c>null</c>.</exception>
        public NtsWktShapeParser(NtsSpatialContext ctx, NtsSpatialContextFactory factory)
            : base(ctx, factory)
        {
            if (ctx is null)
                throw new ArgumentNullException(nameof(ctx));// Spatial4n specific: Added guard clause
            if (factory is null)
                throw new ArgumentNullException(nameof(factory)); // Spatial4n specific: Throw ArgumentNullException rather than NullReferenceException

            this.m_ctx = ctx;
            this.m_datelineRule = factory.DatelineRule;
            this.m_validationRule = factory.ValidationRule;
            this.m_autoIndex = factory.AutoIndex;
        }

        /// <summary>
        /// See <see cref="Nts.ValidationRule"/>
        /// </summary>
        public virtual ValidationRule ValidationRule => m_validationRule;

        /// <summary>
        /// NtsGeometry shapes are automatically validated when <see cref="ValidationRule"/> isn't
        /// <c>none</c>.
        /// </summary>
        public virtual bool AutoValidate => m_validationRule != ValidationRule.None;
        [Obsolete("Use AutoValidate property instead. This property will be removed in 0.5.0."), System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
        public virtual bool IsAutoValidate => AutoValidate;

        /// <summary>
        /// If NtsGeometry shapes should be automatically prepared (i.e. optimized) when read via WKT.
        /// <see cref="NtsGeometry.Index()"/>
        /// </summary>
        public virtual bool AutoIndex => m_autoIndex;
        [Obsolete("Use AutoIndex property instead. This property will be removed in 0.5.0."), System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
        public virtual bool IsAutoIndex => m_autoIndex;


        /// <summary>
        /// See <see cref="Nts.DatelineRule"/>
        /// </summary>
        public virtual DatelineRule DatelineRule => m_datelineRule;

        protected override IShape? ParseShapeByType(WktShapeParser.State state, string shapeType)
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

        /// <summary>
        /// Bypasses <see cref="NtsSpatialContext.MakeLineString(IList{Shapes.IPoint})"/> so that we can more
        /// efficiently get the <see cref="LineString"/> without creating a <c>List&lt;Shapes.IPoint&gt;</c>.
        /// </summary>
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

        /// <summary>
        /// Parses a POLYGON shape from the raw string. It might return a <see cref="IRectangle"/>
        /// if the polygon is one.
        /// <code>
        ///   CoordinateSequenceList
        /// </code>
        /// </summary>
        /// <param name="state"></param>
        /// <returns></returns>
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

        protected virtual IRectangle MakeRectFromPoly(IGeometry geometry)
        {
            Debug.Assert(geometry.IsRectangle);
            Envelope env = geometry.EnvelopeInternal;
            bool crossesDateline = false;
            if (m_ctx.IsGeo && m_datelineRule != DatelineRule.None)
            {
                if (m_datelineRule == DatelineRule.CounterClockwiseRectangle)
                {
                    // If NTS says it is clockwise, then it's actually a dateline crossing rectangle.
#pragma warning disable 612, 618
                    crossesDateline = !CGAlgorithms.IsCCW(geometry.Coordinates);
#pragma warning restore 612, 618
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

        /// <summary>
        /// Reads a polygon, returning a NTS polygon.
        /// </summary>
        protected virtual IPolygon Polygon(WktShapeParser.State state)
        {
            GeometryFactory geometryFactory = m_ctx.GeometryFactory;

            IList<Coordinate[]> coordinateSequenceList = CoordinateSequenceList(state);

            ILinearRing shell = geometryFactory.CreateLinearRing(coordinateSequenceList[0]);

            ILinearRing[]? holes = null;
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

        /// <summary>
        /// Parses a MULTIPOLYGON shape from the raw string.
        /// <code>
        ///   '(' polygon (',' polygon )* ')'
        /// </code>
        /// </summary>
        protected virtual IShape ParseMulitPolygonShape(WktShapeParser.State state)
        {
            if (state.NextIfEmptyAndSkipZM())
                return m_ctx.MakeCollection(new List<IShape>());

            IList<IShape> polygons = new List<IShape>();
            state.NextExpect('(');
            do
            {
                polygons.Add(ParsePolygonShape(state));
            } while (state.NextIf(','));
            state.NextExpect(')');

            return m_ctx.MakeCollection(polygons);
        }

        /// <summary>
        /// Reads a list of NTS Coordinate sequences from the current position.
        /// <code>
        ///   '(' coordinateSequence (',' coordinateSequence )* ')'
        /// </code>
        /// </summary>
        protected virtual IList<Coordinate[]> CoordinateSequenceList(WktShapeParser.State state)
        {
            IList<Coordinate[]> sequenceList = new List<Coordinate[]>();
            state.NextExpect('(');
            do
            {
                sequenceList.Add(CoordinateSequence(state));
            } while (state.NextIf(','));
            state.NextExpect(')');
            return sequenceList;
        }

        /// <summary>
        /// Reads a NTS Coordinate sequence from the current position.
        /// <code>
        ///   '(' coordinate (',' coordinate )* ')'
        /// </code>
        /// </summary>
        protected virtual Coordinate[] CoordinateSequence(WktShapeParser.State state)
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

        /// <summary>
        /// Reads a <see cref="GeoAPI.Geometries.Coordinate"/> from the current position.
        /// It's akin to <see cref="WktShapeParser.State"/> but for
        /// a NTS Coordinate.  Only the first 2 numbers are parsed; any remaining are ignored.
        /// </summary>
        /// <param name="state"></param>
        /// <returns></returns>
        protected virtual Coordinate Coordinate(WktShapeParser.State state)
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

        /// <summary>
        /// Creates the NtsGeometry, potentially validating, repairing, and preparing.
        /// </summary>
        protected virtual NtsGeometry MakeShapeFromGeometry(IGeometry geometry)
        {
            bool dateline180Check = DatelineRule != DatelineRule.None;
            NtsGeometry ntsGeom;
            try
            {
                ntsGeom = m_ctx.MakeShape(geometry, dateline180Check, m_ctx.AllowMultiOverlap);
                if (AutoValidate)
                    ntsGeom.Validate();
            }
            catch (Exception e)
            {
                //repair:
                if (m_validationRule == ValidationRule.RepairConvexHull)
                {
                    ntsGeom = m_ctx.MakeShape(geometry.ConvexHull(), dateline180Check, m_ctx.AllowMultiOverlap);
                }
                else if (m_validationRule == ValidationRule.RepairBuffer0)
                {
                    ntsGeom = m_ctx.MakeShape(geometry.Buffer(0), dateline180Check, m_ctx.AllowMultiOverlap);
                }
                else
                {
                    //TODO there are other smarter things we could do like repairing inner holes and subtracting
                    //  from outer repaired shell; but we needn't try too hard.
                    throw e;
                }
            }
            if (AutoIndex)
                ntsGeom.Index();
            return ntsGeom;
        }
    }

    /// <summary>
    /// Indicates the algorithm used to process NTS <see cref="IPolygon"/>s and NTS <see cref="ILineString"/>s for detecting dateline
    /// crossings. It only applies when geo=true.
    /// </summary>
#if LEGACY_NAMESPACE
    [Obsolete("Use Spatial4n.IO.Nts.DatelineRule instead. This enum will be removed in 0.5.0."), System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
#endif
    public enum DatelineRule
    {
        /// <summary>
        /// No polygon will cross the dateline.
        /// </summary>
        None = 0,

        /// <summary>
        /// Adjacent points with an x (longitude) difference that spans more than half
        /// way around the globe will be interpreted as going the other (shorter) way, and thus cross the
        /// dateline.
        /// </summary>
        Width180 = 1,//TODO is there a better name that doesn't have '180' in it?

        /// <summary>
        /// For rectangular polygons, the point order is interpreted as being counter-clockwise (CCW).
        /// However, non-rectangular polygons or other shapes aren't processed this way; they use the
        /// <see cref="Width180"/> rule instead. The CCW rule is specified by OGC Simple Features
        /// Specification v. 1.2.0 section 6.1.11.1.
        /// </summary>
        CounterClockwiseRectangle = 2,
        [Obsolete("Use CounterClockwiseRectangle instead. This value will be removed in 0.5.0."), System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
        CcwRect = 2
    }

    /// <summary>
    /// Indicates how NTS geometries (notably polygons but applies to other geometries too) are
    /// validated (if at all) and repaired (if at all).
    /// </summary>
#if LEGACY_NAMESPACE
    [Obsolete("Use Spatial4n.IO.Nts.ValidationRule instead. This enum will be removed in 0.5.0."), System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
#endif
    public enum ValidationRule
    {
        /// <summary>
        /// Geometries will not be validated (because it's kinda expensive to calculate). You may or may
        /// not ultimately get an error at some point; results are undefined. However, note that
        /// coordinates will still be validated for falling within the world boundaries.
        /// <see cref="Geometry.IsValid"/>.
        /// </summary>
        None,

        /// <summary>
        /// Geometries will be explicitly validated on creation, possibly resulting in an exception:
        /// <see cref="Exceptions.InvalidShapeException"/>.
        /// </summary>
        Error,

        /// <summary>
        /// Invalid Geometries are repaired by taking the convex hull. The result will very likely be a
        /// larger shape that matches false-positives, but no false-negatives.
        /// See <see cref="Geometry.ConvexHull"/>.
        /// </summary>
        RepairConvexHull,

        /// <summary>
        /// Invalid polygons are repaired using the <c>Buffer(0)</c> technique. From the <a
        /// href="http://tsusiatsoftware.net/jts/jts-faq/jts-faq.html">JTS FAQ</a>:
        /// <para>
        /// The buffer operation is fairly insensitive to topological invalidity, and the act of
        /// computing the buffer can often resolve minor issues such as self-intersecting rings. However,
        /// in some situations the computed result may not be what is desired (i.e. the buffer operation
        /// may be "confused" by certain topologies, and fail to produce a result which is close to the
        /// original. An example where this can happen is a "bow-tie: or "figure-8" polygon, with one
        /// very small lobe and one large one. Depending on the orientations of the lobes, the buffer(0)
        /// operation may keep the small lobe and discard the "valid" large lobe).
        /// </para>
        /// </summary>
        RepairBuffer0
    }
}