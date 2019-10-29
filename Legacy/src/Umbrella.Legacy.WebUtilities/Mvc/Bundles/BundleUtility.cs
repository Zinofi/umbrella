﻿using System;
using System.IO;
using System.Runtime.CompilerServices;
using System.Web.Mvc;
using Microsoft.Extensions.Logging;
using Umbrella.Legacy.WebUtilities.Mvc.Bundles.Abstractions;
using Umbrella.Legacy.WebUtilities.Mvc.Bundles.Options;
using Umbrella.Utilities;
using Umbrella.Utilities.Caching.Abstractions;
using Umbrella.WebUtilities.Exceptions;
using Umbrella.WebUtilities.Hosting;

namespace Umbrella.Legacy.WebUtilities.Mvc.Bundles
{
	public class BundleUtility : BundleUtility<BundleUtilityOptions>
	{
		public BundleUtility(
			ILogger<BundleUtility> logger,
			BundleUtilityOptions options,
			IHybridCache multiCache,
			IUmbrellaWebHostingEnvironment hostingEnvironment)
			: base(logger, options, multiCache, hostingEnvironment)
		{
		}
	}

	public abstract class BundleUtility<TOptions> : IBundleUtility
		where TOptions : BundleUtilityOptions
	{
		protected ILogger Log { get; }
		protected TOptions Options { get; }
		protected IHybridCache Cache { get; }
		protected IUmbrellaWebHostingEnvironment HostingEnvironment { get; }

		public BundleUtility(
			ILogger logger,
			TOptions options,
			IHybridCache multiCache,
			IUmbrellaWebHostingEnvironment hostingEnvironment)
		{
			Log = logger;
			Options = options;
			Cache = multiCache;
			HostingEnvironment = hostingEnvironment;

			Guard.ArgumentNotNullOrWhiteSpace(Options.DefaultBundleFolderAppRelativePath, "The default path to the bundles must be specified.");

			// Ensure the path ends with a slash
			if (!Options.DefaultBundleFolderAppRelativePath.EndsWith("/"))
				Options.DefaultBundleFolderAppRelativePath += "/";
		}

		public virtual MvcHtmlString GetScript(string bundleNameOrPath)
		{
			Guard.ArgumentNotNullOrWhiteSpace(bundleNameOrPath, nameof(bundleNameOrPath));

			try
			{
				return GetScript(bundleNameOrPath, true);
			}
			catch (Exception exc) when (Log.WriteError(exc, new { bundleNameOrPath }, returnValue: true))
			{
				throw new UmbrellaWebException("There has been a problem creating the HTML script tag.", exc);
			}
		}

		public MvcHtmlString GetScriptInline(string bundleNameOrPath)
		{
			Guard.ArgumentNotNullOrWhiteSpace(bundleNameOrPath, nameof(bundleNameOrPath));

			try
			{
				return BuildOutput(bundleNameOrPath, () =>
				{
					string content = ResolveBundleContent(bundleNameOrPath, "js");

					var builder = new TagBuilder("script")
					{
						InnerHtml = content
					};

					return MvcHtmlString.Create(builder.ToString());
				});
			}
			catch (Exception exc) when (Log.WriteError(exc, new { bundleNameOrPath }, returnValue: true))
			{
				throw new UmbrellaWebException("There was a problem getting the script content.", exc);
			}
		}

		public virtual MvcHtmlString GetStyleSheet(string bundleNameOrPath)
		{
			Guard.ArgumentNotNullOrWhiteSpace(bundleNameOrPath, nameof(bundleNameOrPath));

			try
			{
				return GetStyleSheet(bundleNameOrPath, true);
			}
			catch (Exception exc) when (Log.WriteError(exc, new { bundleNameOrPath }, returnValue: true))
			{
				throw new UmbrellaWebException("There has been a problem creating the Webpack HTML style tag.", exc);
			}
		}

		public MvcHtmlString GetStyleSheetInline(string bundleNameOrPath)
		{
			Guard.ArgumentNotNullOrWhiteSpace(bundleNameOrPath, nameof(bundleNameOrPath));

			try
			{
				return BuildOutput(bundleNameOrPath, () =>
				{
					string content = ResolveBundleContent(bundleNameOrPath, "css");

					var builder = new TagBuilder("style")
					{
						InnerHtml = content
					};

					return MvcHtmlString.Create(builder.ToString());
				});
			}
			catch (Exception exc) when (Log.WriteError(exc, new { bundleNameOrPath }, returnValue: true))
			{
				throw new UmbrellaWebException("There was a problem getting the stylesheet content.", exc);
			}
		}

		protected MvcHtmlString BuildOutput(string bundleNameOrPath, Func<MvcHtmlString> builder, [CallerMemberName] string caller = "") =>
			// When watching the source files, we can't cache the generated HTML string here and need to rebuild it everytime.
			Options.WatchFiles
				? builder()
				: Cache.GetOrCreate(caller + ":" + bundleNameOrPath,
				() => builder(),
				Options);

		protected string ResolveBundlePath(string bundleNameOrPath, string bundleType, bool appendVersion)
			=> HostingEnvironment.MapWebPath(DetermineBundlePath(bundleNameOrPath, bundleType), appendVersion: Options.AppendVersion ?? appendVersion, watchWhenAppendVersion: Options.WatchFiles);

		protected string ResolveBundleContent(string bundleNameOrPath, string bundleType)
		{
			ConfiguredTaskAwaitable<string> task = HostingEnvironment.GetFileContentAsync(DetermineBundlePath(bundleNameOrPath, bundleType), Options.CacheEnabled, Options.WatchFiles).ConfigureAwait(false);

			return task.GetAwaiter().GetResult();
		}

		protected virtual string DetermineBundlePath(string bundleNameOrPath, string bundleType)
		{
			if (Path.HasExtension(bundleNameOrPath))
				bundleNameOrPath = bundleNameOrPath.Substring(0, bundleNameOrPath.LastIndexOf('.'));

			bundleNameOrPath += "." + bundleType;

			if (bundleNameOrPath.StartsWith("~") || bundleNameOrPath.StartsWith("/"))
				return bundleNameOrPath.ToLowerInvariant();

			return Path.Combine(Options.DefaultBundleFolderAppRelativePath, bundleNameOrPath).ToLowerInvariant();
		}

		protected MvcHtmlString GetScript(string bundleNameOrPath, bool appendVersion)
			=> BuildOutput(
				bundleNameOrPath,
				() =>
				{
					string path = ResolveBundlePath(bundleNameOrPath, "js", appendVersion);

					var builder = new TagBuilder("script");
					builder.MergeAttribute("defer", "defer");
					builder.MergeAttribute("src", path);

					return MvcHtmlString.Create(builder.ToString());
				});

		protected MvcHtmlString GetStyleSheet(string bundleNameOrPath, bool appendVersion)
			=> BuildOutput(
				bundleNameOrPath,
				() =>
				{
					string path = ResolveBundlePath(bundleNameOrPath, "css", appendVersion);

					var builder = new TagBuilder("link");
					builder.MergeAttribute("rel", "stylesheet");
					builder.MergeAttribute("href", path);

					return MvcHtmlString.Create(builder.ToString(TagRenderMode.SelfClosing));
				});
	}
}