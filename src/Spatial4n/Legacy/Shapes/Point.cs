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
#nullable disable
using System;

namespace Spatial4n.Core.Shapes
{
    /// <summary>
    /// A Point with X &amp; Y coordinates.
    /// </summary>
    [Obsolete("Use Spatial4n.Shapes.IPoint instead. This class will be removed in 0.5.0."), System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
    public interface IPoint : IShape
    {
        /// <summary>
        /// Expert: Resets the state of this point given the arguments. This is a performance
        /// feature to avoid excessive <see cref="IShape"/> object allocation as well as some
        /// argument normalization &amp; error checking. Mutable shapes is error-prone so use with
        /// care.
        /// </summary>
        void Reset(double x, double y);

        /// <summary>
        /// The X coordinate, or Longitude in geospatial contexts.
        /// </summary>
        /// <returns></returns>
        double X { get; }

        /// <summary>
        /// The Y coordinate, or Latitude in geospatial contexts.
        /// </summary>
        /// <returns></returns>
        double Y { get; }
    }
}
