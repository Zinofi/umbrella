﻿using System;
using System.Buffers;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging;
using Umbrella.Utilities;
using Umbrella.Utilities.Caching.Abstractions;
using Umbrella.Utilities.Extensions;
using Umbrella.Utilities.Hosting;
using Umbrella.WebUtilities.Exceptions;
using Umbrella.WebUtilities.Hosting;
using Umbrella.WebUtilities.Hosting.Options;

namespace Umbrella.AspNetCore.WebUtilities.Hosting
{
	/// <summary>
	/// An implementation of the <see cref="IUmbrellaWebHostingEnvironment" /> for use with ASP.NET Core applications.
	/// </summary>
	/// <seealso cref="Umbrella.Utilities.Hosting.UmbrellaHostingEnvironment" />
	/// <seealso cref="Umbrella.WebUtilities.Hosting.IUmbrellaWebHostingEnvironment" />
	public class UmbrellaWebHostingEnvironment : UmbrellaHostingEnvironment, IUmbrellaWebHostingEnvironment
	{
		#region Private Static Members
		private static readonly Regex _multipleForwardSlashRegex = new Regex("/+", RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.CultureInvariant);
		#endregion

		#region Protected Properties		
		/// <summary>
		/// Gets the hosting environment.
		/// </summary>
		protected IWebHostEnvironment HostingEnvironment { get; }

		/// <summary>
		/// Gets the HTTP context accessor.
		/// </summary>
		protected IHttpContextAccessor HttpContextAccessor { get; }

		/// <summary>
		/// Gets the web root file provider.
		/// </summary>
		protected Lazy<IFileProvider> WebRootFileProvider { get; }
		#endregion

		#region Constructors
		/// <summary>
		/// Initializes a new instance of the <see cref="UmbrellaWebHostingEnvironment"/> class.
		/// </summary>
		/// <param name="logger">The logger.</param>
		/// <param name="hostingEnvironment">The hosting environment.</param>
		/// <param name="httpContextAccessor">The HTTP context accessor.</param>
		/// <param name="options">The options.</param>
		/// <param name="cache">The cache.</param>
		/// <param name="cacheKeyUtility">The cache key utility.</param>
		public UmbrellaWebHostingEnvironment(ILogger<UmbrellaWebHostingEnvironment> logger,
			IWebHostEnvironment hostingEnvironment,
			IHttpContextAccessor httpContextAccessor,
			UmbrellaWebHostingEnvironmentOptions options,
			IHybridCache cache,
			ICacheKeyUtility cacheKeyUtility)
			: base(logger, options, cache, cacheKeyUtility)
		{
			HostingEnvironment = hostingEnvironment;
			HttpContextAccessor = httpContextAccessor;
			FileProvider = new Lazy<IFileProvider>(() => HostingEnvironment.ContentRootFileProvider);
			WebRootFileProvider = new Lazy<IFileProvider>(() => HostingEnvironment.WebRootFileProvider);
		}
		#endregion

		#region IUmbrellaHostingEnvironment Members
		/// <inheritdoc />
		public override string MapPath(string virtualPath) => MapPath(virtualPath, true);
		#endregion

		#region IUmbrellaWebHostingEnvironment Members
		/// <inheritdoc />
		public virtual string MapPath(string virtualPath, bool fromContentRoot)
		{
			Guard.ArgumentNotNullOrWhiteSpace(virtualPath, nameof(virtualPath));

			string[]? cacheKeyParts = null;

			try
			{
				cacheKeyParts = ArrayPool<string>.Shared.Rent(2);
				cacheKeyParts[0] = virtualPath;
				cacheKeyParts[1] = fromContentRoot.ToString();

				string key = CacheKeyUtility.Create<UmbrellaWebHostingEnvironment>(cacheKeyParts, 2);

				return Cache.GetOrCreate(key, () =>
				{
					// Trim and remove the ~/ from the front of the path
					// Also change forward slashes to back slashes
					string cleanedPath = TransformPath(virtualPath, true, false, true);

					string rootPath = fromContentRoot
						? HostingEnvironment.ContentRootPath
						: HostingEnvironment.WebRootPath;

					return Path.Combine(rootPath, cleanedPath);
				},
				Options);
			}
			catch (Exception exc) when (Log.WriteError(exc, new { virtualPath, fromContentRoot }, returnValue: true))
			{
				throw new UmbrellaWebException("There has been a problem mapping the specified virtual path.", exc);
			}
			finally
			{
				if (cacheKeyParts != null)
					ArrayPool<string>.Shared.Return(cacheKeyParts);
			}
		}

