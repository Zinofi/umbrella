﻿using System;
using Umbrella.FileSystem.Abstractions;
using Umbrella.Utilities;
using Umbrella.Utilities.Options.Abstractions;
using Umbrella.WebUtilities.Middleware.Options;

namespace Umbrella.WebUtilities.FileSystem.Middleware.Options
{
	/// <summary>
	/// Specifies a file provider mapping and options for that mapping for use with the file provider middleware.
	/// </summary>
	/// <seealso cref="Umbrella.Utilities.Options.Abstractions.IValidatableUmbrellaOptions" />
	/// <seealso cref="Umbrella.Utilities.Options.Abstractions.ISanitizableUmbrellaOptions" />
	public class UmbrellaFileProviderMiddlewareMapping : IValidatableUmbrellaOptions, ISanitizableUmbrellaOptions
	{
		/// <summary>
		/// Gets or sets the cacheability.
		/// </summary>
		public MiddlewareHttpCacheability Cacheability { get; set; }

		/// <summary>
		/// Gets or sets the file provider mapping.
		/// </summary>
		public UmbrellaFileProviderMapping FileProviderMapping { get; set; }

		/// <summary>
		/// Sanitizes this instance.
		/// </summary>
		public void Sanitize() => FileProviderMapping?.Sanitize();

		/// <summary>
		/// Validates this instance.
		/// </summary>
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
		}
	}
}