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

using GeoAPI;
using GeoAPI.Geometries;
using NetTopologySuite.Geometries;
using NetTopologySuite.Utilities;
using System;
using System.Collections.Generic;

#if LEGACY_NAMESPACE
using Spatial4n.Core.Exceptions;
using Spatial4n.Core.Shapes;
using Spatial4n.Core.Shapes.Nts;
using IPoint = Spatial4n.Core.Shapes.IPoint;

namespace Spatial4n.Core.Context.Nts
#else
using Spatial4n.Exceptions;
using Spatial4n.Shapes;
using Spatial4n.Shapes.Nts;
using IPoint = Spatial4n.Shapes.IPoint;

namespace Spatial4n.Context.Nts
#endif
{
    /// <summary>
    /// Enhances the default <see cref="SpatialContext"/> with support for Polygons (and
    /// other geometry) plus
    /// reading <a href="http://en.wikipedia.org/wiki/Well-known_text">WKT</a>. The
    /// popular <a href="https://github.com/NetTopologySuite/NetTopologySuite">NetTopologySuite (NTS)</a>
    /// library does the heavy lifting.
    /// </summary>
#if LEGACY_NAMESPACE
    [Obsolete("Use Spatial4n.Context.Nts.NtsSpatialContext instead. This class will be removed in 0.5.0."), System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
#endif
    public class NtsSpatialContext : SpatialContext
    {
        [Obsolete("Use Geo static property instead. This field will be removed in 0.5.0."), System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never), CLSCompliant(false)]
        public new static readonly NtsSpatialContext GEO = LoadGeo();

        public new static NtsSpatialContext Geo
#pragma warning disable CS0618 // Type or member is obsolete
            => GEO;
#pragma warning restore CS0618 // Type or member is obsolete

        private static NtsSpatialContext LoadGeo()
        {
#if NETSTANDARD
            // spatial4n specific - need to bootstrap GeoAPI with
            // the NetTopologySuite geometry. See: 
            // https://github.com/NetTopologySuite/NetTopologySuite/issues/189#issuecomment-324844404

            NetTopologySuiteBootstrapper.Bootstrap();
#endif
            return new NtsSpatialContext(new NtsSpatialContextFactory { IsGeo = true });
        }


        protected readonly GeometryFactory m_geometryFactory;

        protected readonly bool m_allowMultiOverlap;
        protected readonly bool m_useNtsPoint;
        protected readonly bool m_useNtsLineString;

        /// <summary>
        /// Called by <see cref="NtsSpatialContextFactory.CreateSpatialContext()"/>.
        /// </summary>
        /// <param name="factory"></param>
        public NtsSpatialContext(NtsSpatialContextFactory factory)
            : base(factory)
        {
            this.m_geometryFactory = factory.GeometryFactory;

            this.m_allowMultiOverlap = factory.AllowMultiOverlap;
            this.m_useNtsPoint = factory.UseNtsPoint;
            this.m_useNtsLineString = factory.UseNtsLineString;
        }

        /// <summary>
        /// If geom might be a multi geometry of some kind, then might multiple
        /// component geometries overlap? Strict OGC says this is invalid but we
        /// can accept it by computing the union. Note: Our ShapeCollection mostly
        /// doesn't care but it has a method related to this
        /// <see cref="Shapes.ShapeCollection.RelateContainsShortCircuits()"/>.
        /// </summary>
        public virtual bool AllowMultiOverlap => m_allowMultiOverlap;

        [Obsolete("Use AllowMultiOverlap property instead. This property will be removed in 0.5.0."), System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never), CLSCompliant(false)]
        public virtual bool IsAllowMultiOverlap => m_allowMultiOverlap;

        public override double NormX(double x)
        {
            x = base.NormX(x);
            return m_geometryFactory.PrecisionModel.MakePrecise(x);
        }

        public override double NormY(double y)
        {
            y = base.NormY(y);
            return m_geometryFactory.PrecisionModel.MakePrecise(y);
        }

#pragma warning disable 672
        public override string ToString(IShape shape)
#pragma warning restore 672
        {
            //Note: this logic is from the defunct NtsShapeReadWriter
            if (shape is NtsGeometry ntsGeom)
            {
                return ntsGeom.Geometry.AsText();
            }
            //Note: doesn't handle ShapeCollection or BufferedLineString
#pragma warning disable 612, 618
            return base.ToString(shape);
#pragma warning restore 612, 618
        }

        /// <summary>
        /// Gets a NTS <see cref="IGeometry"/> for the given <see cref="IShape"/>. Some shapes hold a
        /// NTS geometry whereas new ones must be created for the rest.
        /// </summary>
        /// <param name="shape">Not null</param>
        /// <returns>Not null</returns>
        public virtual IGeometry GetGeometryFrom(IShape shape)
        {
            if (shape is NtsGeometry ntsGeometry)
            {
                return ntsGeometry.Geometry;
            }
            if (shape is NtsPoint ntsPoint)
            {
                return ntsPoint.Geometry;
            }

            if (shape is IPoint point)
            {
                return m_geometryFactory.CreatePoint(new Coordinate(point.X, point.Y));
            }

            if (shape is IRectangle r)
            {

                if (r.CrossesDateLine)
                {
                    var pair = new List<IGeometry>(2)
                       {
                           m_geometryFactory.ToGeometry(new Envelope(
                                                          r.MinX, WorldBounds.MaxX, r.MinY, r.MaxY)),
                           m_geometryFactory.ToGeometry(new Envelope(
                                                          WorldBounds.MinX, r.MaxX, r.MinY, r.MaxY))
                       };
                    return m_geometryFactory.BuildGeometry(pair);//a MultiPolygon or MultiLineString
                }
                else
                {
                    return m_geometryFactory.ToGeometry(new Envelope(r.MinX, r.MaxX, r.MinY, r.MaxY));
                }
            }

            if (shape is ICircle circle)
            {
                // TODO, this should maybe pick a bunch of points
                // and make a circle like:
                //  http://docs.codehaus.org/display/GEOTDOC/01+How+to+Create+a+Geometry#01HowtoCreateaGeometry-CreatingaCircle
                // If this crosses the dateline, it could make two parts
                // is there an existing utility that does this?

                if (circle.BoundingBox.CrossesDateLine)
                    throw new ArgumentException("Doesn't support dateline cross yet: " + circle);//TODO
                var gsf = new GeometricShapeFactory(m_geometryFactory)
                {
                    Size = circle.BoundingBox.Width / 2.0f,
                    NumPoints = 4 * 25,//multiple of 4 is best
                    Centre = new Coordinate(circle.Center.X, circle.Center.Y)
                };
                return gsf.CreateCircle();
            }
            throw new InvalidShapeException("can't make Geometry from: " + shape);
        }

        // Should {@link #makePoint(double, double)} return {@link NtsPoint}?
        public virtual bool UseNtsPoint => m_useNtsPoint;

        public override IPoint MakePoint(double x, double y)
        {
            if (!UseNtsPoint)
                return base.MakePoint(x, y);
            //A Nts Point is fairly heavyweight!  TODO could/should we optimize this?
            VerifyX(x);
            VerifyY(y);
            Coordinate? coord = double.IsNaN(x) ? null : new Coordinate(x, y);
            return new NtsPoint(m_geometryFactory.CreatePoint(coord), this);
        }

        // Should MakeLineString(IList{IPoint}) return NtsGeometry? 
        public virtual bool UseNtsLineString =>
            //BufferedLineString doesn't yet do dateline cross, and can't yet be relate()'ed with a
            // NTS geometry
            m_useNtsLineString;

        public override IShape MakeLineString(IList<IPoint> points)
        {
            if (!m_useNtsLineString)
                return base.MakeLineString(points);
            //convert List<Point> to Coordinate[]
            Coordinate[] coords = new Coordinate[points.Count];
            for (int i = 0; i < coords.Length; i++)
            {
                IPoint p = points[i];
                if (p is NtsPoint ntsPoint)
                {
                    coords[i] = ntsPoint.Geometry.Coordinate;
                }
                else
                {
                    coords[i] = new Coordinate(p.X, p.Y);
                }
            }
            ILineString lineString = m_geometryFactory.CreateLineString(coords);
            return MakeShape(lineString);
        }

        /// <summary>
        /// INTERNAL
        /// <see cref="MakeShape(IGeometry)"/>
        /// </summary>
        /// <param name="geom">Non-null</param>
        /// <param name="dateline180Check">
        /// if both this is true and <see cref="SpatialContextFactory.geo"/>, then NtsGeometry will check
        /// for adjacent coordinates greater than 180 degrees longitude apart, and
        /// it will do tricks to make that line segment (and the shape as a whole)
        /// cross the dateline even though NTS doesn't have geodetic support.
        /// </param>
        /// <param name="allowMultiOverlap"><see cref="IsAllowMultiOverlap"/></param>
        public virtual NtsGeometry MakeShape(IGeometry geom, bool dateline180Check, bool allowMultiOverlap)
        {
            return new NtsGeometry(geom, this, dateline180Check, allowMultiOverlap);
        }

        /// <summary>
        /// INTERNAL: Creates a <see cref="IShape"/> from a NTS <see cref="IGeometry"/>. Generally, this shouldn't be
        /// called when one of the other factory methods are available, such as for points. The caller
        /// needs to have done some verification/normalization of the coordinates by now, if any.
        /// </summary>
        public virtual NtsGeometry MakeShape(IGeometry geom)
        {
            return MakeShape(geom, dateline180Check: true, m_allowMultiOverlap);
        }

        public virtual GeometryFactory GeometryFactory => m_geometryFactory;

        public override string ToString()
        {
            if (this.Equals(Geo))
            {
                return $"{Geo.GetType().Name}.{nameof(Geo)}";
            }
            else
            {
                return base.ToString();
            }
        }
    }
}