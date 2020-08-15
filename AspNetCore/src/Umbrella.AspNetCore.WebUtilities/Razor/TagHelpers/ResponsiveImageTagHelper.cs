﻿using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.TagHelpers;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Umbrella.Utilities.Caching.Abstractions;
using Umbrella.Utilities.Imaging;
using Umbrella.WebUtilities.Hosting;

namespace Umbrella.AspNetCore.WebUtilities.Razor.TagHelpers
{
	/// <summary>
	/// A tag helper used to create responsive "img" tags.
	/// </summary>
	/// <seealso cref="Microsoft.AspNetCore.Razor.TagHelpers.TagHelper" />
	[OutputElementHint("img")]
	[HtmlTargetElement("img", Attributes = RequiredAttributeNames)]
	public class ResponsiveImageTagHelper : TagHelper
	{
		private const string RequiredAttributeNames = "src," + PixelDensitiesAttributeName;

		/// <summary>
		/// The pixel densities attribute name.
		/// </summary>
		protected const string PixelDensitiesAttributeName = "um-pixel-densities";

		#region Public Properties		
		/// <summary>
		/// Gets or sets the pixel densities as a comma delimited list of integers.
		/// </summary>
		[HtmlAttributeName(PixelDensitiesAttributeName)]
		public string PixelDensities { get; set; } = "1";
		#endregion

		#region Protected Properties		
		/// <summary>
		/// Gets the logger.
		/// </summary>
		protected ILogger Logger { get; }

		/// <summary>
		/// Gets the cache.
		/// </summary>
		protected IHybridCache Cache { get; }

		/// <summary>
		/// Gets the cache key utility.
		/// </summary>
		public ICacheKeyUtility CacheKeyUtility { get; }

		/// <summary>
		/// Gets the umbrella hosting environment.
		/// </summary>
		protected IUmbrellaWebHostingEnvironment UmbrellaHostingEnvironment { get; }

		/// <summary>
		/// Gets the responsive image helper.
		/// </summary>
		public IResponsiveImageHelper ResponsiveImageHelper { get; }
		#endregion

		#region Constructors		
		/// <summary>
		/// Initializes a new instance of the <see cref="ResponsiveImageTagHelper"/> class.
		/// </summary>
		/// <param name="logger">The logger.</param>
		/// <param name="umbrellaHostingEnvironment">The umbrella hosting environment.</param>
		/// <param name="cache">The cache.</param>
		/// <param name="cacheKeyUtility">The cache key utility.</param>
		/// <param name="responsiveImageHelper">The responsive image helper.</param>
		public ResponsiveImageTagHelper(
			ILogger<ResponsiveImageTagHelper> logger,
			IUmbrellaWebHostingEnvironment umbrellaHostingEnvironment,
			IHybridCache cache,
			ICacheKeyUtility cacheKeyUtility,
			IResponsiveImageHelper responsiveImageHelper)
		{
			Logger = logger;
			UmbrellaHostingEnvironment = umbrellaHostingEnvironment;
			Cache = cache;
			CacheKeyUtility = cacheKeyUtility;
			ResponsiveImageHelper = responsiveImageHelper;
		}
		#endregion

		#region Overridden Methods
		/// <inheritdoc />
		public override async Task ProcessAsync(TagHelperContext context, TagHelperOutput output)
		{
			await base.ProcessAsync(context, output);

			string? path = output.Attributes["src"]?.Value as string ?? context.AllAttributes["src"]?.Value?.ToString();

			if (!string.IsNullOrWhiteSpace(path))
			{
				// Cache this using the image src attribute value and PixelDensities
				string key = CacheKeyUtility.Create<ResponsiveImageTagHelper>($"{path}:{PixelDensities}");

				string srcsetValue = Cache.GetOrCreate(
					key,
					() => ResponsiveImageHelper.GetPixelDensitySrcSetValue(path, PixelDensities, ResolveImageUrl),
					() => TimeSpan.FromHours(1),
					priority: CacheItemPriority.Low);

				if (!string.IsNullOrWhiteSpace(srcsetValue))
					output.Attributes.Add("srcset", srcsetValue);
			}
		}
		#endregion

		#region Protected Methods		
		/// <summary>
		/// Resolves the image URL.
		/// </summary>
		/// <param name="url">The URL.</param>
		/// <returns>The resolved image url.</returns>
		protected string ResolveImageUrl(string url)
		{
			if (url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
				return url;

			return UmbrellaHostingEnvironment.MapWebPath(url);
		}
		#endregion
	}
}