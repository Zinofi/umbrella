﻿using System;
using System.Collections.Generic;
using Umbrella.DynamicImage.Abstractions;
using Umbrella.FileSystem.Abstractions;
using Umbrella.Utilities;
using Umbrella.Utilities.Options.Abstractions;
using Umbrella.WebUtilities.Middleware.Options;

namespace Umbrella.WebUtilities.DynamicImage.Middleware.Options
{
	/// <summary>
	/// Specifies a file provider mapping and options for that mapping for use with the dynamic image middleware.
	/// </summary>
	/// <seealso cref="Umbrella.Utilities.Options.Abstractions.IValidatableUmbrellaOptions" />
	/// <seealso cref="Umbrella.Utilities.Options.Abstractions.ISanitizableUmbrellaOptions" />
	public class DynamicImageMiddlewareMapping : IValidatableUmbrellaOptions, ISanitizableUmbrellaOptions
	{
		/// <summary>
		/// Gets or sets a value indicating whether validation of the dynamic images being generated is performed.
		/// This is to ensure that only specific pre-determined options can be provided as part of the incoming URLs for the images
		/// to prevent overloading the server. Defaults to <see langword="false" />.
		/// </summary>
		public bool EnableValidation { get; set; }

		/// <summary>
		/// Gets or sets the valid mappings.
		/// </summary>
		public HashSet<DynamicImageMapping> ValidMappings { get; set; } = new HashSet<DynamicImageMapping>();

		/// <summary>
		/// Gets or sets the cacheability. Defaults to <see cref="MiddlewareHttpCacheability.NoCache" />.
		/// </summary>
		public MiddlewareHttpCacheability Cacheability { get; set; } = MiddlewareHttpCacheability.NoCache;

		/// <summary>
		/// Gets or sets the file provider mapping.
		/// </summary>
		public UmbrellaFileProviderMapping FileProviderMapping { get; set; }

		/// <inheritdoc />
		public void Sanitize()
		{
			FileProviderMapping?.Sanitize();
		}

		/// <inheritdoc />
		public void Validate()
		{
			Guard.ArgumentNotNull(FileProviderMapping, nameof(FileProviderMapping));
			FileProviderMapping.Validate();

			switch (Cacheability)
			{
				case MiddlewareHttpCacheability.Private:
				case MiddlewareHttpCacheability.Public:
					throw new ArgumentException("Public and Private values are not permitted.", nameof(Cacheability));
			}

			if (EnableValidation)
				Guard.ArgumentNotNullOrEmpty(ValidMappings, nameof(ValidMappings));
		}
	}
}