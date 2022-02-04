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
using NetTopologySuite.Geometries;
using NetTopologySuite.Geometries.Implementation;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;

#if LEGACY_NAMESPACE
using Spatial4n.Core.Exceptions;
using Spatial4n.Core.IO.Nts;

namespace Spatial4n.Core.Context.Nts
#else
using Spatial4n.Exceptions;
using Spatial4n.IO.Nts;

namespace Spatial4n.Context.Nts
#endif
{
    /// <summary>
    /// See <see cref="SpatialContextFactory.MakeSpatialContext(IDictionary{string, string}, Assembly?)"/>.
    /// <para>
    /// The following keys are looked up in the args map, in addition to those in the
    /// superclass:
    /// </para>
    /// <list type="table">
    ///     <item>
    ///         <term>DatelineRule</term>
    ///         <description>Width180(default)|CounterClockwiseRectangle|None -- see <see cref="IO.Nts.DatelineRule"/></description>
    ///     </item>
    ///     <item>
    ///         <term>ValidationRule</term>
    ///         <description>Error(default)|None|RepairConvexHull|RepairBuffer0 -- see <see cref="IO.Nts.ValidationRule"/></description>
    ///     </item>
    ///     <item>
    ///         <term>AutoIndex</term>
    ///         <description>true|false(default) -- see <see cref="NtsWktShapeParser.AutoIndex"/></description>
    ///     </item>
    ///     <item>
    ///         <term>AllowMultiOverlap</term>
    ///         <description>true|false(default) -- see <see cref="NtsSpatialContext.AllowMultiOverlap"/></description>
    ///     </item>
    ///     <item>
    ///         <term>PrecisionModel</term>
    ///         <description>
    ///         floating(default) | floating_single | fixed -- see <see cref="PrecisionModel"/>.
    ///         If <c>fixed</c> then you must also provide <c>precisionScale</c> -- see <see cref="PrecisionModel.Scale"/>
    ///         </description>
    ///     </item>
    /// </list>
    /// </summary>
#if LEGACY_NAMESPACE
    [Obsolete("Use Spatial4n.Context.Nts.NtsSpatialContextFactory instead. This class will be removed in 0.5.0."), System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
#endif
    public class NtsSpatialContextFactory : SpatialContextFactory
    {
        protected static PrecisionModel DefaultPrecisionModel { get; } = new PrecisionModel(); //floating
        [Obsolete("Use DefaultPrecisionModel property instead. This field will be removed in 0.5.0."), System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never), CLSCompliant(false)]
        protected static readonly PrecisionModel defaultPrecisionModel = DefaultPrecisionModel;

        //These 3 are NTS defaults for new GeometryFactory()
        [Obsolete("Use PrecisionModel property instead. This field will be removed in 0.5.0."), System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never), CLSCompliant(false)]
        public PrecisionModel precisionModel = DefaultPrecisionModel;
        public PrecisionModel PrecisionModel
        {
#pragma warning disable CS0618 // Type or member is obsolete
            get => precisionModel;
            set => precisionModel = value;
#pragma warning restore CS0618 // Type or member is obsolete
        }

        [Obsolete("Use SRID property instead. This field will be removed in 0.5.0."), System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never), CLSCompliant(false)]
        public int srid = 0;
        public int SRID
        {
#pragma warning disable CS0618 // Type or member is obsolete
            get => srid;
            set => srid = value;
#pragma warning restore CS0618 // Type or member is obsolete
        }

        [Obsolete("Use CoordinateSequenceFactory property instead. This field will be removed in 0.5.0."), System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never), CLSCompliant(false)]
        public ICoordinateSequenceFactory coordinateSequenceFactory = CoordinateArraySequenceFactory.Instance;
        public ICoordinateSequenceFactory CoordinateSequenceFactory
        {
#pragma warning disable CS0618 // Type or member is obsolete
            get => coordinateSequenceFactory;
            set => coordinateSequenceFactory = value;
#pragma warning restore CS0618 // Type or member is obsolete
        }
        //ignored if geo=false
        [Obsolete("Use DatelineRule property instead. This property will be removed in 0.5.0."), System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never), CLSCompliant(false)]
        public DatelineRule datelineRule = DatelineRule.Width180;
        public DatelineRule DatelineRule
        {
#pragma warning disable CS0618 // Type or member is obsolete
            get => datelineRule;
            set => datelineRule = value;
#pragma warning restore CS0618 // Type or member is obsolete
        }

        [Obsolete("Use ValidationRule property instead. This field will be removed in 0.5.0."), System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never), CLSCompliant(false)]
        public ValidationRule validationRule = ValidationRule.Error;
        public ValidationRule ValidationRule
        {
#pragma warning disable CS0618 // Type or member is obsolete
            get => validationRule;
            set => validationRule = value;
#pragma warning restore CS0618 // Type or member is obsolete
        }
        [Obsolete("Use AutoIndex property instead. This field will be removed in 0.5.0."), System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never), CLSCompliant(false)]
        public bool autoIndex = false;
        public bool AutoIndex
        {
#pragma warning disable CS0618 // Type or member is obsolete
            get => autoIndex;
            set => autoIndex = value;
#pragma warning restore CS0618 // Type or member is obsolete
        }
        [Obsolete("Use AllowMultiOverlap property instead. This field will be removed in 0.5.0."), System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never), CLSCompliant(false)]
        public bool allowMultiOverlap = false;//ignored if geo=false
        public bool AllowMultiOverlap
        {
#pragma warning disable CS0618 // Type or member is obsolete
            get => allowMultiOverlap;
            set => allowMultiOverlap = value;
#pragma warning restore CS0618 // Type or member is obsolete
        }

        //kinda advanced options:
        [Obsolete("Use UseNtsPoint property instead. This field will be removed in 0.5.0."), System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never), CLSCompliant(false)]
        public bool useNtsPoint = true;
        public bool UseNtsPoint
        {
#pragma warning disable CS0618 // Type or member is obsolete
            get => useNtsPoint;
            set => useNtsPoint = value;
#pragma warning restore CS0618 // Type or member is obsolete
        }
        [Obsolete("Use UseNtsLineString property instead. This field will be removed in 0.5.0."), System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never), CLSCompliant(false)]
        public bool useNtsLineString = true;
        public bool UseNtsLineString
        {
#pragma warning disable CS0618 // Type or member is obsolete
            get => useNtsLineString;
            set => useNtsLineString = value;
#pragma warning restore CS0618 // Type or member is obsolete
        }

        public NtsSpatialContextFactory()
        {
            base.WktShapeParserType = typeof(NtsWktShapeParser);
            base.BinaryCodecType = typeof(NtsBinaryCodec);
        }

        protected override void Init(IDictionary<string, string> args, Assembly? assembly)
        {
            base.Init(args, assembly);

            if (!InitProperty("DatelineRule"))
                InitField("datelineRule");
            if (!InitProperty("ValidationRule"))
                InitField("validationRule");
            if (!InitProperty("AutoIndex"))
                InitField("autoIndex");
            if (!InitProperty("AllowMultiOverlap"))
                InitField("allowMultiOverlap");
            if (!InitProperty("UseNtsPoint"))
                InitField("useNtsPoint");
            if (!InitProperty("UseNtsLineString"))
                InitField("useNtsLineString");

            if (!args.TryGetValue("PrecisionModel", out string modelStr))
                args.TryGetValue("precisionModel", out modelStr);

            if ((args.TryGetValue("PrecisionScale", out string scaleStr) || args.TryGetValue("precisionScale", out scaleStr)) && scaleStr != null)
            {
                if (modelStr != null && !modelStr.Equals("fixed"))
                    throw new RuntimeException("Since precisionScale was specified; precisionModel must be 'fixed' but got: " + modelStr);
#pragma warning disable CS0618 // Type or member is obsolete
                precisionModel = new PrecisionModel(double.Parse(scaleStr, CultureInfo.InvariantCulture));
#pragma warning restore CS0618 // Type or member is obsolete
            }
            else if (modelStr != null)
            {
                if (modelStr.Equals("floating"))
                {
#pragma warning disable CS0618 // Type or member is obsolete
                    precisionModel = new PrecisionModel(PrecisionModels.Floating);
#pragma warning restore CS0618 // Type or member is obsolete
                }
                else if (modelStr.Equals("floating_single"))
                {
#pragma warning disable CS0618 // Type or member is obsolete
                    precisionModel = new PrecisionModel(PrecisionModels.FloatingSingle);
#pragma warning restore CS0618 // Type or member is obsolete
                }
                else if (modelStr.Equals("fixed"))
                {
                    throw new RuntimeException("For fixed model, must specifiy 'precisionScale'");
                }
                else
                {
                    throw new RuntimeException("Unknown precisionModel: " + modelStr);
                }
            }
        }

        public virtual GeometryFactory GeometryFactory
        {
            get
            {
#pragma warning disable CS0618 // Type or member is obsolete
                if (precisionModel is null || coordinateSequenceFactory is null)
                    throw new InvalidOperationException("precision model or coord seq factory can't be null");
                return new GeometryFactory(precisionModel, srid, coordinateSequenceFactory);
#pragma warning restore CS0618 // Type or member is obsolete
            }
        }

        public override SpatialContext CreateSpatialContext()
        {
            return new NtsSpatialContext(this);
        }
    }
}