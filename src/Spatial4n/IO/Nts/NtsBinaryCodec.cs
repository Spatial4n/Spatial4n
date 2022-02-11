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
using NetTopologySuite.IO;
using Spatial4n.Context.Nts;
using Spatial4n.Exceptions;
using Spatial4n.Shapes;
using System;
using System.IO;

namespace Spatial4n.IO.Nts
{
    /// <summary>
    /// Writes shapes in WKB, if it isn't otherwise supported by the superclass.
    /// </summary>
    public class NtsBinaryCodec : BinaryCodec
    {
        protected readonly bool useFloat;//instead of double

        /// <summary>
        /// Initializes a new instance of <see cref="NtsBinaryCodec"/>.
        /// </summary>
        /// <param name="ctx">The spatial context.</param>
        /// <param name="factory">The context factory.</param>
        /// <exception cref="ArgumentNullException"><paramref name="ctx"/> or <paramref name="factory"/> is <c>null</c>.</exception>
        public NtsBinaryCodec(NtsSpatialContext ctx, NtsSpatialContextFactory factory)
            : base(ctx, factory)
        {
            if (factory is null)
                throw new ArgumentNullException(nameof(factory)); // spatial4n specific - use ArgumentNullException instead of NullReferenceException

            //note: ctx.geometryFactory hasn't been set yet
            useFloat = (factory.PrecisionModel.PrecisionModelType == PrecisionModels.FloatingSingle);
        }

        /// <exception cref="ArgumentNullException"><paramref name="dataInput"/> is <c>null</c>.</exception>
        protected override double ReadDim(BinaryReader dataInput)
        {
            // Spatial4n: Added guard clause
            if (dataInput is null)
                throw new ArgumentNullException(nameof(dataInput));

            if (useFloat)
                return dataInput.ReadSingle();
            return base.ReadDim(dataInput);
        }

        /// <exception cref="ArgumentNullException"><paramref name="dataOutput"/> is <c>null</c>.</exception>
        protected override void WriteDim(BinaryWriter dataOutput, double v)
        {
            // Spatial4n: Added guard clause
            if (dataOutput is null)
                throw new ArgumentNullException(nameof(dataOutput));

            if (useFloat)
                dataOutput.Write((float)v);
            else
                base.WriteDim(dataOutput, v);
        }

        protected override ShapeType TypeForShape(IShape? s)
        {
            ShapeType type = base.TypeForShape(s);
            if (type == 0)
            {
                type = ShapeType.Geometry;//handles everything
            }
            return type;
        }

        /// <exception cref="ArgumentNullException"><paramref name="dataInput"/> is <c>null</c>.</exception>
        protected override IShape? ReadShapeByTypeIfSupported(BinaryReader dataInput, ShapeType type)
        {
            // Spatial4n: Added guard clause
            if (dataInput is null)
                throw new ArgumentNullException(nameof(dataInput));

            if (type != ShapeType.Geometry)
                return base.ReadShapeByTypeIfSupported(dataInput, type);
            return ReadNtsGeom(dataInput);
        }

        /// <exception cref="ArgumentNullException"><paramref name="dataOutput"/> or <paramref name="s"/> is <c>null</c>.</exception>
        protected override bool WriteShapeByTypeIfSupported(BinaryWriter dataOutput, IShape s, ShapeType type)
        {
            // Spatial4n: Added guard clauses
            if (dataOutput is null)
                throw new ArgumentNullException(nameof(dataOutput));
            if (s is null)
                throw new ArgumentNullException(nameof(s));

            if (type != ShapeType.Geometry)
                return base.WriteShapeByTypeIfSupported(dataOutput, s, type);
            WriteNtsGeom(dataOutput, s);
            return true;
        }


        /// <summary>
        /// Spatial4n specific class. The primary purpose of this class is
        /// to ensure the inner stream does not get disposed prematurely.
        /// </summary>
        private class InputStreamAnonymousHelper : Stream
        {
            private readonly BinaryReader dataInput;

            public InputStreamAnonymousHelper(BinaryReader dataInput)
            {
                this.dataInput = dataInput;
            }

