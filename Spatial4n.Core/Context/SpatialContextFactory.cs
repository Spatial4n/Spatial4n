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

using Spatial4n.Core.Distance;
using Spatial4n.Core.Io;
using Spatial4n.Core.Shapes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Spatial4n.Core.Context
{
    /// <summary>
    /// Factory for a SpatialContext.
    /// </summary>
    public class SpatialContextFactory
    {
        /** Set by {@link #makeSpatialContext(java.util.Map, ClassLoader)}. */
        protected IDictionary<string, string> args;
        /** Set by {@link #makeSpatialContext(java.util.Map, ClassLoader)}. */
        //protected ClassLoader classLoader;

        /* These fields are public to make it easy to set them without bothering with setters. */
        public bool geo = true;
        public IDistanceCalculator distCalc;//defaults in SpatialContext c'tor based on geo
        public IRectangle worldBounds;//defaults in SpatialContext c'tor based on geo

        public bool normWrapLongitude = false;

        public Type wktShapeParserClass = typeof(WktShapeParser);
        public Type binaryCodecClass = typeof(BinaryCodec);


        /// <summary>
        /// The factory class is lookuped up via "spatialContextFactory" in args
        /// then falling back to a Java system property (with initial caps). If neither are specified
        /// then {@link SimpleSpatialContextFactory} is chosen.
        /// </summary>
        /// <param name="args"></param>
        /// <returns></returns>
        public static SpatialContext MakeSpatialContext(IDictionary<string, string> args)
        {
            SpatialContextFactory instance;
            string cname;
            args.TryGetValue("spatialContextFactory", out cname);
            if (cname == null)
            {
                cname = Environment.GetEnvironmentVariable("SpatialContextFactory");
            }
            if (cname == null)
            {
                instance = new SpatialContextFactory();
            }
            else
            {
                Type t = Type.GetType(cname);
                instance = (SpatialContextFactory)Activator.CreateInstance(t);
            }

            instance.Init(args);
            return instance.NewSpatialContext();
        }

        protected virtual void Init(IDictionary<string, string> args)
        {
            this.args = args;

            InitField("geo");

            InitCalculator();

            //init wktParser before worldBounds because WB needs to be parsed
            InitField("wktShapeParserClass");
            InitWorldBounds();

            InitField("normWrapLongitude");

            InitField("binaryCodecClass");
        }

        /** Gets {@code name} from args and populates a field by the same name with the value. */
        protected virtual void InitField(string name)
        {
            //  note: java.beans API is more verbose to use correctly (?) but would arguably be better
            FieldInfo field = GetType().GetField(name, BindingFlags.GetField | BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic);
            string str;
            if (args.TryGetValue(name, out str))
            {
                try
                {
                    object o;
                    if (field.FieldType == typeof(bool))
                    {
                        o = bool.Parse(str);
                    }
                    else if (field.FieldType.IsClass)
                    {
                        try
                        {
                            o = Type.GetType(str);
                        }
                        catch (TypeLoadException e)
                        {
                            throw new ApplicationException(e.Message, e);
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
                    throw new Exception(e.Message, e);
                }
                catch (Exception e)
                {
                    throw new ApplicationException(
                        "Invalid value '" + str + "' on field " + name + " of type " + field.FieldType, e);
                }
            }
        }

        protected virtual void InitCalculator()
        {
            string calcStr;
            if (!args.TryGetValue("distCalculator", out calcStr) || calcStr == null)
                return;
            if (calcStr.Equals("haversine", StringComparison.InvariantCultureIgnoreCase))
            {
                distCalc = new GeodesicSphereDistCalc.Haversine();
            }
            else if (calcStr.Equals("lawOfCosines", StringComparison.InvariantCultureIgnoreCase))
            {
                distCalc = new GeodesicSphereDistCalc.LawOfCosines();
            }
            else if (calcStr.Equals("vincentySphere", StringComparison.InvariantCultureIgnoreCase))
            {
                distCalc = new GeodesicSphereDistCalc.Vincenty();
            }
            else if (calcStr.Equals("cartesian", StringComparison.InvariantCultureIgnoreCase))
            {
                distCalc = new CartesianDistCalc();
            }
            else if (calcStr.Equals("cartesian^2", StringComparison.InvariantCultureIgnoreCase))
            {
                distCalc = new CartesianDistCalc(true);
            }
            else
            {
                throw new Exception("Unknown calculator: " + calcStr);
            }
        }

        protected virtual void InitWorldBounds()
        {
            string worldBoundsStr;
            if (!args.TryGetValue("worldBounds", out worldBoundsStr) || worldBoundsStr == null)
                return;

            //kinda ugly we do this just to read a rectangle.  TODO refactor
            var ctx = NewSpatialContext();
            worldBounds = (IRectangle)ctx.ReadShape(worldBoundsStr);//TODO use readShapeFromWkt
        }

        /** Subclasses should simply construct the instance from the initialized configuration. */
        protected internal virtual SpatialContext NewSpatialContext()
        {
            return new SpatialContext(this);
        }

        public virtual WktShapeParser MakeWktShapeParser(SpatialContext ctx)
        {
            return MakeClassInstance<WktShapeParser>(wktShapeParserClass, ctx, this);
        }

        public virtual BinaryCodec MakeBinaryCodec(SpatialContext ctx)
        {
            return MakeClassInstance<BinaryCodec>(binaryCodecClass, ctx, this);
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
                    //return clazz.cast(ctor.newInstance(ctorArgs));
                    ctorLoop_continue: { }
                }
            }
            catch (Exception e)
            {
                throw new ApplicationException(e.Message, e);
            }
            throw new ApplicationException(clazz + " needs a constructor that takes: "
                + "[" + string.Join(",", ctorArgs) + "]");
        }
    }
}
