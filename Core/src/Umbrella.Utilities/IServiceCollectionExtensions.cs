﻿using System;
using System.Linq;
using System.Runtime.CompilerServices;
using Umbrella.Utilities;
using Umbrella.Utilities.Caching;
using Umbrella.Utilities.Caching.Abstractions;
using Umbrella.Utilities.Caching.Options;
using Umbrella.Utilities.Context;
using Umbrella.Utilities.Context.Abstractions;
using Umbrella.Utilities.Data;
using Umbrella.Utilities.Data.Abstractions;
using Umbrella.Utilities.DependencyInjection;
using Umbrella.Utilities.Email;
using Umbrella.Utilities.Email.Abstractions;
using Umbrella.Utilities.Email.Options;
using Umbrella.Utilities.Encryption;
using Umbrella.Utilities.Encryption.Abstractions;
using Umbrella.Utilities.Encryption.Options;
using Umbrella.Utilities.FriendlyUrl;
using Umbrella.Utilities.FriendlyUrl.Abstractions;
using Umbrella.Utilities.Hosting.Options;
using Umbrella.Utilities.Http;
using Umbrella.Utilities.Http.Abstractions;
using Umbrella.Utilities.Http.Options;
using Umbrella.Utilities.Mime;
using Umbrella.Utilities.Mime.Abstractions;
using Umbrella.Utilities.Numerics;
using Umbrella.Utilities.Numerics.Abstractions;
using Umbrella.Utilities.Options.Abstractions;
using Umbrella.Utilities.TypeConverters;
using Umbrella.Utilities.TypeConverters.Abstractions;

[assembly: InternalsVisibleTo("Umbrella.Utilities.Benchmark")]

namespace Microsoft.Extensions.DependencyInjection
{
	/// <summary>
	/// Extension methods used to register services for the <see cref="Umbrella.Utilities"/> package with a specified
	/// <see cref="IServiceCollection"/> dependency injection container builder.
	/// Extension methods are also provided to allow for registrations to be removed and replaced.
	/// </summary>
	public static class IServiceCollectionExtensions
	{
		/// <summary>
		/// Adds the <see cref="Umbrella.Utilities"/> services to the specified <see cref="IServiceCollection"/> dependency injection container builder.
		/// </summary>
		/// <param name="services">The services dependency injection container builder to which the services will be added.</param>
		/// <param name="emailBuilderOptionsBuilder">The optional <see cref="EmailFactoryOptions"/> builder.</param>
		/// <param name="hybridCacheOptionsBuilder">The optional <see cref="HybridCacheOptions"/> builder.</param>
		/// <param name="httpResourceInfoUtilityOptionsBuilder">The optional <see cref="HttpResourceInfoUtilityOptions"/> builder.</param>
		/// <param name="secureRandomStringGeneratorOptionsBuilder">The optional <see cref="SecureRandomStringGeneratorOptions"/> builder.</param>
		/// <param name="umbrellaHostingEnvironmentOptionsBuilder">The optional <see cref="UmbrellaHostingEnvironmentOptions"/> builder.</param>
		/// <returns>The <see cref="IServiceCollection"/> dependency injection container builder.</returns>
		/// <exception cref="ArgumentNullException">Thrown if the <paramref name="services"/> is null.</exception>
		public static IServiceCollection AddUmbrellaUtilities(
			this IServiceCollection services,
			Action<IServiceProvider, EmailFactoryOptions> emailBuilderOptionsBuilder = null,
			Action<IServiceProvider, HybridCacheOptions> hybridCacheOptionsBuilder = null,
			Action<IServiceProvider, HttpResourceInfoUtilityOptions> httpResourceInfoUtilityOptionsBuilder = null,
			Action<IServiceProvider, SecureRandomStringGeneratorOptions> secureRandomStringGeneratorOptionsBuilder = null,
			Action<IServiceProvider, UmbrellaHostingEnvironmentOptions> umbrellaHostingEnvironmentOptionsBuilder = null)
		{
			Guard.ArgumentNotNull(services, nameof(services));

			services.AddSingleton<ICacheKeyUtility, CacheKeyUtility>();
			services.AddSingleton<ICertificateUtility, CertificateUtility>();
			services.AddSingleton<IConcurrentRandomGenerator, ConcurrentRandomGenerator>();
			services.AddSingleton<ILookupNormalizer, UpperInvariantLookupNormalizer>();
			services.AddSingleton<IFriendlyUrlGenerator, FriendlyUrlGenerator>();
			services.AddSingleton<IGenericTypeConverter, GenericTypeConverter>();
			services.AddSingleton<IHttpResourceInfoUtility, HttpResourceInfoUtility>();
			services.AddSingleton<IHybridCache, HybridCache>();
			services.AddSingleton<IMimeTypeUtility, MimeTypeUtility>();
			services.AddSingleton<INonceGenerator, NonceGenerator>();
			services.AddSingleton<ISecureRandomStringGenerator, SecureRandomStringGenerator>();
			services.AddTransient(typeof(Lazy<>), typeof(LazyProxy<>));
			services.AddSingleton<IEmailFactory, EmailFactory>();
			services.AddSingleton(typeof(ICurrentUserIdAccessor<>), typeof(DefaultUserIdAccessor<>));
			services.AddSingleton(typeof(ICurrentUserRolesAccessor<>), typeof(DefaultUserRolesAccessor<>));

			// Options
			services.ConfigureUmbrellaOptions(emailBuilderOptionsBuilder);
			services.ConfigureUmbrellaOptions(httpResourceInfoUtilityOptionsBuilder);
			services.ConfigureUmbrellaOptions(hybridCacheOptionsBuilder);
			services.ConfigureUmbrellaOptions(secureRandomStringGeneratorOptionsBuilder);
			services.ConfigureUmbrellaOptions(umbrellaHostingEnvironmentOptionsBuilder);

			return services;
		}

