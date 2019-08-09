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

using System.Collections.Generic;
using System.Linq;
using Xunit;
using Xunit.Extensions;
using Xunit.Sdk;

namespace Spatial4n.Core
{
	public class RepeatFactAttribute : FactAttribute
	{
		readonly int _count;

		public RepeatFactAttribute(int count)
		{
			_count = count;
		}

#if !NETCOREAPP
		protected override IEnumerable<ITestCommand> EnumerateTestCommands(
			IMethodInfo method)
		{
			return base.EnumerateTestCommands(method)
				.SelectMany(tc => Enumerable.Repeat(tc, _count));
		}
#endif
    }

    public class RepeatTheoryAttribute : TheoryAttribute
	{
		readonly int _count;

		public RepeatTheoryAttribute(int count)
		{
			_count = count;
		}

#if !NETCOREAPP
        // Spatial4n TODO: Work out how to make the test repeat in .NET Core
        protected override IEnumerable<ITestCommand> EnumerateTestCommands(
			IMethodInfo method)
		{
			return base.EnumerateTestCommands(method)
				.SelectMany(tc => Enumerable.Repeat(tc, _count));
		}
#endif
    }
}
