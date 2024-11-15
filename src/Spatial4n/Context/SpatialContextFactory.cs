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
using Spatial4n.IO;
using Spatial4n.Shapes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Spatial4n.Context
{
    /// <summary>
    /// Factory for a <see cref="SpatialContext"/> based on configuration data.  Call
    /// <see cref="MakeSpatialContext(IDictionary{string, string}, Assembly?)"/> to construct one via string name-value
    /// pairs. To construct one via code then create a factory instance, set the properties, then call
    /// <see cref="CreateSpatialContext()"/>.
    /// </summary>
    public class SpatialContextFactory
    {
        /// <summary>
        /// Set by <see cref="MakeSpatialContext(IDictionary{string, string}, Assembly?)"/>.
        /// </summary>
        protected IDictionary<string, string>? args;
        /// <summary>
        /// Set by <see cref="MakeSpatialContext(IDictionary{string, string}, Assembly?)"/>
        /// </summary>
        protected Assembly? assembly;

        // These fields are public to make it easy to set them without bothering with setters.
        [Obsolete("Use IsGeo property instead. This field will be removed in 0.5.0."), System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never), CLSCompliant(false)]
        public bool geo = true;
        public bool IsGeo
        {
#pragma warning disable CS0618 // Type or member is obsolete
            get => geo;
            set => geo = value;
#pragma warning restore CS0618 // Type or member is obsolete
        }
        [Obsolete("Use DistanceCalculator property instead. This field will be removed in 0.5.0."), System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never), CLSCompliant(false)]
        public IDistanceCalculator? distCalc;//defaults in SpatialContext c'tor based on geo
        public IDistanceCalculator? DistanceCalculator
        {
#pragma warning disable CS0618 // Type or member is obsolete
            get => distCalc;
            set => distCalc = value;
#pragma warning restore CS0618 // Type or member is obsolete
        }
        [Obsolete("Use WorldBounds property instead. This field will be removed in 0.5.0."), System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never), CLSCompliant(false)]
        public IRectangle? worldBounds;//defaults in SpatialContext c'tor based on geo
        public IRectangle? WorldBounds
        {
#pragma warning disable CS0618 // Type or member is obsolete
            get => worldBounds;
            set => worldBounds = value;
#pragma warning restore CS0618 // Type or member is obsolete
        }

        [Obsolete("Use NormWrapLongitude property instead. This field will be removed in 0.5.0."), System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never), CLSCompliant(false)]
        public bool normWrapLongitude = false;
        public bool NormWrapLongitude
        {
#pragma warning disable CS0618 // Type or member is obsolete
            get => normWrapLongitude;
            set => normWrapLongitude = value;
#pragma warning restore CS0618 // Type or member is obsolete
        }

        [Obsolete("Use WktShapeParserType property instead. This field will be removed in 0.5.0."), System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never), CLSCompliant(false)]
        public Type wktShapeParserClass = typeof(WktShapeParser);
        public Type WktShapeParserType
        {
#pragma warning disable CS0618 // Type or member is obsolete
            get => wktShapeParserClass;
            set => wktShapeParserClass = value;
#pragma warning restore CS0618 // Type or member is obsolete
        }
        [Obsolete("Use BinaryCodecType property instead. This field will be removed in 0.5.0."), System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never), CLSCompliant(false)]
        public Type binaryCodecClass = typeof(BinaryCodec);
        public Type BinaryCodecType
        {
#pragma warning disable CS0618 // Type or member is obsolete
            get => binaryCodecClass;
            set => binaryCodecClass = value;
#pragma warning restore CS0618 // Type or member is obsolete
        }

        /// <summary>
        /// Creates a new <see cref="SpatialContext"/> based on configuration in
        /// <paramref name="args"/>.  See the class definition for what keys are looked up
        /// in it.
        /// The factory class is looked up via "spatialContextFactory" in args
        /// then falling back to an <see cref="Environment.GetEnvironmentVariable(string)"/> setting (with initial caps). If neither are specified
        /// then <see cref="SpatialContextFactory"/> is chosen.
        /// </summary>
        /// <param name="args">Non-null map of name-value pairs.</param>
        /// <exception cref="ArgumentNullException"><paramref name="args"/> is <c>null</c>.</exception>
        public static SpatialContext MakeSpatialContext(IDictionary<string, string> args) // spatial4n specific - Allow calling without supplying Assembly
        {
            return MakeSpatialContext(args, null);
        }

        /// <summary>
        /// Creates a new <see cref="SpatialContext"/> based on configuration in
        /// <paramref name="args"/>.  See the class definition for what keys are looked up
        /// in it.
        /// The factory class is looked up via "spatialContextFactory" in args
        /// then falling back to an <see cref="Environment.GetEnvironmentVariable(string)"/> setting (with initial caps). If neither are specified
        /// then <see cref="SpatialContextFactory"/> is chosen.
        /// </summary>
        /// <param name="args">Non-null map of name-value pairs.</param>
        /// <param name="assembly">Optional, used to resolve class name arguments provided in <paramref name="args"/>.
        /// Falls back to the Spatial4n assembly if not provided. If the type is not found in the provided (or default) assembly,
        /// <see cref="Type.GetType(string)"/> is used to resolve the type.</param>
        /// <exception cref="ArgumentNullException"><paramref name="args"/> is <c>null</c>.</exception>
        public static SpatialContext MakeSpatialContext(IDictionary<string, string> args, Assembly? assembly)
        {
            if (args is null)
                throw new ArgumentNullException(nameof(args)); // spatial4n specific - use ArgumentNullException instead of NullReferenceException
            if (assembly is null)
                assembly = typeof(SpatialContextFactory).Assembly;

            SpatialContextFactory instance;
            if ((!args.TryGetValue("SpatialContextFactory", out string cname) && !args.TryGetValue("spatialContextFactory", out cname)) || cname is null)
            {
                cname = Environment.GetEnvironmentVariable("SpatialContextFactory");
            }
            if (cname is null)
            {
                instance = new SpatialContextFactory();
            }
            else
            {
                Type t = assembly.GetType(cname) ?? Type.GetType(cname); // spatial4n specific - fall back to all loaded types not found in the assembly (since it may be passed in as null)
                instance = (SpatialContextFactory)Activator.CreateInstance(t);
            }

            instance.Init(args, assembly);
            return instance.CreateSpatialContext();
        }

        /// <exception cref="ArgumentNullException"><see cref="args"/> is <c>null</c>.</exception>
        protected virtual void Init(IDictionary<string, string> args, Assembly? assembly)
        {
            this.args = args ?? throw new ArgumentNullException(nameof(args)); // spatial4n specific - use ArgumentNullException instead of NullReferenceException
            this.assembly = assembly ?? typeof(SpatialContextFactory).Assembly; // spatial4n specific - default to the current assembly, just as in MakeSpatialContext()

            if (!InitProperty("IsGeo"))
                InitField("geo");

            InitCalculator();

            //init wktParser before worldBounds because WB needs to be parsed
            if (!InitProperty("WktShapeParserType"))
                InitField("wktShapeParserClass");
            
            InitWorldBounds();

            if (!InitProperty("NormWrapLongitude"))
                InitField("normWrapLongitude");

            if (!InitProperty("BinaryCodecType"))
                InitField("binaryCodecClass");
        }

        /// <exception cref="ArgumentNullException"><see cref="args"/> is <c>null</c>.</exception>
        protected virtual void Init(IDictionary<string, string> args) // spatial4n specific - this API unfortunately made it into the release before Classloader equivalent was identified as Assembly. This is just here to avoid a breaking change.
        {
            Init(args, null);
        }

        /// <summary>
        /// Gets <paramref name="name"/> from args and populates a field by the same name with the value.
        /// </summary>
        /// <exception cref="InvalidOperationException"><see cref="args"/> is not set prior to calling this method.</exception>
        protected virtual void InitField(string name)
        {
            if (args is null)
                throw new InvalidOperationException($"'{nameof(args)}' must be set prior to calling InitField()"); // spatial4n specific - use InvalidOperationException instead of NullReferenceException

            //  note: java.beans API is more verbose to use correctly (?) but would arguably be better
            FieldInfo field = GetType().GetField(name, BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic);
            if (args.TryGetValue(name, out string str))
            {
                if (field is null)
                    throw new InvalidOperationException($"Field not found on {GetType().FullName}: {name}");
                if (field.IsInitOnly)
                    throw new InvalidOperationException($"Field {name} is read-only, so may not be set.");

                try
                {
                    object o;
                    if (field.FieldType == typeof(bool))
                    {
                        o = bool.Parse(str);
                    }
                    else if (field.FieldType.IsClass)
                    {
                        if (assembly is null)
                            throw new InvalidOperationException($"'{nameof(assembly)}' must be set prior to calling InitField()");
                        try
                        {
                            o = assembly.GetType(str) ?? Type.GetType(str); // spatial4n specific - fall back to all loaded types not found in the assembly (since it may be passed in as null)
                        }
                        catch (TypeLoadException e)
                        {
                            throw new Exception(e.ToString(), e);
                        }
                    }
                    else if (field.FieldType.IsEnum)
                    {
                        o = Enum.Parse(field.FieldType, str, true);
                    }
                    else
                    {
                        throw new Exception("unsupported field type: " + field.FieldType);//not plausible at runtime unless developing
                    }
                    field.SetValue(this, o);
                }
                catch (FieldAccessException e)
                {
                    throw new Exception(e.ToString(), e);
                }
                catch (Exception e)
                {
                    throw new Exception(
                        "Invalid value '" + str + "' on field " + name + " of type " + field.FieldType, e);
                }
            }
        }

        /// <summary>
        /// Gets <paramref name="name"/> from args and populates a property by the same name with the value.
        /// </summary>
        /// <returns><c>true</c> if the property was set; otherwise, <c>false</c>.</returns>
        /// <exception cref="InvalidOperationException"><see cref="args"/> is not set prior to calling this method.</exception>
        protected virtual bool InitProperty(string name) // Spatial4n specific - allow setting the properties and fallback to fields if they are not set.
        {
            if (args is null)
                throw new InvalidOperationException($"'{nameof(args)}' must be set prior to calling InitProperty()"); // spatial4n specific - use InvalidOperationException instead of NullReferenceException

            //  note: java.beans API is more verbose to use correctly (?) but would arguably be better
            PropertyInfo property = GetType().GetProperty(name, BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic);
            if (args.TryGetValue(name, out string str))
            {
                if (property is null)
                    throw new InvalidOperationException($"Property not found on {GetType().FullName}: {name}");
                if (!property.CanWrite)
                    throw new InvalidOperationException($"Property {name} is read-only, so may not be set.");

                try
                {
                    object o;
                    if (property.PropertyType == typeof(bool))
                    {
                        o = bool.Parse(str);
                    }
                    else if (property.PropertyType.IsClass)
                    {
                        if (assembly is null)
                            throw new InvalidOperationException($"'{nameof(assembly)}' must be set prior to calling InitField()");
                        try
                        {
                            o = assembly.GetType(str) ?? Type.GetType(str); // spatial4n specific - fall back to all loaded types not found in the assembly (since it may be passed in as null)
                        }
                        catch (TypeLoadException e)
                        {
                            throw new Exception(e.ToString(), e);
                        }
                    }
                    else if (property.PropertyType.IsEnum)
                    {
                        o = Enum.Parse(property.PropertyType, str, true);
                    }
                    else
                    {
                        throw new Exception("unsupported field type: " + property.PropertyType);//not plausible at runtime unless developing
                    }
                    property.SetValue(this, o, null);
                    return true;
                }
                catch (FieldAccessException e)
                {
                    throw new Exception(e.ToString(), e);
                }
                catch (Exception e)
                {
                    throw new Exception(
                        "Invalid value '" + str + "' on field " + name + " of type " + property.PropertyType, e);
                }
            }
            return false;
        }

        /// <exception cref="InvalidOperationException"><see cref="args"/> is not set prior to calling this method.</exception>
        protected virtual void InitCalculator()
        {
            if (args is null)
                throw new InvalidOperationException($"'{nameof(args)}' must be set prior to calling InitCalculator()"); // spatial4n specific - use InvalidOperationException instead of NullReferenceException

            if ((!args.TryGetValue("DistanceCalculator", out string calcStr) && !args.TryGetValue("distCalculator", out calcStr)) || calcStr is null)
                return;
            if (calcStr.Equals("haversine", StringComparison.OrdinalIgnoreCase))
            {
                DistanceCalculator = new GeodesicSphereDistanceCalculator.Haversine();
            }
            else if (calcStr.Equals("lawOfCosines", StringComparison.OrdinalIgnoreCase))
            {
                DistanceCalculator = new GeodesicSphereDistanceCalculator.LawOfCosines();
            }
            else if (calcStr.Equals("vincentySphere", StringComparison.OrdinalIgnoreCase))
            {
                DistanceCalculator = new GeodesicSphereDistanceCalculator.Vincenty();
            }
            else if (calcStr.Equals("cartesian", StringComparison.OrdinalIgnoreCase))
            {
                DistanceCalculator = new CartesianDistanceCalculator();
            }
            else if (calcStr.Equals("cartesian^2", StringComparison.OrdinalIgnoreCase))
            {
                DistanceCalculator = new CartesianDistanceCalculator(true);
            }
            else
            {
                throw new Exception("Unknown calculator: " + calcStr);
            }
        }

        /// <exception cref="InvalidOperationException"><see cref="args"/> is not set prior to calling this method.</exception>
        protected virtual void InitWorldBounds()
        {
            if (args is null)
                throw new InvalidOperationException($"'{nameof(args)}' must be set prior to calling InitWorldBounds()"); // spatial4n specific - use InvalidOperationException instead of NullReferenceException

            if ((!args.TryGetValue("WorldBounds", out string worldBoundsStr) && !args.TryGetValue("worldBounds", out worldBoundsStr)) || worldBoundsStr is null)
                return;

            //kinda ugly we do this just to read a rectangle.  TODO refactor
            var ctx = CreateSpatialContext();
#pragma warning disable 612, 618
            worldBounds = (IRectangle)ctx.ReadShape(worldBoundsStr);//TODO use readShapeFromWkt
#pragma warning restore 612, 618
        }

        [Obsolete("Use CreateSpatialContext() instead."), System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
        protected internal virtual SpatialContext NewSpatialContext()
        {
            return CreateSpatialContext();
        }

        /// <summary>
        /// Subclasses should simply construct the instance from the initialized configuration.
        /// <para/>
        /// Usage Note: Use this method wherever newSpatialContext() is used in Java.
        /// </summary>
        // spatial4n: unfortunately, the release happened before it was noticed that NewSpatialContext() was marked
        // protected, when it was supposed to be public. So, this API was added as a way to avoid a breaking change.
        public virtual SpatialContext CreateSpatialContext()
        {
            return new SpatialContext(this);
        }

        public virtual WktShapeParser MakeWktShapeParser(SpatialContext ctx)
        {
            return MakeClassInstance<WktShapeParser>(WktShapeParserType, ctx, this);
        }

        public virtual BinaryCodec MakeBinaryCodec(SpatialContext ctx)
        {
            return MakeClassInstance<BinaryCodec>(BinaryCodecType, ctx, this);
        }

        private T MakeClassInstance<T>(Type clazz, params object[] ctorArgs)
        {
            try
            {
                //can't simply lookup constructor by arg type because might be subclass type
                foreach (ConstructorInfo ctor in clazz.GetConstructors())
                {
                    Type[] parameterTypes = ctor.GetParameters().Select(x => x.ParameterType).ToArray();
                    if (parameterTypes.Length != ctorArgs.Length)
                        continue;
                    for (int i = 0; i < ctorArgs.Length; i++)
                    {
                        object ctorArg = ctorArgs[i];
                        if (!parameterTypes[i].IsAssignableFrom(ctorArg.GetType()))
                            goto ctorLoop_continue;
                    }
                    return (T)ctor.Invoke(ctorArgs);
                    ctorLoop_continue: { }
                }
            }
            catch (Exception e)
            {
                throw new Exception(e.Message, e);
            }
            throw new Exception(clazz + " needs a constructor that takes: "
                + "[" + string.Join(",", ctorArgs.Select(x => x.GetType().ToString()).ToArray()) + "]");
        }
    }
}
