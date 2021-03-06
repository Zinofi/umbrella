﻿using Microsoft.Owin;
using System;
using System.Linq;
using System.Threading.Tasks;
using Umbrella.Utilities.Extensions;
using System.Net;
using System.Web.Configuration;
using Umbrella.Legacy.WebUtilities.DynamicImage.Configuration;
using Microsoft.Extensions.Logging;
using Umbrella.DynamicImage.Abstractions;
using Umbrella.Legacy.WebUtilities.DynamicImage.Middleware.Options;
using Umbrella.Utilities;
using Umbrella.WebUtilities.Http;
using System.Threading;
using System.Collections.Generic;
using Umbrella.Legacy.WebUtilities.Extensions;

namespace Umbrella.Legacy.WebUtilities.DynamicImage.Middleware
{
	public class DynamicImageMiddleware : OwinMiddleware
	{
		#region Private Static Members
		private static List<string> s_RegisteredDynamicImagePathPrefixList = new List<string>();
		#endregion

		#region Private Members
		private readonly ILogger Log;
		private readonly IDynamicImageUtility m_DynamicImageUtility;
		private readonly IDynamicImageResizer m_DynamicImageResizer;
		private readonly IHttpHeaderValueUtility m_HeaderValueUtility;
		private readonly Lazy<DynamicImageConfigurationOptions> m_ConfigurationOptions = new Lazy<DynamicImageConfigurationOptions>(LoadConfigurationOptions);
		private readonly DynamicImageMiddlewareOptions m_MiddlewareOptions = new DynamicImageMiddlewareOptions();
		#endregion

		#region Private Properties
		private DynamicImageConfigurationOptions ConfigurationOptions => m_ConfigurationOptions.Value;
		#endregion

		// TODO: V3 - Change the optionsBuilder to options.
		#region Constructors
		public DynamicImageMiddleware(OwinMiddleware next,
			ILogger<DynamicImageMiddleware> logger,
			IDynamicImageUtility dynamicImageUtility,
			IDynamicImageResizer dynamicImageResizer,
			IHttpHeaderValueUtility headerValueUtility,
			Action<DynamicImageMiddlewareOptions> optionsBuilder)
			: base(next)
		{
			Log = logger;
			m_DynamicImageUtility = dynamicImageUtility;
			m_DynamicImageResizer = dynamicImageResizer;
			m_HeaderValueUtility = headerValueUtility;

			optionsBuilder?.Invoke(m_MiddlewareOptions);

			Guard.ArgumentNotNull(m_MiddlewareOptions.SourceFileProvider, nameof(m_MiddlewareOptions.SourceFileProvider));
			Guard.ArgumentNotNullOrWhiteSpace(m_MiddlewareOptions.DynamicImagePathPrefix, nameof(m_MiddlewareOptions.DynamicImagePathPrefix));

			//Ensure that only one instance of the middleware can be registered for a specified path prefix value
			if (s_RegisteredDynamicImagePathPrefixList.Contains(m_MiddlewareOptions.DynamicImagePathPrefix, StringComparer.OrdinalIgnoreCase))
				throw new DynamicImageException($"The application is trying to register multiple instances of the {nameof(DynamicImageMiddleware)} with the same prefix: {m_MiddlewareOptions.DynamicImagePathPrefix}. This is not allowed.");

			s_RegisteredDynamicImagePathPrefixList.Add(m_MiddlewareOptions.DynamicImagePathPrefix);
		}
		#endregion

		#region Overridden Methods
		public override async Task Invoke(IOwinContext context)
		{
			context.Request.CallCancelled.ThrowIfCancellationRequested();

			var cts = CancellationTokenSource.CreateLinkedTokenSource(context.Request.CallCancelled);
			CancellationToken token = cts.Token;

			try
			{
				string path = context.Request.Path.Value;

				DynamicImageFormat? overrideFormat = null;

				if(m_MiddlewareOptions.EnableJpgPngWebPOverride
					&& (path.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase) || path.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
					&& context.Request.AcceptsWebP())
				{
					overrideFormat = DynamicImageFormat.WebP;
				}

				var (status, imageOptions) = m_DynamicImageUtility.TryParseUrl(m_MiddlewareOptions.DynamicImagePathPrefix, path, overrideFormat);

				if (status == DynamicImageParseUrlResult.Skip)
				{
					await Next.Invoke(context);
					return;
				}

				if (status == DynamicImageParseUrlResult.Invalid || !m_DynamicImageUtility.ImageOptionsValid(imageOptions, ConfigurationOptions))
				{
					cts.Cancel();
					await context.Response.SendStatusCode(HttpStatusCode.NotFound);
					return;
				}
				
				DynamicImageItem image = await m_DynamicImageResizer.GenerateImageAsync(m_MiddlewareOptions.SourceFileProvider, imageOptions, token);

				if (image == null)
				{
					cts.Cancel();
					await context.Response.SendStatusCode(HttpStatusCode.NotFound);
					return;
				}

				//Check the cache headers
				if (context.Request.IfModifiedSinceHeaderMatched(image.LastModified))
				{
					cts.Cancel();
					await context.Response.SendStatusCode(HttpStatusCode.NotModified);
					return;
				}

				string eTagValue = m_HeaderValueUtility.CreateETagHeaderValue(image.LastModified, image.Length);

				if (context.Request.IfNoneMatchHeaderMatched(eTagValue))
				{
					cts.Cancel();
					await context.Response.SendStatusCode(HttpStatusCode.NotModified);
					return;
				}

				if (image.Length > 0)
				{
					AppendResponseHeaders(context.Response, image);

					await image.WriteContentToStreamAsync(context.Response.Body, token);

					// Ensure the response stream is flushed async immediately here. If not, there could be content
					// still buffered which will not be sent out until the stream is disposed at which point
					// the IO will happen synchronously!
					await context.Response.Body.FlushAsync(token);

					return;
				}
				else
				{
					cts.Cancel();
					await context.Response.SendStatusCode(HttpStatusCode.NotFound);
					return;
				}
			}
			catch (Exception exc) when (Log.WriteError(exc, message: "Error in DynamicImageModule for path: " + context.Request.Path, returnValue: true))
			{
				cts.Cancel();
				await context.Response.SendStatusCode(HttpStatusCode.NotFound);
				return;
			}
		}
		#endregion

		#region Private Methods
		private void AppendResponseHeaders(IOwinResponse response, DynamicImageItem image)
		{
			response.ContentType = "image/" + image.ImageOptions.Format.ToString().ToLowerInvariant();
			response.ContentLength = image.Length;
			response.Headers["Last-Modified"] = m_HeaderValueUtility.CreateLastModifiedHeaderValue(image.LastModified);
			response.ETag = m_HeaderValueUtility.CreateETagHeaderValue(image.LastModified, image.Length);

			if (!string.IsNullOrWhiteSpace(m_MiddlewareOptions.CacheControlHeaderValue))
				response.Headers["Cache-Control"] = m_MiddlewareOptions.CacheControlHeaderValue.Trim().ToLowerInvariant();
		}

		private static DynamicImageConfigurationOptions LoadConfigurationOptions()
		{
			var mappingsConfig = new DynamicImageMappingsConfig(WebConfigurationManager.OpenWebConfiguration("~/web.config"));
			var options = (DynamicImageConfigurationOptions)mappingsConfig;

			return options;
		}
		#endregion
	}
}