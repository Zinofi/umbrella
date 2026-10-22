using System.Globalization;
using System.Text.Json;
using CommunityToolkit.Diagnostics;
using Microsoft.Graph;
using Microsoft.Graph.Models;
using Microsoft.Graph.Models.ODataErrors;
using Microsoft.Kiota.Abstractions.Serialization;
using Umbrella.FileSystem.Abstractions;
using Umbrella.Utilities.TypeConverters.Abstractions;

namespace Umbrella.FileSystem.SharePoint;

/// <summary>Stores file metadata in configured SharePoint library columns. Does not create or modify column definitions.</summary>
public sealed class UmbrellaSharePointFileMetadataProvider : IUmbrellaFileMetadataProvider
{
	private readonly string _siteId;
	private readonly Dictionary<string, SharePointMetadataColumnMapping> _mappings;
	private readonly IGenericTypeConverter _converter;

	/// <summary>Creates a backend bound to the configured site and a snapshot of its column mappings.</summary>
	/// <param name="options">The SharePoint storage options.</param>
	/// <param name="converter">The converter used by metadata getters.</param>
	public UmbrellaSharePointFileMetadataProvider(UmbrellaSharePointFileStorageProviderOptions options, IGenericTypeConverter converter)
	{
		Guard.IsNotNull(options);
		Guard.IsNotNullOrWhiteSpace(options.SiteId);
		Guard.IsNotNull(converter);
		_siteId = options.SiteId;
		_mappings = SharePointMetadataColumnMapping.Snapshot(options.MetadataColumnMappings);
		_converter = new MetadataValueConverter(converter);
	}

	/// <inheritdoc />
	public IUmbrellaFileMetadataSession CreateSession(UmbrellaFileMetadataContext context)
	{
		Guard.IsNotNull(context);
		if (context.FileInfo is not UmbrellaSharePointFileInfo file)
			throw new ArgumentException("SharePoint column metadata requires a SharePoint file.", nameof(context));
		return new Session(file, _siteId, _mappings, _converter);
	}

	/// <inheritdoc />
	public Task DeleteFileAsync(UmbrellaFileMetadataContext context, CancellationToken cancellationToken = default)
	{
		cancellationToken.ThrowIfCancellationRequested();
		return Task.CompletedTask; // The fields belong to the deleted item.
	}

	/// <inheritdoc />
	public Task DeleteDirectoryAsync(string? storageNamespace, string subPath, CancellationToken cancellationToken = default)
	{
		cancellationToken.ThrowIfCancellationRequested();
		return Task.CompletedTask;
	}

	// The shared converter uses CurrentCulture by default. Graph values use invariant
	// numbers and ISO dates, including when callers read them under a different culture.
	private sealed class MetadataValueConverter(IGenericTypeConverter converter) : IGenericTypeConverter
	{
		public T Convert<T>(string? value, Func<T> fallbackCreator, Func<string?, T>? customValueConverter = null)
			=> converter.Convert(value, fallbackCreator, customValueConverter ?? GetInvariantConverter<T>());

		public T Convert<T>(string? value, T fallback = default!, Func<string?, T>? customValueConverter = null)
			=> converter.Convert(value, fallback, customValueConverter ?? GetInvariantConverter<T>());

		public T ConvertToEnum<T>(string? value, T fallback = default) where T : struct, Enum
			=> converter.ConvertToEnum(value, fallback);

		private static Func<string?, T>? GetInvariantConverter<T>()
		{
			Type type = Nullable.GetUnderlyingType(typeof(T)) ?? typeof(T);
			if (type == typeof(DateTimeOffset))
				return value => (T)(object)DateTimeOffset.Parse(value!, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal);
			if (type == typeof(DateTime))
				return value => (T)(object)DateTime.Parse(value!, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
			return Type.GetTypeCode(type) is TypeCode.Byte or TypeCode.SByte or TypeCode.Int16 or TypeCode.UInt16
				or TypeCode.Int32 or TypeCode.UInt32 or TypeCode.Int64 or TypeCode.UInt64 or TypeCode.Single or TypeCode.Double or TypeCode.Decimal
				? value => (T)System.Convert.ChangeType(value, type, CultureInfo.InvariantCulture)!
				: null;
		}
	}