            public override int Read(byte[] buffer, int offset, int count)
            {
                // Spatial4n note: We don't need the BOM handling in .NET
                return dataInput.Read(buffer, offset, count);
            }

            public override bool CanRead => dataInput.BaseStream.CanRead;

            public override bool CanSeek => dataInput.BaseStream.CanSeek;

            public override bool CanWrite => dataInput.BaseStream.CanWrite;

            public override long Length => dataInput.BaseStream.Length;

            public override long Position
            {
                get => dataInput.BaseStream.Position;
                set => dataInput.BaseStream.Position = value;
            }

            public override void Flush()
            {
                dataInput.BaseStream.Flush();
            }

            public override long Seek(long offset, SeekOrigin origin)
            {
                return dataInput.BaseStream.Seek(offset, origin);
            }

            public override void SetLength(long value)
            {
                dataInput.BaseStream.SetLength(value);
            }

            public override void Write(byte[] buffer, int offset, int count)
            {
                dataInput.BaseStream.Write(buffer, offset, count);
            }
        }

        /// <exception cref="ArgumentNullException"><paramref name="dataInput"/> is <c>null</c>.</exception>
        public virtual IShape ReadNtsGeom(BinaryReader dataInput)
        {
            // Spatial4n: Added guard clause
            if (dataInput is null)
                throw new ArgumentNullException(nameof(dataInput));

            NtsSpatialContext ctx = (NtsSpatialContext)base.ctx;
#pragma warning disable 612, 618
            WKBReader reader = new WKBReader(ctx.GeometryFactory);
#pragma warning restore 612, 618
            try
            {
                Stream inStream = new InputStreamAnonymousHelper(dataInput);
                IGeometry geom = reader.Read(inStream);
                //false: don't check for dateline-180 cross or multi-polygon overlaps; this won't happen
                // once it gets written, and we're reading it now
                return ctx.MakeShape(geom, false, false);

            }
            catch (GeoAPI.IO.ParseException ex)
            {
                throw new InvalidShapeException("error reading WKT", ex);
            }
        }

        /// <summary>
        /// Spatial4n specific class. The primary purpose of this class is
        /// to ensure the inner stream does not get disposed prematurely.
        /// </summary>
        private class OutputStreamAnonymousHelper : Stream
        {
            private readonly BinaryWriter dataOutput;
            public OutputStreamAnonymousHelper(BinaryWriter dataOutput)
            {
                this.dataOutput = dataOutput;
            }

            public override void Write(byte[] buffer, int offset, int count)
            {
                // Spatial4n note: We don't need the BOM handling in .NET
                dataOutput.BaseStream.Write(buffer, offset, count);
            }

            public override bool CanRead => dataOutput.BaseStream.CanRead;

            public override bool CanSeek => dataOutput.BaseStream.CanSeek;

            public override bool CanWrite => dataOutput.BaseStream.CanWrite;

            public override long Length => dataOutput.BaseStream.Length;

            public override long Position
            {
                get => dataOutput.BaseStream.Position;
                set => dataOutput.BaseStream.Position = value;
            }

            public override void Flush()
            {
                dataOutput.BaseStream.Flush();
            }

            public override long Seek(long offset, SeekOrigin origin)
            {
                return dataOutput.BaseStream.Seek(offset, origin);
            }

            public override void SetLength(long value)
            {
                dataOutput.BaseStream.SetLength(value);
            }

            public override int Read(byte[] buffer, int offset, int count)
            {
                return dataOutput.BaseStream.Read(buffer, offset, count);
            }
        }

        /// <exception cref="ArgumentNullException"><paramref name="dataOutput"/> or <paramref name="s"/> is <c>null</c>.</exception>
        public virtual void WriteNtsGeom(BinaryWriter dataOutput, IShape s)
        {
            // Spatial4n: Added guard clauses
            if (dataOutput is null)
                throw new ArgumentNullException(nameof(dataOutput));
            if (s is null)
                throw new ArgumentNullException(nameof(s));

            NtsSpatialContext ctx = (NtsSpatialContext)base.ctx;
            IGeometry geom = ctx.GetGeometryFrom(s);//might even translate it
            new WKBWriter().Write(geom, new OutputStreamAnonymousHelper(dataOutput));
        }
    }
}