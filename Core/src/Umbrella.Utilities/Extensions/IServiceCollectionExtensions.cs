﻿using System;
using System.Linq;
using Umbrella.Utilities;

namespace Microsoft.Extensions.DependencyInjection
{
	// TODO: Are these actually needed? Doesn't adding 2 things twice with different keys just mean last in wins?
	// Must have run into an issue at some point but can't remember... :S
	public static class IServiceCollectionExtensions2
	{
		public static IServiceCollection ReplaceTransient<TService>(this IServiceCollection services)
			where TService : class
			=> services.Remove<TService>().AddTransient<TService>();

		public static IServiceCollection ReplaceTransient<TService, TImplementation>(this IServiceCollection services)
			where TService : class
			where TImplementation : class, TService
			=> services.Remove<TService>().AddTransient<TService, TImplementation>();

		public static IServiceCollection ReplaceScoped<TService>(this IServiceCollection services)
			where TService : class
			=> services.Remove<TService>().AddScoped<TService>();

		public static IServiceCollection ReplaceScoped<TService, TImplementation>(this IServiceCollection services)
			where TService : class
			where TImplementation : class, TService
			=> services.Remove<TService>().AddScoped<TService, TImplementation>();

		public static IServiceCollection ReplaceSingleton<TService>(this IServiceCollection services)
			where TService : class
			=> services.Remove<TService>().AddSingleton<TService>();

		public static IServiceCollection ReplaceSingleton<TService, TImplementation>(this IServiceCollection services)
			where TService : class
			where TImplementation : class, TService
			=> services.Remove<TService>().AddSingleton<TService, TImplementation>();

		public static IServiceCollection ReplaceSingleton<TService>(this IServiceCollection services, TService implementation)
			where TService : class
			=> services.Remove<TService>().AddSingleton(implementation);

		public static IServiceCollection ReplaceSingleton<TService>(this IServiceCollection services, Func<IServiceProvider, TService> implementationFactory)
			where TService : class
			=> services.Remove<TService>().AddSingleton(implementationFactory);

		public static IServiceCollection Remove<TService>(this IServiceCollection services)
		{
			Guard.ArgumentNotNull(services, nameof(services));

			ServiceDescriptor serviceToRemove = services.SingleOrDefault(x => x.ServiceType == typeof(TService));

			if (serviceToRemove != null)
				services.Remove(serviceToRemove);

			return services;
		}

		public static IServiceCollection ConfigureUmbrellaOptions<TOptions>(this IServiceCollection services, Action<IServiceProvider, TOptions> optionsBuilder)
			where TOptions : class, new()
		{
			Guard.ArgumentNotNull(services, nameof(services));

			services.ReplaceSingleton(serviceProvider =>
			{
				var options = new TOptions();
				optionsBuilder?.Invoke(serviceProvider, options);

				return options;
			});

			return services;
		}
	}
}