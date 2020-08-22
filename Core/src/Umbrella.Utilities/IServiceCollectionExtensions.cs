﻿using System;
using System.Collections.Generic;
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
using Umbrella.Utilities.DataAnnotations;
using Umbrella.Utilities.DataAnnotations.Abstractions;
using Umbrella.Utilities.DataAnnotations.Options;
using Umbrella.Utilities.DependencyInjection;
using Umbrella.Utilities.Email;
using Umbrella.Utilities.Email.Abstractions;
using Umbrella.Utilities.Email.Options;
using Umbrella.Utilities.Encryption;
using Umbrella.Utilities.Encryption.Abstractions;
using Umbrella.Utilities.Encryption.Options;
using Umbrella.Utilities.FriendlyUrl;
using Umbrella.Utilities.FriendlyUrl.Abstractions;
using Umbrella.Utilities.Hosting;
using Umbrella.Utilities.Hosting.Abstractions;
using Umbrella.Utilities.Hosting.Options;
using Umbrella.Utilities.Http;
using Umbrella.Utilities.Http.Abstractions;
using Umbrella.Utilities.Http.Extensions;
using Umbrella.Utilities.Http.Options;
using Umbrella.Utilities.Imaging;
using Umbrella.Utilities.Mime;
using Umbrella.Utilities.Mime.Abstractions;
using Umbrella.Utilities.Numerics;
using Umbrella.Utilities.Numerics.Abstractions;
using Umbrella.Utilities.Options.Abstractions;
using Umbrella.Utilities.Options.Exceptions;
using Umbrella.Utilities.Security;
using Umbrella.Utilities.Security.Abstractions;
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
		/// <param name="emailFactoryOptionsBuilder">The optional <see cref="EmailFactoryOptions"/> builder.</param>
		/// <param name="emailSenderOptionsBuilder">The optional <see cref="EmailSenderOptions"/> builder.</param>
		/// <param name="hybridCacheOptionsBuilder">The optional <see cref="HybridCacheOptions"/> builder.</param>
		/// <param name="httpResourceInfoUtilityOptionsBuilder">The optional <see cref="HttpResourceInfoUtilityOptions"/> builder.</param>
		/// <param name="secureRandomStringGeneratorOptionsBuilder">The optional <see cref="SecureRandomStringGeneratorOptions"/> builder.</param>
		/// <param name="umbrellaConsoleHostingEnvironmentOptionsBuilder">The optional <see cref="UmbrellaHostingEnvironmentOptions"/> builder.</param>
		/// <param name="objectGraphValidatorOptionsBuilder">The optional <see cref="ObjectGraphValidatorOptions"/> builder.</param>
		/// <param name="httpServicesBuilder">The optional builder for all Http Services.</param>
		/// <returns>The <see cref="IServiceCollection"/> dependency injection container builder.</returns>
		/// <exception cref="ArgumentNullException">Thrown if the <paramref name="services"/> is null.</exception>
		public static IServiceCollection AddUmbrellaUtilities(
			this IServiceCollection services,
			Action<IServiceProvider, EmailFactoryOptions> emailFactoryOptionsBuilder = null,
			Action<IServiceProvider, EmailSenderOptions> emailSenderOptionsBuilder = null,
			Action<IServiceProvider, HybridCacheOptions> hybridCacheOptionsBuilder = null,
			Action<IServiceProvider, HttpResourceInfoUtilityOptions> httpResourceInfoUtilityOptionsBuilder = null,
			Action<IServiceProvider, SecureRandomStringGeneratorOptions> secureRandomStringGeneratorOptionsBuilder = null,
			Action<IServiceProvider, UmbrellaConsoleHostingEnvironmentOptions> umbrellaConsoleHostingEnvironmentOptionsBuilder = null,
			Action<IServiceProvider, ObjectGraphValidatorOptions> objectGraphValidatorOptionsBuilder = null,
			Action<Dictionary<Type, IHttpClientBuilder>> httpServicesBuilder = null)
		{
			Guard.ArgumentNotNull(services, nameof(services));

			services.AddSingleton(typeof(ICurrentUserIdAccessor<>), typeof(DefaultUserIdAccessor<>));
			services.AddSingleton(typeof(ICurrentUserRolesAccessor<>), typeof(DefaultUserRolesAccessor<>));
			services.AddSingleton<ICurrentUserClaimsAccessor, DefaultUserClaimsAccessor>();
			services.AddSingleton<ICurrentUserClaimsPrincipalAccessor, DefaultUserClaimsPrincipalAccessor>();
			services.AddSingleton<ICacheKeyUtility, CacheKeyUtility>();
			services.AddSingleton<ICertificateUtility, CertificateUtility>();
			services.AddSingleton<IConcurrentRandomGenerator, ConcurrentRandomGenerator>();
			services.AddSingleton<IEmailFactory, EmailFactory>();
			services.AddSingleton<IEmailSender, EmailSender>();
			services.AddSingleton<IFriendlyUrlGenerator, FriendlyUrlGenerator>();
			services.AddSingleton<IGenericTypeConverter, GenericTypeConverter>();
			services.AddSingleton<IHybridCache, HybridCache>();
			services.AddSingleton<ILookupNormalizer, UpperInvariantLookupNormalizer>();
			services.AddSingleton<IMimeTypeUtility, MimeTypeUtility>();
			services.AddSingleton<INonceGenerator, NonceGenerator>();
			services.AddSingleton<ISecureRandomStringGenerator, SecureRandomStringGenerator>();
			services.AddTransient(typeof(Lazy<>), typeof(LazyProxy<>));
			services.AddSingleton<IUmbrellaHostingEnvironment, UmbrellaConsoleHostingEnvironment>();
			services.AddSingleton<IObjectGraphValidator, ObjectGraphValidator>();
			services.AddSingleton<IDataExpressionFactory, DataExpressionFactory>();
			services.AddSingleton<IJwtUtility, JwtUtility>();
			services.AddSingleton<IGenericHttpServiceUtility, GenericHttpServiceUtility>();
			services.AddSingleton<IResponsiveImageHelper, ResponsiveImageHelper>();

			if (httpServicesBuilder != null)
			{
				var dict = new Dictionary<Type, IHttpClientBuilder>
				{
					[typeof(IGenericHttpService)] = services.AddHttpClient<IGenericHttpService, GenericHttpService>(),
					[typeof(IHttpResourceInfoUtility)] = services.AddHttpClient<IHttpResourceInfoUtility, HttpResourceInfoUtility>()
				};

				httpServicesBuilder(dict);
			}
			else
			{
				services.AddHttpClient<IGenericHttpService, GenericHttpService>().AddUmbrellaPolicyHandlers();
				services.AddHttpClient<IHttpResourceInfoUtility, HttpResourceInfoUtility>().AddUmbrellaPolicyHandlers();
			}

			// Options
			services.ConfigureUmbrellaOptions(emailFactoryOptionsBuilder);
			services.ConfigureUmbrellaOptions(emailSenderOptionsBuilder);
			services.ConfigureUmbrellaOptions(httpResourceInfoUtilityOptionsBuilder);
			services.ConfigureUmbrellaOptions(hybridCacheOptionsBuilder);
			services.ConfigureUmbrellaOptions(secureRandomStringGeneratorOptionsBuilder);
			services.ConfigureUmbrellaOptions(umbrellaConsoleHostingEnvironmentOptionsBuilder);
			services.ConfigureUmbrellaOptions(objectGraphValidatorOptionsBuilder);

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
				try
				{
					var options = new TOptions();
					optionsBuilder?.Invoke(serviceProvider, options);

					if (options is ISanitizableUmbrellaOptions sanitizableOptions)
						sanitizableOptions.Sanitize();

					if (options is IValidatableUmbrellaOptions validatableOptions)
						validatableOptions.Validate();

					return options;
				}
				catch (Exception exc)
				{
					throw new UmbrellaOptionsException("An error has occurred during options configuration.", exc);
				}
			});

			return services;
		}

		/// <summary>
		/// Configures the current user identifier accessor.
		/// </summary>
		/// <typeparam name="TCurrentUserIdAccessor">The type of the current user identifier accessor.</typeparam>
		/// <typeparam name="TKey">The type of the key.</typeparam>
		/// <param name="services">The services.</param>
		/// <returns>The services.</returns>
		public static IServiceCollection ConfigureCurrentUserIdAccessor<TCurrentUserIdAccessor, TKey>(this IServiceCollection services)
			where TCurrentUserIdAccessor : class, ICurrentUserIdAccessor<TKey>
		{
			services.ReplaceSingleton<ICurrentUserIdAccessor<TKey>, TCurrentUserIdAccessor>();

			return services;
		}

		/// <summary>
		/// Configures the current user claims accessor.
		/// </summary>
		/// <typeparam name="TCurrentUserClaimsAccessor">The type of the current user claims accessor.</typeparam>
		/// <param name="services">The services.</param>
		/// <returns>The services.</returns>
		public static IServiceCollection ConfigureCurrentUserClaimsAccessor<TCurrentUserClaimsAccessor>(this IServiceCollection services)
			where TCurrentUserClaimsAccessor : class, ICurrentUserClaimsAccessor
		{
			services.ReplaceSingleton<ICurrentUserClaimsAccessor, TCurrentUserClaimsAccessor>();

			return services;
		}

		/// <summary>
		/// Configures the current user claims principal accessor.
		/// </summary>
		/// <typeparam name="TCurrentUserClaimsPrincipalAccessor">The type of the current user claims principal accessor.</typeparam>
		/// <param name="services">The services.</param>
		/// <returns>The services.</returns>
		public static IServiceCollection ConfigureCurrentUserClaimsPrincipalAccessor<TCurrentUserClaimsPrincipalAccessor>(this IServiceCollection services)
			where TCurrentUserClaimsPrincipalAccessor : class, ICurrentUserClaimsPrincipalAccessor
		{
			services.ReplaceSingleton<ICurrentUserClaimsPrincipalAccessor, TCurrentUserClaimsPrincipalAccessor>();

			return services;
		}

		/// <summary>
		/// Configures the current user roles accessor.
		/// </summary>
		/// <typeparam name="TCurrentUserRolesAccessor">The type of the current user roles accessor.</typeparam>
		/// <typeparam name="TRoleType">The type of the role type.</typeparam>
		/// <param name="services">The services.</param>
		/// <returns>The services.</returns>
		public static IServiceCollection ConfigureCurrentUserRolesAccessor<TCurrentUserRolesAccessor, TRoleType>(this IServiceCollection services)
			where TCurrentUserRolesAccessor : class, ICurrentUserRolesAccessor<TRoleType>
		{
			services.ReplaceSingleton<ICurrentUserRolesAccessor<TRoleType>, TCurrentUserRolesAccessor>();

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

		/// <summary>
		/// Replaces the transient service registration with the new service.
		/// </summary>
		/// <typeparam name="TService">The type of the service.</typeparam>
		/// <typeparam name="TImplementation">The type of the implementation.</typeparam>
		/// <param name="services">The services.</param>
		/// <returns>The services.</returns>
		public static IServiceCollection ReplaceTransient<TService, TImplementation>(this IServiceCollection services)
			where TService : class
			where TImplementation : class, TService
			=> services.Remove<TService>().AddTransient<TService, TImplementation>();

		/// <summary>
		/// Replaces the transient service registration with the new service.
		/// </summary>
		/// <param name="services">The services.</param>
		/// <param name="serviceType">Type of the service.</param>
		/// <param name="implementationType">Type of the implementation.</param>
		/// <returns>The services.</returns>
		public static IServiceCollection ReplaceTransient(this IServiceCollection services, Type serviceType, Type implementationType)
			=> services.Remove(serviceType).AddTransient(serviceType, implementationType);

		/// <summary>
		/// Replaces the scoped service registration with the new service.
		/// </summary>
		/// <typeparam name="TService">The type of the service.</typeparam>
		/// <typeparam name="TImplementation">The type of the implementation.</typeparam>
		/// <param name="services">The services.</param>
		/// <returns>The services.</returns>
		public static IServiceCollection ReplaceScoped<TService, TImplementation>(this IServiceCollection services)
			where TService : class
			where TImplementation : class, TService
			=> services.Remove<TService>().AddScoped<TService, TImplementation>();

		/// <summary>
		/// Replaces the scoped service registration with the new service.
		/// </summary>
		/// <param name="services">The services.</param>
		/// <param name="serviceType">Type of the service.</param>
		/// <param name="implementationType">Type of the implementation.</param>
		/// <returns>The services.</returns>
		public static IServiceCollection ReplaceScoped(this IServiceCollection services, Type serviceType, Type implementationType)
			=> services.Remove(serviceType).AddScoped(serviceType, implementationType);

		/// <summary>
		/// Replaces the singleton service registration with the new service.
		/// </summary>
		/// <typeparam name="TService">The type of the service.</typeparam>
		/// <typeparam name="TImplementation">The type of the implementation.</typeparam>
		/// <param name="services">The services.</param>
		/// <returns>The services.</returns>
		public static IServiceCollection ReplaceSingleton<TService, TImplementation>(this IServiceCollection services)
			where TService : class
			where TImplementation : class, TService
			=> services.Remove<TService>().AddSingleton<TService, TImplementation>();

		/// <summary>
		/// Replaces the singleton service registration with the new service.
		/// </summary>
		/// <param name="services">The services.</param>
		/// <param name="serviceType">Type of the service.</param>
		/// <param name="implementationType">Type of the implementation.</param>
		/// <returns>The services.</returns>
		public static IServiceCollection ReplaceSingleton(this IServiceCollection services, Type serviceType, Type implementationType)
			=> services.Remove(serviceType).AddSingleton(serviceType, implementationType);

		/// <summary>
		/// Replaces the singleton service registration with the new service.
		/// </summary>
		/// <typeparam name="TService">The type of the service.</typeparam>
		/// <param name="services">The services.</param>
		/// <param name="implementationFactory">The implementation factory.</param>
		/// <returns>The services.</returns>
		public static IServiceCollection ReplaceSingleton<TService>(this IServiceCollection services, Func<IServiceProvider, TService> implementationFactory)
			where TService : class
			=> services.Remove<TService>().AddSingleton(implementationFactory);

		/// <summary>
		/// Removes all occurrences of the specified <typeparamref name="TService"/>.
		/// </summary>
		/// <typeparam name="TService">The type of the service.</typeparam>
		/// <param name="services">The services.</param>
		/// <returns>The services.</returns>
		public static IServiceCollection Remove<TService>(this IServiceCollection services)
			=> services.Remove(typeof(TService));

		/// <summary>
		/// Removes all occurrences of the specified service type.
		/// </summary>
		/// <param name="services">The services.</param>
		/// <param name="serviceType">Type of the service.</param>
		/// <returns>The services.</returns>
		public static IServiceCollection Remove(this IServiceCollection services, Type serviceType)
		{
			Guard.ArgumentNotNull(services, nameof(services));
			Guard.ArgumentNotNull(serviceType, nameof(serviceType));

			foreach (ServiceDescriptor serviceDescriptor in services.Where(x => x.ServiceType == serviceType).ToArray())
			{
				services.Remove(serviceDescriptor);
			}

			return services;
		}
	}
}