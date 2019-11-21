﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Umbrella.Utilities.Mime.Abstractions;
using Umbrella.Utilities.TypeConverters.Abstractions;

namespace Umbrella.FileSystem.Abstractions
{
	public class UmbrellaFileProviderFactory : IUmbrellaFileProviderFactory
	{
		protected ILogger<UmbrellaFileProviderFactory> Log { get; }
		protected IMimeTypeUtility MimeTypeUtility { get; }
		protected IGenericTypeConverter GenericTypeConverter { get; }
		protected IServiceProvider Services { get; }

		public UmbrellaFileProviderFactory(
			ILogger<UmbrellaFileProviderFactory> logger,
			IMimeTypeUtility mimeTypeUtility,
			IGenericTypeConverter genericTypeConverter,
			IServiceProvider services)
		{
			Log = logger;
			MimeTypeUtility = mimeTypeUtility;
			GenericTypeConverter = genericTypeConverter;
			Services = services;
		}

		// TODO: What's the best way to register a file provider really.
		// 1. Could create an interface and register with DI as a singleton. We wouldn't need this then really. Hmmmm...
		// Why did I even write this??
		public TProvider CreateProvider<TProvider, TOptions>(TOptions options)
			where TProvider : IUmbrellaFileProvider
			where TOptions : IUmbrellaFileProviderOptions
		{
			var provider = ActivatorUtilities.CreateInstance<TProvider>(Services);
			provider.InitializeOptions(options);

			// We can cache these by creating a key based on the type names of the generic arguments.

			return provider;
		}
	}
}