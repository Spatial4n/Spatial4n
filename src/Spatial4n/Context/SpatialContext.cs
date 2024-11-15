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

using Spatial4n.Distance;
using Spatial4n.Exceptions;
using Spatial4n.IO;
using Spatial4n.Shapes;
using System;
using System.Collections.Generic;

namespace Spatial4n.Context
{
    /// <summary>
    /// This is a facade to most of Spatial4n, holding things like <see cref="IDistanceCalculator"/>, 
    /// <see cref="WktShapeParser"/>, and acting as a factory for the <see cref="IShape"/>s.
    /// <para>
    /// If you want a typical geodetic context, just reference <see cref="Geo"/>.  Otherwise,
    /// You should either create and configure a <see cref="SpatialContextFactory"/> and then call
    /// <see cref="SpatialContextFactory.CreateSpatialContext()"/>, OR, call
    /// <see cref="SpatialContextFactory.MakeSpatialContext(IDictionary{string, string}, System.Reflection.Assembly?)"/>
    /// to do this via configuration data.
    /// </para>
    /// Thread-safe &amp; immutable.
    /// </summary>
    public class SpatialContext
    {
        [Obsolete("Use Geo static property instead. This field will be removed in 0.5.0."), System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never), CLSCompliant(false)]
        public static readonly SpatialContext GEO = new SpatialContext(new SpatialContextFactory());

        /// <summary>
        /// A popular default SpatialContext implementation for geospatial.
        /// </summary>
        public static SpatialContext Geo
#pragma warning disable CS0618 // Type or member is obsolete
            => GEO;
#pragma warning restore CS0618 // Type or member is obsolete

        //These are non-null
        private readonly bool geo;
        private readonly IDistanceCalculator calculator;
        private readonly IRectangle worldBounds;

        private readonly WktShapeParser wktShapeParser;
        private readonly BinaryCodec binaryCodec;

        private readonly bool normWrapLongitude;

