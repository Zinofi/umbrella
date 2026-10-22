using CommunityToolkit.Diagnostics;

namespace Umbrella.FileSystem.SharePoint;

/// <summary>Maps a logical metadata key to an existing or explicitly provisioned SharePoint column.</summary>
public sealed record SharePointMetadataColumnMapping
{
	/// <summary>The internal, API-facing column name, not its display name.</summary>
	public string ColumnName { get; init; } = null!;

	/// <summary>The column type. Defaults to single-line text.</summary>
	public SharePointMetadataColumnType ColumnType { get; init; }

	/// <summary>An optional display name used only when creating the column.</summary>
	public string? DisplayName { get; init; }

	/// <summary>The allowed, case-sensitive values for a single-choice column.</summary>
	public IReadOnlyList<string> Choices { get; init; } = Array.Empty<string>();

	/// <summary>Validates this column definition without accessing SharePoint.</summary>
	public void Validate()
	{
		Guard.IsNotNullOrWhiteSpace(ColumnName);
		if (ColumnName != ColumnName.Trim())
			throw new ArgumentException("Column names must not contain leading or trailing whitespace.", nameof(ColumnName));
		if (!Enum.IsDefined(ColumnType))
			throw new ArgumentOutOfRangeException(nameof(ColumnType));
		if (DisplayName is not null)
			Guard.IsNotNullOrWhiteSpace(DisplayName);
		Guard.IsNotNull(Choices);
		if (ColumnType == SharePointMetadataColumnType.Choice)
		{
			if (Choices.Count == 0 || Choices.Any(string.IsNullOrWhiteSpace) || Choices.Distinct(StringComparer.Ordinal).Count() != Choices.Count)
				throw new ArgumentException("Choice columns require non-empty, distinct choice values.", nameof(Choices));
		}
		else if (Choices.Count != 0)
		{
			throw new ArgumentException("Only choice columns can specify choices.", nameof(Choices));
		}
	}

	internal static Dictionary<string, SharePointMetadataColumnMapping> Snapshot(IDictionary<string, SharePointMetadataColumnMapping> mappings)
	{
		Guard.IsNotNull(mappings);
		var result = new Dictionary<string, SharePointMetadataColumnMapping>(StringComparer.OrdinalIgnoreCase);
		var columns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		foreach (var (key, mapping) in mappings)
		{
			Guard.IsNotNullOrWhiteSpace(key);
			Guard.IsNotNull(mapping);
			mapping.Validate();
			if (!columns.Add(mapping.ColumnName))
				throw new ArgumentException($"Multiple metadata keys map to column '{mapping.ColumnName}'.", nameof(mappings));
			result.Add(key, mapping with { Choices = mapping.Choices.ToArray() });
		}

		return result;
	}
}