		/// <inheritdoc />
		public virtual string MapWebPath(string virtualPath, bool toAbsoluteUrl = false, string scheme = "http", bool appendVersion = false, string versionParameterName = "v", bool mapFromContentRoot = true, bool watchWhenAppendVersion = true)
		{
			Guard.ArgumentNotNullOrWhiteSpace(virtualPath, nameof(virtualPath));
			Guard.ArgumentNotNullOrWhiteSpace(scheme, nameof(scheme));
			Guard.ArgumentNotNullOrWhiteSpace(versionParameterName, nameof(versionParameterName));

			string[]? cacheKeyParts = null;

			try
			{
				cacheKeyParts = ArrayPool<string>.Shared.Rent(7);
				cacheKeyParts[0] = virtualPath;
				cacheKeyParts[1] = toAbsoluteUrl.ToString();
				cacheKeyParts[2] = scheme;
				cacheKeyParts[3] = appendVersion.ToString();
				cacheKeyParts[4] = versionParameterName;
				cacheKeyParts[5] = mapFromContentRoot.ToString();
				cacheKeyParts[6] = watchWhenAppendVersion.ToString();

				string key = CacheKeyUtility.Create<UmbrellaWebHostingEnvironment>(cacheKeyParts, 7);
				string cleanedPath = TransformPathForFileProvider(virtualPath);

				IFileProvider fileProvider = mapFromContentRoot
							? FileProvider.Value
							: WebRootFileProvider.Value;

				return Cache.GetOrCreate(key, () =>
				{
					// NB: This will be empty for non-virtual applications
					PathString applicationPath = HttpContextAccessor.HttpContext.Request.PathBase;

					// Prefix the path with the virtual application segment but only if the cleanedPath doesn't already start with the segment
					string url = applicationPath.HasValue && cleanedPath.StartsWith(applicationPath, StringComparison.OrdinalIgnoreCase)
						? cleanedPath
						: applicationPath.Add(cleanedPath).Value;

					if (toAbsoluteUrl)
						url = $"{scheme}://{ResolveHttpHost()}{url}";

					if (appendVersion)
					{
						IFileInfo fileInfo = fileProvider.GetFileInfo(cleanedPath);

						if (!fileInfo.Exists)
							throw new FileNotFoundException($"The specified virtual path {virtualPath} does not exist on disk at {fileInfo.PhysicalPath}.");

						long versionHash = fileInfo.LastModified.UtcDateTime.ToFileTimeUtc() ^ fileInfo.Length;
						string version = Convert.ToString(versionHash, 16);

						string qsStart = url.Contains("?") ? "&" : "?";

						url = $"{url}{qsStart}{versionParameterName}={version}";
					}

					return url;
				},
				Options,
				() => appendVersion && watchWhenAppendVersion ? new[] { fileProvider.Watch(cleanedPath) } : null);
			}
			catch (Exception exc) when (Log.WriteError(exc, new { virtualPath, toAbsoluteUrl, scheme, appendVersion, versionParameterName, mapFromContentRoot }, returnValue: true))
			{
				throw new UmbrellaWebException("There was a problem mapping the web path.", exc);
			}
		}

		/// <inheritdoc />
		public async Task<string> GetFileContentAsync(string virtualPath, bool fromContentRoot, bool cache = true, bool watch = true, CancellationToken cancellationToken = default)
		{
			cancellationToken.ThrowIfCancellationRequested();
			Guard.ArgumentNotNullOrWhiteSpace(virtualPath, nameof(virtualPath));

			try
			{
				return fromContentRoot
					? await GetFileContentAsync("Standard", FileProvider.Value, virtualPath, cache, watch, cancellationToken)
					: await GetFileContentAsync("Web", WebRootFileProvider.Value, virtualPath, cache, watch, cancellationToken);
			}
			catch (Exception exc) when (Log.WriteError(exc, new { virtualPath, cache, watch }, returnValue: true))
			{
				throw new UmbrellaWebException("There has been a problem reading the contents of the specified file.", exc);
			}
		}
		#endregion

		#region Protected Methods		
		/// <summary>
		/// Resolves the HTTP host for current request.
		/// </summary>
		/// <returns>The HTTP host.</returns>
		protected virtual string ResolveHttpHost() => HttpContextAccessor.HttpContext.Request.Host.Value;

		/// <inheritdoc />
		protected override string TransformPathForFileProvider(string virtualPath) => TransformPath(virtualPath, false, true, false);
		#endregion

		#region Internal Methods
		internal string TransformPath(string virtualPath, bool removeLeadingSlash, bool ensureLeadingSlash, bool convertForwardSlashesToBackSlashes)
		{
			StringBuilder sb = new StringBuilder(virtualPath)
				.Trim()
				.Trim('~');

			if (removeLeadingSlash && ensureLeadingSlash)
				throw new ArgumentException($"{nameof(removeLeadingSlash)} and {nameof(ensureLeadingSlash)} are both set to true. This is not allowed.");

			if (removeLeadingSlash)
				sb.Trim('/');

			if (ensureLeadingSlash && !sb.StartsWith('/'))
				sb.Insert(0, '/');

			string path = _multipleForwardSlashRegex.Replace(sb.ToString(), "/");

			// Only do this on Windows. Not required for Linux and macOS.
			if (convertForwardSlashesToBackSlashes && RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
				path = path.Replace("/", @"\");

			return path;
		}
		#endregion
	}
}