		/// <summary>
		/// Configures the specified Umbrella Options denoted by <typeparamref name="TOptions"/>.
		/// </summary>
		/// <typeparam name="TOptions">The type of the options.</typeparam>
		/// <param name="services">The services.</param>
		/// <param name="optionsBuilder">The options builder.</param>
		/// <returns>
		/// The same instance of <see cref="IServiceCollection"/> as passed in but with the Umbrella Options type specified by
		/// <typeparamref name="TOptions"/> added to it.
		/// </returns>
		public static IServiceCollection ConfigureUmbrellaOptions<TOptions>(this IServiceCollection services, Action<IServiceProvider, TOptions> optionsBuilder)
			where TOptions : class, new()
		{
			Guard.ArgumentNotNull(services, nameof(services));

			services.ReplaceSingleton(serviceProvider =>
			{
				var options = new TOptions();
				optionsBuilder?.Invoke(serviceProvider, options);

				if (options is ISanitizableUmbrellaOptions sanitizableOptions)
					sanitizableOptions.Sanitize();

				if (options is IValidatableUmbrellaOptions validatableOptions)
					validatableOptions.Validate();

				return options;
			});

			return services;
		}

		public static IServiceCollection ConfigureCurrentUserIdAccessor<T, TKey>(this IServiceCollection services)
			where T : class, ICurrentUserIdAccessor<TKey>
		{
			services.ReplaceSingleton<ICurrentUserIdAccessor<TKey>, T>();

			return services;
		}

		public static IServiceCollection ConfigureCurrentUserRolesAccessor<T, TRoleType>(this IServiceCollection services)
			where T : class, ICurrentUserRolesAccessor<TRoleType>
		{
			services.ReplaceSingleton<ICurrentUserRolesAccessor<TRoleType>, T>();

			return services;
		}

		// TODO: Really need have an encryption utility options class with properties for key and iv and register with DI
		// It's a breaking change though so leave until v3.
		//public static IServiceCollection AddUmbrellaEncryptionUtility<T>(this IServiceCollection services, string encryptionKey, string iv)
		//	where T : class, IEncryptionUtility
		//{
		//	services.AddSingleton<IEncryptionUtility, T>

		//	return services;
		//}

		public static IServiceCollection ReplaceTransient<TService, TImplementation>(this IServiceCollection services)
			where TService : class
			where TImplementation : class, TService
			=> services.Remove<TService>().AddTransient<TService, TImplementation>();

		public static IServiceCollection ReplaceTransient(this IServiceCollection services, Type serviceType, Type implementationType)
			=> services.Remove(serviceType).AddTransient(serviceType, implementationType);

		public static IServiceCollection ReplaceScoped<TService, TImplementation>(this IServiceCollection services)
			where TService : class
			where TImplementation : class, TService
			=> services.Remove<TService>().AddScoped<TService, TImplementation>();

		public static IServiceCollection ReplaceScoped(this IServiceCollection services, Type serviceType, Type implementationType)
			=> services.Remove(serviceType).AddScoped(serviceType, implementationType);

		public static IServiceCollection ReplaceSingleton<TService, TImplementation>(this IServiceCollection services)
			where TService : class
			where TImplementation : class, TService
			=> services.Remove<TService>().AddSingleton<TService, TImplementation>();

		public static IServiceCollection ReplaceSingleton(this IServiceCollection services, Type serviceType, Type implementationType)
			=> services.Remove(serviceType).AddSingleton(serviceType, implementationType);

		public static IServiceCollection ReplaceSingleton<TService>(this IServiceCollection services, Func<IServiceProvider, TService> implementationFactory)
			where TService : class
			=> services.Remove<TService>().AddSingleton(implementationFactory);

		public static IServiceCollection Remove<TService>(this IServiceCollection services)
			=> services.Remove(typeof(TService));

		public static IServiceCollection Remove(this IServiceCollection services, Type serviceType)
		{
			Guard.ArgumentNotNull(services, nameof(services));
			Guard.ArgumentNotNull(serviceType, nameof(serviceType));

			foreach(ServiceDescriptor serviceDescriptor in services.Where(x => x.ServiceType == serviceType))
			{
				services.Remove(serviceDescriptor);
			}

			return services;
		}
	}
}