	private sealed class Session(UmbrellaSharePointFileInfo file, string siteId, Dictionary<string, SharePointMetadataColumnMapping> mappings, IGenericTypeConverter converter)
		: UmbrellaFileMetadataSession(converter, StringComparer.OrdinalIgnoreCase)
	{
		private async Task<(string ListId, string ItemId)> ResolveItemAsync(CancellationToken cancellationToken)
		{
			var item = await file.MetadataGraphClient.Drives[file.MetadataDriveId].Root.ItemWithPath(file.MetadataRelativePath)
				.GetAsync(config => config.QueryParameters.Select = ["sharepointIds"], cancellationToken).ConfigureAwait(false);
			var ids = item?.SharepointIds;
			if (string.IsNullOrWhiteSpace(ids?.ListId) || string.IsNullOrWhiteSpace(ids.ListItemId))
				throw new InvalidOperationException($"SharePoint list item identity is unavailable for '{file.SubPath}'.");
			return (ids.ListId, ids.ListItemId);
		}

		protected override async Task<Dictionary<string, object?>> ReadCoreAsync(CancellationToken cancellationToken)
		{
			var values = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
			// IsNew is permitted only for authorization during missing-file deletion by the manager.
			if (mappings.Count == 0 || file.IsNew)
				return values;
			var (listId, itemId) = await ResolveItemAsync(cancellationToken).ConfigureAwait(false);
			// Kiota's AdditionalData parser infers dates from strings, even for text columns.
			// Keep the SDK transport/authentication/error handling, but preserve JSON scalar types.
			var request = file.MetadataGraphClient.Sites[siteId].Lists[listId].Items[itemId].Fields
				.ToGetRequestInformation(config => config.QueryParameters.Select = mappings.Values.Select(x => x.ColumnName).ToArray());
			var errors = new Dictionary<string, ParsableFactory<IParsable>>
			{
				["4XX"] = ODataError.CreateFromDiscriminatorValue,
				["5XX"] = ODataError.CreateFromDiscriminatorValue
			};
			using var stream = await file.MetadataGraphClient.RequestAdapter.SendPrimitiveAsync<Stream>(request, errors, cancellationToken).ConfigureAwait(false)
				?? throw new InvalidOperationException("SharePoint returned no field values.");
			using var fields = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken).ConfigureAwait(false);
			foreach (var (key, mapping) in mappings)
			{
				if (fields.RootElement.TryGetProperty(mapping.ColumnName, out var value))
				{
					values[key] = value.ValueKind switch
					{
						JsonValueKind.Null => null,
						JsonValueKind.String => value.GetString(),
						JsonValueKind.True => true,
						JsonValueKind.False => false,
						JsonValueKind.Number => value.GetDouble(),
						_ => throw new InvalidOperationException($"SharePoint column '{mapping.ColumnName}' returned a non-scalar metadata value.")
					};
				}
			}

			return values;
		}

		protected override async Task WriteCoreAsync(IReadOnlyDictionary<string, object?> changes, bool clear, CancellationToken cancellationToken)
		{
			var data = new Dictionary<string, object>();
			if (clear)
			{
				foreach (var mapping in mappings.Values)
					data[mapping.ColumnName] = null!;
			}

			foreach (var (key, value) in changes)
				data[mappings[key].ColumnName] = value!;
			if (data.Count == 0)
				return;
			var (listId, itemId) = await ResolveItemAsync(cancellationToken).ConfigureAwait(false);
			_ = await file.MetadataGraphClient.Sites[siteId].Lists[listId].Items[itemId].Fields
				.PatchAsync(new FieldValueSet { AdditionalData = data }, cancellationToken: cancellationToken).ConfigureAwait(false);
		}

		protected override bool TryConvertValue(string key, object? value, out object? converted)
		{
			if (!mappings.TryGetValue(key, out var mapping))
				throw new ArgumentException($"Metadata key '{key}' has no configured SharePoint column mapping.", nameof(key));
			converted = value is null ? null : mapping.ColumnType switch
			{
				SharePointMetadataColumnType.Boolean => Convert.ToBoolean(value, CultureInfo.InvariantCulture),
				SharePointMetadataColumnType.Number => ConvertNumber(value),
				SharePointMetadataColumnType.DateTime => ConvertDateTime(value),
				_ => Convert.ToString(value, CultureInfo.InvariantCulture)
			};
			if (mapping.ColumnType == SharePointMetadataColumnType.Choice && converted is string choice && !mapping.Choices.Contains(choice, StringComparer.Ordinal))
				throw new ArgumentException($"'{choice}' is not an allowed choice for metadata key '{key}'.", nameof(value));
			return true;
		}

		protected override string? ConvertToString(object? value) => value switch
		{
			DateTime or DateTimeOffset => ConvertDateTime(value),
			_ => Convert.ToString(value, CultureInfo.InvariantCulture)
		};

		private static double ConvertNumber(object value)
		{
			double number = Convert.ToDouble(value, CultureInfo.InvariantCulture);
			return double.IsFinite(number) ? number : throw new ArgumentException("SharePoint numeric metadata must be finite.", nameof(value));
		}

		private static string ConvertDateTime(object value)
		{
			DateTimeOffset date = value switch
			{
				DateTimeOffset offset => offset,
				DateTime time => new DateTimeOffset(time.Kind == DateTimeKind.Unspecified ? DateTime.SpecifyKind(time, DateTimeKind.Utc) : time),
				_ => DateTimeOffset.Parse(Convert.ToString(value, CultureInfo.InvariantCulture)!, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal)
			};
			return date.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture);
		}
	}
}
