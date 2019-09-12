﻿using System;
using Umbrella.FileSystem.Abstractions;
using Umbrella.FileSystem.Disk;
using Umbrella.Utilities;

namespace Microsoft.Extensions.DependencyInjection
{
	/// <summary>
	/// Extension methods used to register services for the <see cref="Umbrella.FileSystem.Disk"/> package with a specified
	/// <see cref="IServiceCollection"/> dependency injection container builder.
	/// </summary>
	public static class IServiceCollectionExtensions
    {
		/// <summary>
		/// Adds the <see cref="Umbrella.FileSystem.Disk"/> services to the specified <see cref="IServiceCollection"/> dependency injection container builder.
		/// </summary>
		/// <param name="services">The services dependency injection container builder to which the services will be added.</param>
		/// <param name="optionsBuilder">The <see cref="UmbrellaDiskFileProviderOptions"/> builder.</param>
		/// <returns>The <see cref="IServiceCollection"/> dependency injection container builder.</returns>
		/// <exception cref="ArgumentNullException">Thrown if the <paramref name="services"/> is null.</exception>
		/// <exception cref="ArgumentNullException">Thrown if the <paramref name="optionsBuilder"/> is null.</exception>
		public static IServiceCollection AddUmbrellaDiskFileProvider(this IServiceCollection services, Action<IServiceProvider, UmbrellaDiskFileProviderOptions> optionsBuilder)
            => AddUmbrellaDiskFileProvider<UmbrellaDiskFileProvider>(services, optionsBuilder);

		/// <summary>
		/// Adds the <see cref="Umbrella.FileSystem.Disk"/> services to the specified <see cref="IServiceCollection"/> dependency injection container builder.
		/// </summary>
		/// <typeparam name="TFileProvider">
		/// The concrete implementation of <see cref="IUmbrellaDiskFileProvider"/> to register. This allows consuming applications to override the default implementation and allow it to be
		/// resolved from the container correctly for both the <see cref="IUmbrellaFileProvider"/> and <see cref="IUmbrellaDiskFileProvider"/> interfaces.
		/// </typeparam>
		/// <param name="services">The services dependency injection container builder to which the services will be added.</param>
		/// <param name="optionsBuilder">The <see cref="UmbrellaDiskFileProviderOptions"/> builder.</param>
		/// <returns>The <see cref="IServiceCollection"/> dependency injection container builder.</returns>
		/// <exception cref="ArgumentNullException">Thrown if the <paramref name="services"/> is null.</exception>
		/// <exception cref="ArgumentNullException">Thrown if the <paramref name="optionsBuilder"/> is null.</exception>
		public static IServiceCollection AddUmbrellaDiskFileProvider<TFileProvider>(this IServiceCollection services, Action<IServiceProvider, UmbrellaDiskFileProviderOptions> optionsBuilder)
            where TFileProvider : class, IUmbrellaDiskFileProvider
        {
            Guard.ArgumentNotNull(services, nameof(services));
            Guard.ArgumentNotNull(optionsBuilder, nameof(optionsBuilder));

            services.AddSingleton(optionsBuilder);
            services.AddSingleton<IUmbrellaDiskFileProvider, TFileProvider>();
            services.AddSingleton<IUmbrellaFileProvider>(x => x.GetService<IUmbrellaDiskFileProvider>());

            return services;
        }
    }
}