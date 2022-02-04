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

using Spatial4n.Core.Context;
using Spatial4n.Core.Util;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;

namespace Spatial4n.Core.Shapes.Impl
{
    /// <summary>
    /// A <see cref="BufferedLineString"/> is a collection of <see cref="BufferedLine"/> shapes,
    /// resulting in what some call a "Track" or "Polyline" (ESRI terminology).
    /// The buffer can be 0.  Note that <see cref="BufferedLine"/> isn't yet aware of geodesics (e.g. the dateline).
    /// </summary>
    public class BufferedLineString : IShape
    {
        //TODO add some geospatial awareness like:
        // segment that spans at the dateline (split it at DL?).

        private readonly ShapeCollection segments;
        private readonly double buf;

        /// <summary>
        /// Needs at least 1 point, usually more than that.  If just one then it's
        /// internally treated like 2 points.
        /// </summary>
        public BufferedLineString(IList<IPoint> points, double buf, SpatialContext ctx)
            : this(points, buf, false, ctx)
        {
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="points">ordered control points. If empty then this shape is empty.</param>
        /// <param name="buf">Buffer &gt;= 0</param>
        /// <param name="expandBufForLongitudeSkew">
        /// See <see cref="BufferedLine.ExpandBufForLongitudeSkew(IPoint, IPoint, double)"/>
        /// If true then the buffer for each segment is computed.
        /// </param>
        /// <param name="ctx"></param>
        /// <exception cref="ArgumentNullException"><paramref name="points"/> or <paramref name="ctx"/> is <c>null</c>.</exception>
        public BufferedLineString(IList<IPoint> points, double buf, bool expandBufForLongitudeSkew,
                                  SpatialContext ctx)
        {
            if (points is null)
                throw new ArgumentNullException(nameof(points)); // spatial4n specific - use ArgumentNullException instead of NullReferenceException
            if (ctx is null)
                throw new ArgumentNullException(nameof(ctx)); // spatial4n specific - use ArgumentNullException instead of NullReferenceException

            this.buf = buf;

            if (points.Count == 0)
            {
                this.segments = ctx.MakeCollection(CollectionUtils.EmptyList<IShape>());
            }
            else
            {
                List<IShape> segments = new List<IShape>(points.Count - 1);

                IPoint? prevPoint = null;
                foreach (IPoint point in points)
                {
                    if (prevPoint != null)
                    {
                        double segBuf = buf;
                        if (expandBufForLongitudeSkew)
                        {
                            //TODO this is faulty in that it over-buffers.  See Issue#60.
                            segBuf = BufferedLine.ExpandBufForLongitudeSkew(prevPoint, point, buf);
                        }
                        segments.Add(new BufferedLine(prevPoint, point, segBuf, ctx));
                    }
                    prevPoint = point;
                }
                if (segments.Count == 0)
                {//TODO throw exception instead?
                    segments.Add(new BufferedLine(prevPoint!, prevPoint!, buf, ctx));
                }
                this.segments = ctx.MakeCollection(segments);
            }
        }


        public virtual bool IsEmpty => segments.IsEmpty;

        /// <exception cref="ArgumentNullException"><paramref name="ctx"/> is <c>null</c>.</exception>
        public virtual IShape GetBuffered(double distance, SpatialContext ctx)
        {
            if (ctx is null)
                throw new ArgumentNullException(nameof(ctx)); // spatial4n specific - use ArgumentNullException instead of NullReferenceException

            return ctx.MakeBufferedLineString(Points, buf + distance);
        }

        public virtual ShapeCollection Segments => segments;

        public virtual double Buf => buf;

        public virtual double GetArea(SpatialContext? ctx)
        {
            return segments.GetArea(ctx);
        }

        /// <exception cref="ArgumentNullException"><paramref name="other"/> is <c>null</c>.</exception>
        public virtual SpatialRelation Relate(IShape other)
        {
            if (other is null)
                throw new ArgumentNullException(nameof(other)); // spatial4n specific - use ArgumentNullException instead of NullReferenceException

            return segments.Relate(other);
        }

        public virtual bool HasArea => segments.HasArea;


        public virtual IPoint Center => segments.Center;


        public virtual IRectangle BoundingBox => segments.BoundingBox;


        public override string ToString()
        {
            StringBuilder str = new StringBuilder(100);
            str.Append("BufferedLineString(buf=").Append(buf).Append(" pts=");
            bool first = true;
            foreach (IPoint point in Points)
            {
                if (first)
                {
                    first = false;
                }
                else
                {
                    str.Append(", ");
                }
                str.Append(point.X).Append(' ').Append(point.Y);
            }
            str.Append(')');
            return str.ToString();
        }

        public virtual IList<IPoint> Points
        {
            get
            {
                if (segments.Count == 0)
                    return CollectionUtils.EmptyList<IPoint>();
                IList<IShape> shapes = segments.Shapes;
                return new PointsAnonymousClass(shapes);
            }
        }

        private class PointsAnonymousClass : IList<IPoint>
        {
            private readonly IList<IShape> lines;

            public PointsAnonymousClass(IList<IShape> lines)
            {
                this.lines = lines ?? throw new ArgumentNullException(nameof(lines));
            }

            public IPoint this[int index]
            {
                get
                {
                    if (index == 0)
                        return ((BufferedLine)lines[0]).A;
                    return ((BufferedLine)lines[index - 1]).B;
                }
                set => throw new NotSupportedException();
            }

            public int Count => lines.Count + 1;

            public bool IsReadOnly => lines.IsReadOnly;

            public void Add(IPoint item)
            {
                throw new NotSupportedException();
            }

            public void Clear()
            {
                lines.Clear();
            }

            public bool Contains(IPoint item)
            {
                using var it = GetEnumerator();
                if (item is null)
                {
                    while (it.MoveNext())
                        if (it.Current is null) return true;
                }
                else
                {
                    while (it.MoveNext())
                        if (it.Current.Equals(item)) return true;
                }
                return false;
            }

            public void CopyTo(IPoint[] array, int index)
            {
                if (array is null)
                    throw new ArgumentNullException(nameof(array));
                if (index < 0)
                    throw new ArgumentOutOfRangeException(nameof(index), index, "Non-negative number required.");
                if (Count > array.Length - index)
                    throw new ArgumentException("Destination array is not long enough to copy all the items in the collection. Check array index and length.");

                array[index++] = ((BufferedLine)lines[0]).A;
                for (int i = 1; i < lines.Count + 1; i++)
                    array[index++] = ((BufferedLine)lines[i - 1]).B;
            }

            public IEnumerator<IPoint> GetEnumerator()
            {
                yield return ((BufferedLine)lines[0]).A;
                for (int i = 1; i < lines.Count + 1; i++)
                    yield return ((BufferedLine)lines[i - 1]).B;
            }

            public int IndexOf(IPoint item)
            {
                if (item is null)
                {
                    if (((BufferedLine)lines[0]).A is null)
                        return 0;
                    for (int i = 1; i < lines.Count + 1; i++)
                    {
                        if (((BufferedLine)lines[i - 1]).B is null)
                            return i;
                    }
                }
                else
                {
                    if (((BufferedLine)lines[0]).A.Equals(item))
                        return 0;
                    for (int i = 1; i < lines.Count + 1; i++)
                    {
                        if (((BufferedLine)lines[i - 1]).B.Equals(item))
                            return i;
                    }
                }
                return -1;
            }

            public void Insert(int index, IPoint item)
            {
                throw new NotSupportedException();
            }

            public bool Remove(IPoint item)
            {
                throw new NotSupportedException();
            }

            public void RemoveAt(int index)
            {
                throw new NotSupportedException();
            }

            IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
        }


        public override bool Equals(object o)
        {
            if (this == o) return true;
            if (o == null || GetType() != o.GetType()) return false;

            BufferedLineString that = (BufferedLineString)o;

            if (that.buf.CompareTo(buf) != 0) return false;
            if (!segments.Equals(that.segments)) return false;

            return true;
        }

        public override int GetHashCode()
        {
            int result;
            long temp;
            result = segments.GetHashCode();
            temp = buf != +0.0d ? BitConverter.DoubleToInt64Bits(buf) : 0L;
            result = 31 * result + (int)(temp ^ (long)((ulong)temp >> 32));
            return result;
        }
    }
}