        /// <summary>
        /// Consider using <see cref="SpatialContextFactory"/> instead.
        /// </summary>
        /// <param name="geo">Establishes geo vs cartesian / Euclidean.</param>
        /// <param name="calculator">Optional; defaults to Haversine or cartesian depending on units.</param>
        /// <param name="worldBounds">Optional; defaults to GEO_WORLDBOUNDS or MAX_WORLDBOUNDS depending on units.</param> 
        [Obsolete, System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
        public SpatialContext(bool geo, IDistanceCalculator calculator, IRectangle worldBounds)
            : this(InitFromLegacyConstructor(geo, calculator, worldBounds))
        { }

        private static SpatialContextFactory InitFromLegacyConstructor(bool geo,
                                                                 IDistanceCalculator? calculator,
                                                                 IRectangle? worldBounds)
        {
            SpatialContextFactory factory = new SpatialContextFactory();
            factory.IsGeo = geo;
            factory.DistanceCalculator = calculator;
            factory.WorldBounds = worldBounds;
            return factory;
        }

        [Obsolete, System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
        public SpatialContext(bool geo)
            : this(InitFromLegacyConstructor(geo, null, null))
        { }

        /// <summary>
        /// Called by <see cref="SpatialContextFactory.CreateSpatialContext()"/>.
        /// </summary>
        /// <param name="factory"></param>
        public SpatialContext(SpatialContextFactory factory)
        {
            if (factory is null)
                throw new ArgumentNullException(nameof(factory)); // spatial4n specific - use ArgumentNullException instead of NullReferenceException

            this.geo = factory.IsGeo;

            if (factory.DistanceCalculator is null)
            {
                this.calculator = IsGeo
                        ? (IDistanceCalculator)new GeodesicSphereDistanceCalculator.Haversine()
                        : new CartesianDistanceCalculator();
            }
            else
            {
                this.calculator = factory.DistanceCalculator;
            }

            //TODO remove worldBounds from Spatial4j: see Issue #55
            IRectangle? bounds = factory.WorldBounds;
            if (bounds is null)
            {
                this.worldBounds = IsGeo
                        ? new Rectangle(-180, 180, -90, 90, this)
                        : new Rectangle(-double.MaxValue, double.MaxValue,
                        -double.MaxValue, double.MaxValue, this);
            }
            else
            {
                if (IsGeo && !bounds.Equals(new Rectangle(-180, 180, -90, 90, this)))
                    throw new ArgumentException("for geo (lat/lon), bounds must be " + Geo.WorldBounds);
                if (bounds.MinX > bounds.MaxX)
                    throw new ArgumentException("worldBounds minX should be <= maxX: " + bounds);
                if (bounds.MinY > bounds.MaxY)
                    throw new ArgumentException("worldBounds minY should be <= maxY: " + bounds);
                //hopefully worldBounds' rect implementation is compatible
                this.worldBounds = new Rectangle(bounds, this);
            }

            this.normWrapLongitude = factory.NormWrapLongitude && this.IsGeo;
            this.wktShapeParser = factory.MakeWktShapeParser(this);
            this.binaryCodec = factory.MakeBinaryCodec(this);
        }

        [Obsolete("Use DistanceCalculator property instead. This field will be removed in 0.5.0."), System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never), CLSCompliant(false)]
        public virtual IDistanceCalculator DistCalc => calculator;

        public virtual IDistanceCalculator DistanceCalculator => calculator;

        /// <summary>
        /// Convenience that uses <see cref="DistanceCalculator"/>
        /// </summary>
        /// <exception cref="ArgumentNullException"><paramref name="p"/> is <c>null</c>.</exception>
        public virtual double CalcDistance(IPoint p, double x2, double y2)
        {
            return DistanceCalculator.Distance(p, x2, y2);
        }

        /// <summary>
        /// Convenience that uses <see cref="DistanceCalculator"/>
        /// </summary>
        /// <exception cref="ArgumentNullException"><paramref name="p"/> or <paramref name="p2"/> is <c>null</c>.</exception>
        public virtual double CalcDistance(IPoint p, IPoint p2)
        {
            return DistanceCalculator.Distance(p, p2);
        }

        /// <summary>
        /// The extent of x &amp; y coordinates should fit within the return'ed rectangle.
        /// Do *NOT* invoke <see cref="IRectangle.Reset(double, double, double, double)"/> on this return type.
        /// </summary>
        /// <returns></returns>
        public virtual IRectangle WorldBounds => worldBounds;

        /// <summary>
        /// If true then <see cref="NormX(double)"/> will wrap longitudes outside of the standard
        /// geodetic boundary into it. Example: 181 will become -179.
        /// </summary>
        public virtual bool IsNormWrapLongitude => normWrapLongitude;

        /// <summary>
        /// Is the mathematical world model based on a sphere, or is it a flat plane? The word
        /// "geodetic" or "geodesic" is sometimes used to refer to the former, and the latter is sometimes
        /// referred to as "Euclidean" or "cartesian".
        /// </summary>
        /// <returns></returns>
        public virtual bool IsGeo => geo;

        /// <summary>
        /// Normalize the 'x' dimension. Might reduce precision or wrap it to be within the bounds. This
        /// is called by <see cref="IO.WktShapeParser"/> before creating a shape.
        /// </summary>
        public virtual double NormX(double x)
        {
            if (normWrapLongitude)
                x = DistanceUtils.NormLonDEG(x);
            return x;
        }

        /// <summary>
        /// Normalize the 'y' dimension. Might reduce precision or wrap it to be within the bounds. This
        /// is called by <see cref="IO.WktShapeParser"/> before creating a shape.
        /// </summary>
        public virtual double NormY(double y) { return y; }

        /// <summary>
        /// Ensure fits in <see cref="WorldBounds"/>. It's called by any shape factory method that
        /// gets an 'x' dimension.
        /// </summary>
        /// <param name="x"></param>
        public virtual void VerifyX(double x)
        {
            IRectangle bounds = WorldBounds;
            if (x < bounds.MinX || x > bounds.MaxX) //NaN will pass
                throw new InvalidShapeException("Bad X value " + x + " is not in boundary " + bounds);
        }

        /// <summary>
        /// Ensure fits in <see cref="WorldBounds"/>. It's called by any shape factory method that
        /// gets a 'y' dimension.
        /// </summary>
        /// <param name="y"></param>
        public virtual void VerifyY(double y)
        {
            IRectangle bounds = WorldBounds;
            if (y < bounds.MinY || y > bounds.MaxY) //NaN will pass
                throw new InvalidShapeException("Bad Y value " + y + " is not in boundary " + bounds);
        }

        /// <summary>
        /// Construct a point.
        /// </summary>
        /// <param name="x"></param>
        /// <param name="y"></param>
        /// <returns></returns>
        public virtual IPoint MakePoint(double x, double y)
        {
            VerifyX(x);
            VerifyY(y);
            return new Point(x, y, this);
        }

        /// <summary>
        /// Construct a rectangle.
        /// </summary>
        /// <param name="lowerLeft"></param>
        /// <param name="upperRight"></param>
        /// <returns></returns>
        public virtual IRectangle MakeRectangle(IPoint lowerLeft, IPoint upperRight)
        {
            return MakeRectangle(lowerLeft.X, upperRight.X,
                            lowerLeft.Y, upperRight.Y);
        }

        /// <summary>
        /// Construct a rectangle. If just one longitude is on the dateline (+/- 180)
        /// then potentially adjust its sign to ensure the rectangle does not cross the
        /// dateline.
        /// </summary>
        /// <param name="minX"></param>
        /// <param name="maxX"></param>
        /// <param name="minY"></param>
        /// <param name="maxY"></param>
        /// <returns></returns>
        public virtual IRectangle MakeRectangle(double minX, double maxX, double minY, double maxY)
        {
            IRectangle bounds = WorldBounds;
            // Y
            if (minY < bounds.MinY || maxY > bounds.MaxY) //NaN will fail
                throw new InvalidShapeException("Y values [" + minY + " to " + maxY + "] not in boundary " + bounds);
            if (minY > maxY)
                throw new InvalidShapeException("maxY must be >= minY: " + minY + " to " + maxY);
            // X
            if (IsGeo)
            {
                VerifyX(minX);
                VerifyX(maxX);
                //TODO consider removing this logic so that there is no normalization here
                //if (minX != maxX) {   USUALLY TRUE, inline check below
                //If an edge coincides with the dateline then don't make this rect cross it
                if (minX == 180 && minX != maxX)
                {
                    minX = -180;
                }
                else if (maxX == -180 && minX != maxX)
                {
                    maxX = 180;
                }
                //}
            }
            else
            {
                if (minX < bounds.MinX || maxX > bounds.MaxX) //NaN will fail
                    throw new InvalidShapeException("X values [" + minX + " to " + maxX + "] not in boundary " + bounds);
                if (minX > maxX)
                    throw new InvalidShapeException("maxX must be >= minX: " + minX + " to " + maxX);
            }
            return new Rectangle(minX, maxX, minY, maxY, this);
        }

        /// <summary>
        /// Construct a circle. The units of "distance" should be the same as x &amp; y.
        /// </summary>
        /// <param name="x"></param>
        /// <param name="y"></param>
        /// <param name="distance"></param>
        /// <returns></returns>
        public virtual ICircle MakeCircle(double x, double y, double distance)
        {
            return MakeCircle(MakePoint(x, y), distance);
        }

        /// <summary>
        /// Construct a circle. The units of "distance" should be the same as x &amp; y.
        /// </summary>
        /// <param name="point"></param>
        /// <param name="distance"></param>
        /// <returns></returns>
        public virtual ICircle MakeCircle(IPoint point, double distance)
        {
            if (distance < 0)
                throw new InvalidShapeException("distance must be >= 0; got " + distance);
            if (IsGeo)
            {
                if (distance > 180)
                {
                    // (it's debatable whether to error or not)
                    //throw new InvalidShapeException("distance must be <= 180; got " + distance);
                    distance = 180;
                }
                return new GeoCircle(point, distance, this);
            }
            else
            {
                return new Circle(point, distance, this);
            }
        }

        /// <summary>
        /// Constructs a line string. It's an ordered sequence of connected vertexes. There
        /// is no official shape/interface for it yet so we just return <see cref="IShape"/>.
        /// </summary>
        /// <param name="points"></param>
        /// <returns></returns>
        /// <exception cref="ArgumentNullException"><paramref name="points"/> is <c>null</c>.</exception>
        public virtual IShape MakeLineString(IList<IPoint> points)
        {
            return new BufferedLineString(points, 0, false, this);
        }

        /// <summary>
        /// Constructs a buffered line string. It's an ordered sequence of connected vertexes,
        /// with a buffer distance along the line in all directions. There
        /// is no official shape/interface for it so we just return <see cref="IShape"/>.
        /// </summary>
        /// <param name="points"></param>
        /// <param name="buf"></param>
        /// <returns></returns>
        /// <exception cref="ArgumentNullException"><paramref name="points"/> is <c>null</c>.</exception>
        public virtual IShape MakeBufferedLineString(IList<IPoint> points, double buf)
        {
            return new BufferedLineString(points, buf, IsGeo, this);
        }

        /// <summary>
        /// Construct a <see cref="ShapeCollection"/>, analogous to an OGC GeometryCollection.
        /// </summary>
        /// <param name="coll"></param>
        /// <returns></returns>
        /// <exception cref="ArgumentNullException"><paramref name="coll"/> is <c>null</c>.</exception>
        public virtual ShapeCollection MakeCollection(IList<IShape> coll) //where S : Shape
        {
            return new ShapeCollection(coll, this);
        }

        /// <summary>
        /// The <see cref="IO.WktShapeParser"/> used by <see cref="ReadShapeFromWkt(string)"/>.
        /// </summary>
        /// <returns></returns>
        public virtual WktShapeParser WktShapeParser => wktShapeParser;

        /// <summary>
        /// Reads a shape from the string formatted in WKT.
        /// See <see cref="IO.WktShapeParser"/>.
        /// </summary>
        /// <param name="wkt">non-null WKT.</param>
        /// <returns>non-null</returns>
        /// <exception cref="ParseException">if it failed to parse.</exception>
        /// <exception cref="ArgumentNullException"><paramref name="wkt"/> is <c>null</c>.</exception>
        public virtual IShape ReadShapeFromWkt(string wkt)
        {
            return wktShapeParser.Parse(wkt);
        }

        public virtual BinaryCodec BinaryCodec => binaryCodec;

        /// <summary>
        /// Reads the shape from a String using the old/deprecated
        /// <see cref="LegacyShapeReadWriterFormat"/>.
        /// Instead you should use standard WKT via <see cref="ReadShapeFromWkt(string)"/>. This method falls
        /// back on WKT if it's not in the legacy format.
        /// </summary>
        /// <param name="value">non-null</param>
        /// <returns>non-null</returns>
        /// <exception cref="InvalidShapeException"><paramref name="value"/> is <c>null</c> or empty, or is not a valid shape.</exception>
        [Obsolete, System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
        public virtual IShape ReadShape(string value)
        {
            IShape? s = LegacyShapeReadWriterFormat.ReadShapeOrNull(value, this);
            if (s is null)
            {
                try
                {
                    s = ReadShapeFromWkt(value);
                }
                catch (ParseException e)
                {
                    if (e.InnerException is InvalidShapeException)
                        throw (InvalidShapeException)e.InnerException;
                    throw new InvalidShapeException(e.ToString(), e);
                }
            }
            return s;
        }

        /// <summary>
        /// Writes the shape to a String using the old/deprecated
        /// <see cref="LegacyShapeReadWriterFormat"/>. The NTS based subclass will write it
        /// to WKT if the legacy format doesn't support that shape.
        /// <para>
        /// Spatial4n in the near future won't support writing shapes to strings.
        /// </para>
        /// </summary>
        /// <param name="shape">non-null</param>
        /// <returns>non-null</returns>
        /// <exception cref="ArgumentNullException"><paramref name="shape"/> is <c>null</c>.</exception>
        [Obsolete, System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
        public virtual string ToString(IShape shape)
        {
            return LegacyShapeReadWriterFormat.WriteShape(shape);
        }

        public override string ToString()
        {
            if (this.Equals(Geo))
                return $"{Geo.GetType().Name}.{nameof(Geo)}";

            return GetType().Name + "{" +
                   "geo=" + geo +
                   ", calculator=" + calculator +
                   ", worldBounds=" + worldBounds +
                   '}';
        }
    }
}
