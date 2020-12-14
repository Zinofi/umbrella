﻿using System;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Umbrella.Utilities.Configuration.Abstractions;
using Umbrella.Utilities.TypeConverters.Abstractions;

namespace Umbrella.Utilities.Configuration
{
	/// <summary>
	/// The base class for an AppSettings class that contains property definitions for settings that are read from the appSettings section
	/// of the application config file, e.g. app.config, web.config.
	/// </summary>
	/// <seealso cref="ReadOnlyAppSettingsBase{IReadOnlyAppSettingsSource}"/>
	public abstract class ReadOnlyAppSettingsBase : ReadOnlyAppSettingsBase<IReadOnlyAppSettingsSource>
	{
		#region Constructors		
		/// <summary>
		/// Initializes a new instance of the <see cref="ReadOnlyAppSettingsBase"/> class.
		/// </summary>
		/// <param name="logger">The logger.</param>
		/// <param name="cache">The cache.</param>
		/// <param name="appSettingsSource">The application settings source.</param>
		/// <param name="genericTypeConverter">The generic type converter.</param>
		public ReadOnlyAppSettingsBase(ILogger logger,
			IMemoryCache cache,
			IReadOnlyAppSettingsSource appSettingsSource,
			IGenericTypeConverter genericTypeConverter)
			: base(logger, cache, appSettingsSource, genericTypeConverter)
		{
		}
		#endregion
	}

	/// <summary>
	/// The base class for an AppSettings class that contains property definitions for settings that are read from
	/// </summary>
	/// <typeparam name="TAppSettingsSource">The type of the application settings source.</typeparam>
	/// <seealso cref="IReadOnlyAppSettingsSource"/>
	public abstract class ReadOnlyAppSettingsBase<TAppSettingsSource>
		where TAppSettingsSource : IReadOnlyAppSettingsSource
	{
		#region Private Static Members
		private static readonly MemoryCacheEntryOptions s_DefaultMemoryCacheEntryOptions = new MemoryCacheEntryOptions();
		private static readonly string s_CacheKeyPrefix = typeof(ReadOnlyAppSettingsBase<TAppSettingsSource>).FullName;
		#endregion

		#region Protected Properties		
		/// <summary>
		/// Gets the log.
		/// </summary>
		protected ILogger Log { get; }

		/// <summary>
		/// Gets the cache.
		/// </summary>
		protected IMemoryCache Cache { get; }

		/// <summary>
		/// Gets the application settings source.
		/// </summary>
		protected TAppSettingsSource AppSettingsSource { get; }

		/// <summary>
		/// Gets the generic type converter.
		/// </summary>
		protected IGenericTypeConverter GenericTypeConverter { get; }
		#endregion

		#region Constructors		
		/// <summary>
		/// Initializes a new instance of the <see cref="ReadOnlyAppSettingsBase{TAppSettingsSource}"/> class.
		/// </summary>
		/// <param name="logger">The logger.</param>
		/// <param name="cache">The cache.</param>
		/// <param name="appSettingsSource">The application settings source.</param>
		/// <param name="genericTypeConverter">The generic type converter.</param>
		public ReadOnlyAppSettingsBase(ILogger logger,
			IMemoryCache cache,
			TAppSettingsSource appSettingsSource,
			IGenericTypeConverter genericTypeConverter)
		{
			Log = logger;
			Cache = cache;
			AppSettingsSource = appSettingsSource;
			GenericTypeConverter = genericTypeConverter;
		}
		#endregion

		#region Protected Methods
		// Move to Options class		
		/// <summary>
		/// Generates the cache key.
		/// </summary>
		/// <param name="settingKey">The setting key.</param>
		/// <returns>The cache key.</returns>
		protected virtual string GenerateCacheKey(string settingKey) => $"{s_CacheKeyPrefix}:{settingKey}";

		/// <summary>
		/// Gets the cache entry options builder. Defaults to <see langword="null" /> unless overridden in a derived type.
		/// </summary>
		/// <returns>The cache entry options builder.</returns>
		protected virtual Func<MemoryCacheEntryOptions>? GetCacheEntryOptionsFunc() => null;
		
		/// <summary>
		/// Converts the specified JSON string to the specified type <typeparamref name="T"/>.
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="value">The value.</param>
		/// <returns>An instance of type <typeparamref name="T"/>.</returns>
		protected virtual T FromJson<T>(string value) => UmbrellaStatics.DeserializeJson<T>(value);

		protected virtual T? GetSetting<T>(T fallback = default, [CallerMemberName]string key = "", bool useCache = true, bool throwException = false, Func<string?, T>? customValueConverter = null)
		{
			Guard.ArgumentNotNullOrWhiteSpace(key, nameof(key));

			try
			{
				T? GetValue()
				{
					string? value = AppSettingsSource.GetValue(key);

					if(string.IsNullOrWhiteSpace(value) && throwException)
						throw new ArgumentException($"The value for key: {key} is not valid. An app setting with that key cannot be found.");

					return GenericTypeConverter.Convert(value, fallback, customValueConverter);
				}

				return useCache
					? Cache.GetOrCreate(GenerateCacheKey(key), entry =>
					{
						entry.SetOptions(GetCacheEntryOptionsFunc()?.Invoke() ?? s_DefaultMemoryCacheEntryOptions);
						return GetValue();
					})
					: GetValue();
			}
			catch (Exception exc) when (Log.WriteError(exc))
			{
				throw;
			}
		}

		protected virtual T? GetSetting<T>(Func<T> fallbackCreator, [CallerMemberName]string key = "", bool useCache = true, bool throwException = false, Func<string?, T>? customValueConverter = null)
		{
			Guard.ArgumentNotNullOrWhiteSpace(key, nameof(key));

			try
			{
				T? GetValue()
				{
					string? value = AppSettingsSource.GetValue(key);

					if (string.IsNullOrWhiteSpace(value) && throwException)
						throw new ArgumentException($"The value for key: {key} is not valid. An app setting with that key cannot be found.");

					return GenericTypeConverter.Convert(value, fallbackCreator, customValueConverter);
				}

				return useCache
					? Cache.GetOrCreate(GenerateCacheKey(key), entry =>
					{
						entry.SetOptions(GetCacheEntryOptionsFunc()?.Invoke() ?? s_DefaultMemoryCacheEntryOptions);
						return GetValue();
					})
					: GetValue();
			}
			catch (Exception exc) when (Log.WriteError(exc))
			{
				throw;
			}
		}

		protected virtual T GetSettingEnum<T>(T fallback = default, [CallerMemberName]string key = "", bool useCache = true, bool throwException = false)
			where T : struct, Enum
		{
			Guard.ArgumentNotNullOrWhiteSpace(key, nameof(key));

			try
			{
				T GetValue()
				{
					string? value = AppSettingsSource.GetValue(key);

					if (string.IsNullOrWhiteSpace(value) && throwException)
						throw new ArgumentException($"The value for key: {key} is not valid. An app setting with that key cannot be found.");

					return GenericTypeConverter.ConvertToEnum(value, fallback);
				}

				return useCache
					? Cache.GetOrCreate(GenerateCacheKey(key), entry =>
					{
						entry.SetOptions(GetCacheEntryOptionsFunc()?.Invoke() ?? s_DefaultMemoryCacheEntryOptions);
						return GetValue();
					})
					: GetValue();
			}
			catch (Exception exc) when (Log.WriteError(exc))
			{
				throw;
			}
		}
		#endregion
	}
}