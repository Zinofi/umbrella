﻿using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Order;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging;
using Microsoft.Owin;
using Moq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Umbrella.Legacy.WebUtilities.Middleware;
using Umbrella.Legacy.WebUtilities.Middleware.Options;
using Umbrella.Utilities.Caching.Abstractions;
using Umbrella.Utilities.Hosting;
using Umbrella.Utilities.Mime;
using Umbrella.WebUtilities.Http;

namespace Umbrella.Legacy.WebUtilities.Benchmark.Middleware
{
	[ClrJob]
	[MemoryDiagnoser]
	public class FrontEndCompressionMiddlewareBenchmark
	{
		private readonly FrontEndCompressionMiddleware _frontEndCompressionMiddleware;

		public FrontEndCompressionMiddlewareBenchmark()
		{
			var logger = new Mock<ILogger<FrontEndCompressionMiddleware>>();
			var cache = new Mock<IMultiCache>();
			var hostingEnvironment = new Mock<IUmbrellaHostingEnvironment>();
			var httpHeaderValueUtility = new Mock<IHttpHeaderValueUtility>();
			var mimeTypeUtility = new Mock<IMimeTypeUtility>();

			var options = new FrontEndCompressionMiddlewareOptions
			{
				FrontEndRootFolderAppRelativePaths = new[] { "/sitefiles" }
			};

			_frontEndCompressionMiddleware = new FrontEndCompressionMiddleware(
				null,
				logger.Object,
				cache.Object,
				hostingEnvironment.Object,
				httpHeaderValueUtility.Object,
				mimeTypeUtility.Object,
				options);

			var fileProvider = new Mock<IFileProvider>();

			_frontEndCompressionMiddleware.FileProvider = fileProvider.Object;
		}

		[Benchmark]
		public async Task RunMiddleware()
		{
			var context = new Mock<IOwinContext>();

			await _frontEndCompressionMiddleware.Invoke(context.Object);
		}
	}
}