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
using Spatial4n.Exceptions;
using Spatial4n.Shapes;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;

namespace Spatial4n.IO
{
    /// <summary>
    /// A binary shape format. It is <c>not</c> designed to be a published standard, unlike Well Known
    /// Binary (WKB). The initial release is simple but it could get more optimized to use fewer bytes or
    /// to write &amp; read pre-computed index structures.
    /// <para>
    /// Immutable and thread-safe.
    /// </para>
    /// </summary>
    public class BinaryCodec
    {
        //type 0; reserved for unkonwn/generic; see readCollection
        [SuppressMessage("Design", "CA1027:Mark enums with FlagsAttribute", Justification = "Not a flags enum")]
        protected enum ShapeType : byte
        {
            Point = 1,
            [Obsolete("Use Point instead. This const will be removed in 0.5.0."), System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
            TYPE_POINT = 1,
            Rectangle = 2,
            [Obsolete("Use Rectangle instead. This const will be removed in 0.5.0."), System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
            TYPE_RECT = 2,
            Circle = 3,
            [Obsolete("Use Circle instead. This const will be removed in 0.5.0."), System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
            TYPE_CIRCLE = 3,
            Collection = 4,
            [Obsolete("Use Collection instead. This const will be removed in 0.5.0."), System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
            TYPE_COLL = 4,
            Geometry = 5,
            [Obsolete("Use Geometry instead. This const will be removed in 0.5.0."), System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
            TYPE_GEOM = 5
        }


        //TODO support BufferedLineString

        protected readonly SpatialContext ctx;

        /// <summary>
        /// Initializes a new instance of <see cref="BinaryCodec"/>.
        /// </summary>
        /// <param name="ctx">The spatial context.</param>
        /// <param name="factory">The spatial context factory.</param>
        /// <exception cref="ArgumentNullException"><paramref name="ctx"/> is <c>null</c>.</exception>
        [SuppressMessage("Style", "IDE0060:Remove unused parameter", Justification = "This constructor is mandated by SpatialContextFactory")]
        public BinaryCodec(SpatialContext ctx, SpatialContextFactory? factory)
        {
            if (ctx is null)
                throw new ArgumentNullException(nameof(ctx)); // spatial4n specific - added guard clause

            this.ctx = ctx;
        }

        /// <exception cref="ArgumentNullException"><paramref name="dataInput"/> is <c>null</c>.</exception>
        public virtual IShape ReadShape(BinaryReader dataInput)
        {
            if (dataInput is null)
                throw new ArgumentNullException(nameof(dataInput)); // spatial4n specific - use ArgumentNullException instead of NullReferenceException

            byte type = dataInput.ReadByte();
            IShape? s = ReadShapeByTypeIfSupported(dataInput, (ShapeType)type);
            if (s is null)
                throw new ArgumentException("Unsupported shape byte " + type);
            return s;
        }

        /// <exception cref="ArgumentNullException"><paramref name="dataOutput"/> or <paramref name="s"/> is <c>null</c>.</exception>
        public virtual void WriteShape(BinaryWriter dataOutput, IShape s)
        {
            if (dataOutput is null)
                throw new ArgumentNullException(nameof(dataOutput)); // spatial4n specific - use ArgumentNullException instead of NullReferenceException
            if (s is null)
                throw new ArgumentNullException(nameof(s)); // spatial4n specific - use ArgumentNullException instead of NullReferenceException

            bool written = WriteShapeByTypeIfSupported(dataOutput, s);
            if (!written)
                throw new ArgumentException("Unsupported shape " + s.GetType());
        }

        /// <exception cref="ArgumentNullException"><paramref name="dataInput"/> is <c>null</c>.</exception>
        protected virtual IShape? ReadShapeByTypeIfSupported(BinaryReader dataInput, ShapeType type)
        {
            if (dataInput is null)
                throw new ArgumentNullException(nameof(dataInput)); // spatial4n specific - use ArgumentNullException instead of NullReferenceException

            switch (type)
            {
                case ShapeType.Point: return ReadPoint(dataInput);
                case ShapeType.Rectangle: return ReadRect(dataInput);
                case ShapeType.Circle: return ReadCircle(dataInput);
                case ShapeType.Collection: return ReadCollection(dataInput);
                default: return null;
            }
        }

        /// <summary>
        /// Note: writes the type byte even if not supported
        /// </summary>
        /// <exception cref="ArgumentNullException"><paramref name="dataOutput"/> or <paramref name="s"/> is <c>null</c>.</exception>
        protected virtual bool WriteShapeByTypeIfSupported(BinaryWriter dataOutput, IShape s)
        {
            if (dataOutput is null)
                throw new ArgumentNullException(nameof(dataOutput)); // spatial4n specific - use ArgumentNullException instead of NullReferenceException
            if (s is null)
                throw new ArgumentNullException(nameof(s)); // spatial4n specific - use ArgumentNullException instead of NullReferenceException

            ShapeType type = TypeForShape(s);
            dataOutput.Write((byte)type);
            return WriteShapeByTypeIfSupported(dataOutput, s, type);
            //dataOutput.position(dataOutput.position() - 1);//reset putting type
        }

        /// <exception cref="ArgumentNullException"><paramref name="dataOutput"/> or <paramref name="s"/> is <c>null</c>.</exception>
        protected virtual bool WriteShapeByTypeIfSupported(BinaryWriter dataOutput, IShape s, ShapeType type)
        {
            if (dataOutput is null)
                throw new ArgumentNullException(nameof(dataOutput)); // spatial4n specific - use ArgumentNullException instead of NullReferenceException
            if (s is null)
                throw new ArgumentNullException(nameof(s)); // spatial4n specific - use ArgumentNullException instead of NullReferenceException

            switch (type)
            {
                case ShapeType.Point: WritePoint(dataOutput, (IPoint)s); break;
                case ShapeType.Rectangle: WriteRect(dataOutput, (IRectangle)s); break;
                case ShapeType.Circle: WriteCircle(dataOutput, (ICircle)s); break;
                case ShapeType.Collection: WriteCollection(dataOutput, (ShapeCollection)s); break;
                default:
                    return false;
            }
            return true;
        }

        protected virtual ShapeType TypeForShape(IShape s)
        {
            if (s is IPoint)
            {
                return ShapeType.Point;
            }
            else if (s is IRectangle)
            {
                return ShapeType.Rectangle;
            }
            else if (s is ICircle)
            {
                return ShapeType.Circle;
            }
            else if (s is ShapeCollection)
            {
                return ShapeType.Collection;
            }
            else
            {
                return 0;
            }
        }

        /// <exception cref="ArgumentNullException"><paramref name="dataInput"/> is <c>null</c>.</exception>
        protected virtual double ReadDim(BinaryReader dataInput)
        {
            if (dataInput is null)
                throw new ArgumentNullException(nameof(dataInput)); // spatial4n specific - use ArgumentNullException instead of NullReferenceException

            return dataInput.ReadDouble();
        }

        /// <exception cref="ArgumentNullException"><paramref name="dataOutput"/> is <c>null</c>.</exception>
        protected virtual void WriteDim(BinaryWriter dataOutput, double v)
        {
            if (dataOutput is null)
                throw new ArgumentNullException(nameof(dataOutput)); // spatial4n specific - use ArgumentNullException instead of NullReferenceException

            dataOutput.Write(v);
        }

        /// <exception cref="ArgumentNullException"><paramref name="dataInput"/> is <c>null</c>.</exception>
        public virtual IPoint ReadPoint(BinaryReader dataInput)
        {
            if (dataInput is null)
                throw new ArgumentNullException(nameof(dataInput)); // spatial4n specific - use ArgumentNullException instead of NullReferenceException

            return ctx.MakePoint(ReadDim(dataInput), ReadDim(dataInput));
        }

        /// <exception cref="ArgumentNullException"><paramref name="dataOutput"/> or <paramref name="pt"/> is <c>null</c>.</exception>
        public virtual void WritePoint(BinaryWriter dataOutput, IPoint pt)
        {
            if (dataOutput is null)
                throw new ArgumentNullException(nameof(dataOutput)); // spatial4n specific - use ArgumentNullException instead of NullReferenceException

            WriteDim(dataOutput, pt.X);
            WriteDim(dataOutput, pt.Y);
        }

        /// <exception cref="ArgumentNullException"><paramref name="dataInput"/> is <c>null</c>.</exception>
        public virtual IRectangle ReadRect(BinaryReader dataInput)
        {
            if (dataInput is null)
                throw new ArgumentNullException(nameof(dataInput)); // spatial4n specific - use ArgumentNullException instead of NullReferenceException

            return ctx.MakeRectangle(ReadDim(dataInput), ReadDim(dataInput), ReadDim(dataInput), ReadDim(dataInput));
        }

        /// <exception cref="ArgumentNullException"><paramref name="dataOutput"/> or <paramref name="r"/> is <c>null</c>.</exception>
        public virtual void WriteRect(BinaryWriter dataOutput, IRectangle r)
        {
            if (dataOutput is null)
                throw new ArgumentNullException(nameof(dataOutput)); // spatial4n specific - use ArgumentNullException instead of NullReferenceException

            WriteDim(dataOutput, r.MinX);
            WriteDim(dataOutput, r.MaxX);
            WriteDim(dataOutput, r.MinY);
            WriteDim(dataOutput, r.MaxY);
        }

        /// <exception cref="ArgumentNullException"><paramref name="dataInput"/> is <c>null</c>.</exception>
        public virtual ICircle ReadCircle(BinaryReader dataInput)
        {
            if (dataInput is null)
                throw new ArgumentNullException(nameof(dataInput)); // spatial4n specific - use ArgumentNullException instead of NullReferenceException

            return ctx.MakeCircle(ReadPoint(dataInput), ReadDim(dataInput));
        }

        /// <exception cref="ArgumentNullException"><paramref name="dataOutput"/> or <paramref name="c"/> is <c>null</c>.</exception>
        public virtual void WriteCircle(BinaryWriter dataOutput, ICircle c)
        {
            if (dataOutput is null)
                throw new ArgumentNullException(nameof(dataOutput)); // spatial4n specific - use ArgumentNullException instead of NullReferenceException
            if (c is null)
                throw new ArgumentNullException(nameof(c)); // spatial4n specific - use ArgumentNullException instead of NullReferenceException

            WritePoint(dataOutput, c.Center);
            WriteDim(dataOutput, c.Radius);
        }

        /// <exception cref="ArgumentNullException"><paramref name="dataInput"/> is <c>null</c>.</exception>
        public virtual ShapeCollection ReadCollection(BinaryReader dataInput)
        {
            if (dataInput is null)
                throw new ArgumentNullException(nameof(dataInput)); // spatial4n specific - use ArgumentNullException instead of NullReferenceException

            byte type = dataInput.ReadByte();
            int size = dataInput.ReadInt32();
            IList<IShape> shapes = new List<IShape>(size);
            for (int i = 0; i < size; i++)
            {
                if (type == 0)
                {
                    shapes.Add(ReadShape(dataInput));
                }
                else
                {
                    IShape? s = ReadShapeByTypeIfSupported(dataInput, (ShapeType)type);
                    if (s is null)
                        throw new InvalidShapeException("Unsupported shape byte " + type);
                    shapes.Add(s);
                }
            }
            return ctx.MakeCollection(shapes);
        }

        /// <exception cref="ArgumentNullException"><paramref name="dataOutput"/> or <paramref name="col"/> is <c>null</c>.</exception>
        public virtual void WriteCollection(BinaryWriter dataOutput, ShapeCollection col)
        {
            if (dataOutput is null)
                throw new ArgumentNullException(nameof(dataOutput)); // spatial4n specific - use ArgumentNullException instead of NullReferenceException
            if (col is null)
                throw new ArgumentNullException(nameof(col)); // spatial4n specific - use ArgumentNullException instead of NullReferenceException

            byte type = (byte)0;//TODO add type to ShapeCollection
            dataOutput.Write(type);
            dataOutput.Write(col.Count);
            for (int i = 0; i < col.Count; i++)
            {
                IShape s = col[i];
                if (type == 0)
                {
                    WriteShape(dataOutput, s);
                }
                else
                {
                    bool written = WriteShapeByTypeIfSupported(dataOutput, s, (ShapeType)type);
                    if (!written)
                        throw new ArgumentException("Unsupported shape type " + s.GetType());
                }
            }
        }
    }
}
