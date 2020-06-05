﻿using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using Moq;
using Umbrella.Internal.Mocks;
using Umbrella.Legacy.WebUtilities.Hosting;
using Umbrella.Utilities.Caching;
using Umbrella.Utilities.Data.Abstractions;
using Umbrella.Utilities.Hosting.Options;
using Xunit;

namespace Umbrella.Legacy.WebUtilities.Test.Hosting
{
	public class UmbrellaWebHostingEnvironmentTest
	{
		public static List<object[]> UrlsToTest = new List<object[]>
		{
			new object[] { "/path/to/a/resource.jpg" },
			new object[] { "path/to/a/resource.jpg" },
			new object[] { "~/path/to/a/resource.jpg" },
			new object[] { "//path////to/a/resource.jpg" }
		};

		[Theory]
		[MemberData(nameof(UrlsToTest))]
		public void TransformPath_EnsureStartsWithTildeSlash(string path)
		{
			var env = CreateHostingEnvironment();

			string transformedPath = env.TransformPath(path, true, false, false);

			Assert.Equal("~/path/to/a/resource.jpg", transformedPath);
		}

		[Theory]
		[MemberData(nameof(UrlsToTest))]
		public void TransformPath_VirtualPath_EnsureNoTilde_EnsureLeadingSlash(string path)
		{
			var env = CreateHostingEnvironment();

			string transformedPath = env.TransformPath(path, false, true, true);

			Assert.Equal("/path/to/a/resource.jpg", transformedPath);
		}

		private UmbrellaWebHostingEnvironment CreateHostingEnvironment()
		{
			var logger = new Mock<ILogger<UmbrellaWebHostingEnvironment>>();
			var memoryCache = CoreUtilitiesMocks.CreateHybridCache();
			ILookupNormalizer lookupNormalizer = CoreUtilitiesMocks.CreateILookupNormalizer();
			var cacheKeyUtility = new CacheKeyUtility(new Mock<ILogger<CacheKeyUtility>>().Object, lookupNormalizer);

			return new UmbrellaWebHostingEnvironment(logger.Object, new UmbrellaHostingEnvironmentOptions(), memoryCache, cacheKeyUtility);
		}
	}
}