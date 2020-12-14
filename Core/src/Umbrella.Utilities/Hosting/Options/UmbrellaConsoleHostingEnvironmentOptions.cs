﻿using System;
using Umbrella.Utilities.Options.Abstractions;

namespace Umbrella.Utilities.Hosting.Options
{
	/// <summary>
	/// Options for the <see cref="UmbrellaHostingEnvironment"/> and derived types.
	/// </summary>
	/// <seealso cref="Umbrella.Utilities.Hosting.Options.UmbrellaHostingEnvironmentOptions" />
	public class UmbrellaConsoleHostingEnvironmentOptions : UmbrellaHostingEnvironmentOptions, ISanitizableUmbrellaOptions, IValidatableUmbrellaOptions
	{
		/// <summary>
		/// Gets or sets the base directory. Defaults to <see cref="AppContext.BaseDirectory" />.
		/// </summary>
		public string BaseDirectory { get; set; } = AppContext.BaseDirectory;

		/// <inheritdoc />
		public void Sanitize()
		{
			BaseDirectory = BaseDirectory?.Trim()!;
		}

		/// <inheritdoc />
		public void Validate()
		{
			Guard.ArgumentNotNullOrEmpty(BaseDirectory, nameof(BaseDirectory));
		}
	}
}