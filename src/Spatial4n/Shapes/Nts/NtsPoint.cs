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
using Spatial4n.Context;
using System;
using System.Diagnostics;

namespace Spatial4n.Shapes.Nts
{
    /// <summary>
    /// Wraps a <see cref="GeoAPI.Geometries.IPoint"/>.
    /// </summary>
    public class NtsPoint : IPoint
    {
        private readonly SpatialContext ctx;
        private readonly GeoAPI.Geometries.IPoint pointGeom;
        private readonly bool empty;//cached

        /// <summary>
        /// A simple constructor without normalization / validation.
        /// </summary>
        /// <exception cref="ArgumentNullException"><paramref name="pointGeom"/> or <paramref name="ctx"/> is <c>null</c>.</exception>
        public NtsPoint(GeoAPI.Geometries.IPoint pointGeom, SpatialContext ctx)
        {
            this.ctx = ctx ?? throw new ArgumentNullException(nameof(ctx));
            this.pointGeom = pointGeom ?? throw new ArgumentNullException(nameof(pointGeom));
            this.empty = pointGeom.IsEmpty;
        }

        public virtual GeoAPI.Geometries.IPoint Geometry => pointGeom;

        public virtual bool IsEmpty => empty;

        public virtual IPoint Center => this;

        public virtual bool HasArea => false;

        public virtual double GetArea(SpatialContext? ctx)
        {
            return 0;
        }

        public virtual IRectangle BoundingBox => ctx.MakeRectangle(this, this);

        /// <exception cref="ArgumentNullException"><paramref name="ctx"/> is <c>null</c>.</exception>
        public virtual IShape GetBuffered(double distance, SpatialContext ctx)
        {
            if (ctx is null)
                throw new ArgumentNullException(nameof(ctx)); // spatial4n specific - use ArgumentNullException instead of NullReferenceException

            return ctx.MakeCircle(this, distance);
        }

        /// <exception cref="ArgumentNullException"><paramref name="other"/> is <c>null</c>.</exception>
        public virtual SpatialRelation Relate(IShape other)
        {
            if (other is null)
                throw new ArgumentNullException(nameof(other)); // spatial4n specific - use ArgumentNullException instead of NullReferenceException

            // ** NOTE ** the overall order of logic is kept consistent here with simple.PointImpl.
            if (IsEmpty || other.IsEmpty)
                return SpatialRelation.Disjoint;
            if (other is IPoint)
                return this.Equals(other) ? SpatialRelation.Intersects : SpatialRelation.Disjoint;
            return other.Relate(this).Transpose();
        }

        public virtual double X => IsEmpty ? double.NaN : pointGeom.X;

        public virtual double Y => IsEmpty ? double.NaN : pointGeom.Y;

        public virtual void Reset(double x, double y)
        {
            Debug.Assert(!IsEmpty);
            var cSeq = pointGeom.CoordinateSequence;
            cSeq.SetOrdinate(0, Ordinate.X, x);
            cSeq.SetOrdinate(0, Ordinate.Y, y);
        }


        public override string ToString()
        {
            return string.Format("Pt(x={0:0.0#############},y={1:0.0#############})", X, Y);
        }

        public override bool Equals(object? o)
        {
            return Point.Equals(this, o);
        }

        public override int GetHashCode()
        {
            return Point.GetHashCode(this);
        }
    }
}