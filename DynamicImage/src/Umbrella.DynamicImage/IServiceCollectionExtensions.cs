﻿using System;
using Umbrella.DynamicImage;
using Umbrella.DynamicImage.Abstractions;
using Umbrella.DynamicImage.Caching;
using Umbrella.Utilities;

namespace Microsoft.Extensions.DependencyInjection
{
	/// <summary>
	/// Extension methods used to register services for the <see cref="Umbrella.DynamicImage"/> package with a specified
	/// <see cref="IServiceCollection"/> dependency injection container builder.
	/// </summary>
	public static class IServiceCollectionExtensions
	{
		/// <summary>
		/// Adds the core <see cref="Umbrella.DynamicImage"/> services to the specified <see cref="IServiceCollection"/> dependency injection container builder. Caching is disabled by default
		/// which means dynamically generated images are created every time they are requested.
		/// To enable caching, call one of the other methods instead, e.g. <see cref="AddUmbrellaDynamicImageMemoryCache"/> or <see cref="AddUmbrellaDynamicImageDiskCache"/>.
		/// </summary>
		/// <param name="services">The services dependency injection container builder to which the services will be added.</param>
		/// <returns>The <see cref="IServiceCollection"/> dependency injection container builder.</returns>
		/// <exception cref="ArgumentNullException">Thrown if the <paramref name="services"/> is null.</exception>
		public static IServiceCollection AddUmbrellaDynamicImageCore(this IServiceCollection services)
		{
			Guard.ArgumentNotNull(services, nameof(services));

			services.AddSingleton<IDynamicImageUtility, DynamicImageUtility>();
			services.AddSingleton<IDynamicImageCache, DynamicImageNoCache>();

			return services;
		}

		/// <summary>
		/// Adds the <see cref="Umbrella.DynamicImage"/> services to the specified <see cref="IServiceCollection"/> dependency injection container builder
		/// with in-memory caching.
		/// </summary>
		/// <param name="services">The services dependency injection container builder to which the services will be added.</param>
		/// <param name="dynamicImageCacheCoreOptionsBuilder">The optional <see cref="DynamicImageCacheCoreOptions"/> builder.</param>
		/// <param name="dynamicImageMemoryCacheOptionsBuilder">The optional <see cref="DynamicImageMemoryCacheOptions"/> builder.</param>
		/// <returns>The <see cref="IServiceCollection"/> dependency injection container builder.</returns>
		/// <exception cref="ArgumentNullException">Thrown if the <paramref name="services"/> is null.</exception>
		public static IServiceCollection AddUmbrellaDynamicImageMemoryCache(
			this IServiceCollection services,
			Action<IServiceProvider, DynamicImageCacheCoreOptions> dynamicImageCacheCoreOptionsBuilder = null,
			Action<IServiceProvider, DynamicImageMemoryCacheOptions> dynamicImageMemoryCacheOptionsBuilder = null)
		{
			Guard.ArgumentNotNull(services, nameof(services));

			services.AddUmbrellaDynamicImageCore();
			services.ReplaceSingleton<IDynamicImageCache, DynamicImageMemoryCache>();
			services.ConfigureUmbrellaOptions(dynamicImageCacheCoreOptionsBuilder);
			services.ConfigureUmbrellaOptions(dynamicImageMemoryCacheOptionsBuilder);

			return services;
		}

		/// <summary>
		/// Adds the <see cref="Umbrella.DynamicImage"/> services to the specified <see cref="IServiceCollection"/> dependency injection container builder
		/// with disk caching.
		/// </summary>
		/// <param name="services">The services dependency injection container builder to which the services will be added.</param>
		/// <param name="dynamicImageCacheCoreOptionsBuilder">The optional <see cref="DynamicImageCacheCoreOptions"/> builder.</param>
		/// <param name="dynamicImageDiskCacheOptionsBuilder">The optional <see cref="DynamicImageDiskCacheOptions"/> builder.</param>
		/// <returns>The <see cref="IServiceCollection"/> dependency injection container builder.</returns>
		/// <exception cref="ArgumentNullException">Thrown if the <paramref name="services"/> is null.</exception>
		public static IServiceCollection AddUmbrellaDynamicImageDiskCache(
			this IServiceCollection services,
			Action<IServiceProvider, DynamicImageCacheCoreOptions> dynamicImageCacheCoreOptionsBuilder = null,
			Action<IServiceProvider, DynamicImageDiskCacheOptions> dynamicImageDiskCacheOptionsBuilder = null)
		{
			Guard.ArgumentNotNull(services, nameof(services));

			services.AddUmbrellaDynamicImageCore();
			services.ReplaceSingleton<IDynamicImageCache, DynamicImageDiskCache>();
			services.ConfigureUmbrellaOptions(dynamicImageCacheCoreOptionsBuilder);
			services.ConfigureUmbrellaOptions(dynamicImageDiskCacheOptionsBuilder);

			return services;
		}
	}
}