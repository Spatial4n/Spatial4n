﻿/* 
 * Licensed to the Apache Software Foundation (ASF) under one or more
 * contributor license agreements.  See the NOTICE file distributed with
 * this work for additional information regarding copyright ownership.
 * The ASF licenses this file to You under the Apache License, Version 2.0
 * (the "License"); you may not use this file except in compliance with
 * the License.  You may obtain a copy of the License at
 * 
 * http://www.apache.org/licenses/LICENSE-2.0
 * 
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */
#nullable disable
using Spatial4n.Core.Context;
using Spatial4n.Core.Shapes;
using System;
using System.Diagnostics;
using System.Text;

namespace Spatial4n.Core.Util
{
    /// <summary>
    ///  Utilities for encoding and decoding <a href="http://en.wikipedia.org/wiki/Geohash">geohashes</a>.
    ///  <para/>
    ///  This class isn't used by any other part of Spatial4n; it's included largely for convenience of
    ///  software using Spatial4n. There are other open-source libraries that have more comprehensive
    ///  geohash utilities but providing this one avoids an additional dependency for what's a small
    ///  amount of code.  <c>If you're using Spatial4n just for this class, consider alternatives.</c>
    ///  <para/>
    ///  This code originally came from <a href="https://issues.apache.org/jira/browse/LUCENE-1512">
    ///  Apache Lucene, LUCENE-1512</a>.
    /// </summary>
    [Obsolete("Use Spatial4n.Util.GeohashUtils instead. This class will be removed in 0.5.0."), System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
    public static class GeohashUtils
    {
        private static readonly char[] BASE_32 = {
                                                     '0', '1', '2', '3', '4', '5', '6', '7', '8', '9',
                                                     'b', 'c', 'd', 'e', 'f', 'g', 'h', 'j', 'k', 'm',
                                                     'n', 'p', 'q', 'r', 's', 't', 'u', 'v', 'w', 'x',
                                                     'y', 'z'
                                                 }; //note: this is sorted

        private static readonly int[] BASE_32_IDX; //sparse array of indexes from '0' to 'z'

        public const int MAX_PRECISION = 24; //DWS: I forget what level results in needless more precision but it's about this

        private static readonly int[] BITS = { 16, 8, 4, 2, 1 };

        static GeohashUtils()
        {
            BASE_32_IDX = new int[BASE_32[BASE_32.Length - 1] - BASE_32[0] + 1];
            Debug.Assert(BASE_32_IDX.Length < 100);//reasonable length
            for (int i = 0; i < BASE_32_IDX.Length; i++)
            {
                BASE_32_IDX[i] = -500;
            }

            for (int i = 0; i < BASE_32.Length; i++)
            {
                BASE_32_IDX[BASE_32[i] - BASE_32[0]] = i;
            }

            hashLenToLatHeight = new double[MAX_PRECISION + 1];
            hashLenToLonWidth = new double[MAX_PRECISION + 1];
            hashLenToLatHeight[0] = 90 * 2;
            hashLenToLonWidth[0] = 180 * 2;
            bool even = false;
            for (int i = 1; i <= MAX_PRECISION; i++)
            {
                hashLenToLatHeight[i] = hashLenToLatHeight[i - 1] / (even ? 8 : 4);
                hashLenToLonWidth[i] = hashLenToLonWidth[i - 1] / (even ? 4 : 8);
                even = !even;
            }
        }

        /// <summary>
        /// Encodes the given latitude and longitude into a geohash
        /// </summary>
        /// <param name="latitude">Latitude to encode</param>
        /// <param name="longitude">Longitude to encode</param>
        /// <returns>Geohash encoding of the longitude and latitude</returns>
        public static string EncodeLatLon(double latitude, double longitude)
        {
            return EncodeLatLon(latitude, longitude, 12);
        }

        /// <summary>
        /// Encodes the given latitude and longitude into a geohash
        /// </summary>
        /// <param name="latitude">Latitude to encode</param>
        /// <param name="longitude">Longitude to encode</param>
        /// <param name="precision"></param>
        /// <returns>Geohash encoding of the longitude and latitude</returns>
        public static string EncodeLatLon(double latitude, double longitude, int precision)
        {
            double[] latInterval = { -90.0, 90.0 };
            double[] lonInterval = { -180.0, 180.0 };

            var geohash = new StringBuilder();
            bool isEven = true;
            int bit = 0, ch = 0;

            while (geohash.Length < precision)
            {
                double mid = 0.0d;
                if (isEven)
                {
                    mid = (lonInterval[0] + lonInterval[1]) / 2;
                    if (longitude > mid)
                    {
                        ch |= BITS[bit];
                        lonInterval[0] = mid;
                    }
                    else
                    {
                        lonInterval[1] = mid;
                    }
                }
                else
                {
                    mid = (latInterval[0] + latInterval[1]) / 2;
                    if (latitude > mid)
                    {
                        ch |= BITS[bit];
                        latInterval[0] = mid;
                    }
                    else
                    {
                        latInterval[1] = mid;
                    }
                }

                isEven = !isEven;

                if (bit < 4)
                {
                    bit++;
                }
                else
                {
                    geohash.Append(BASE_32[ch]);
                    bit = 0;
                    ch = 0;
                }
            }

            return geohash.ToString();
        }

        /// <summary>
        /// Decodes the given geohash into a latitude and longitude
        /// </summary>
        /// <param name="geohash">Geohash to deocde</param>
        /// <param name="ctx"></param>
        /// <returns>Array with the latitude at index 0, and longitude at index 1</returns>
        public static IPoint Decode(string geohash, SpatialContext ctx)
        {
            IRectangle rect = DecodeBoundary(geohash, ctx);
            double latitude = (rect.MinY + rect.MaxY) / 2D;
            double longitude = (rect.MinX + rect.MaxX) / 2D;
            return ctx.MakePoint(longitude, latitude);
        }

        /// <summary>
        /// Returns min-max lat, min-max lon.
        /// </summary>
        public static IRectangle DecodeBoundary(string geohash, SpatialContext ctx)
        {
            double minY = -90, maxY = 90, minX = -180, maxX = 180;
            bool isEven = true;

            for (int i = 0; i < geohash.Length; i++)
            {
                char c = geohash[i];
                if (c >= 'A' && c <= 'Z')
                    c = Convert.ToChar(c - Convert.ToChar('A' - 'a'));

                int cd = BASE_32_IDX[c - BASE_32[0]]; //TODO check successful?

                foreach (var mask in BITS)
                {
                    if (isEven)
                    {
                        if ((cd & mask) != 0)
                        {
                            minX = (minX + maxX) / 2D;
                        }
                        else
                        {
                            maxX = (minX + maxX) / 2D;
                        }
                    }
                    else
                    {
                        if ((cd & mask) != 0)
                        {
                            minY = (minY + maxY) / 2D;
                        }
                        else
                        {
                            maxY = (minY + maxY) / 2D;
                        }
                    }
                    isEven = !isEven;
                }
            }
            return ctx.MakeRectangle(minX, maxX, minY, maxY);
        }

        /// <summary>Array of geohashes 1 level below the baseGeohash. Sorted.</summary>
        public static string[] GetSubGeohashes(string baseGeohash)
        {
            var hashes = new string[BASE_32.Length];
            for (int i = 0; i < BASE_32.Length; i++)
            {//note: already sorted
                char c = BASE_32[i];
                hashes[i] = baseGeohash + c;
            }
            return hashes;
        }

        public static double[] LookupDegreesSizeForHashLen(int hashLen)
        {
            return new double[] { hashLenToLatHeight[hashLen], hashLenToLonWidth[hashLen] };
        }

        /// <summary>
        /// Return the shortest geohash length that will have a width &amp; height >= specified arguments.
        /// </summary>
        public static int LookupHashLenForWidthHeight(double lonErr, double latErr)
        {
            //loop through hash length arrays from beginning till we find one.
            for (int len = 1; len < MAX_PRECISION; len++)
            {
                double latHeight = hashLenToLatHeight[len];
                double lonWidth = hashLenToLonWidth[len];
                if (latHeight < latErr && lonWidth < lonErr)
                    return len;
            }
            return MAX_PRECISION;
        }

        /** See the table at http://en.wikipedia.org/wiki/Geohash */
        private static readonly double[] hashLenToLatHeight;
        private static readonly double[] hashLenToLonWidth;
    }
}