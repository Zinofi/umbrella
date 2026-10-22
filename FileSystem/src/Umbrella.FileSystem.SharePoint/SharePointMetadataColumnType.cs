namespace Umbrella.FileSystem.SharePoint;

/// <summary>The supported SharePoint library column types for file metadata.</summary>
public enum SharePointMetadataColumnType
{
	/// <summary>A single line of text.</summary>
	Text,
	/// <summary>Multiple lines of plain text, replacing rather than appending values.</summary>
	MultilineText,
	/// <summary>A Yes/No value.</summary>
	Boolean,
	/// <summary>A finite number stored using SharePoint's numeric representation.</summary>
	Number,
	/// <summary>A date and time, serialized in UTC.</summary>
	DateTime,
	/// <summary>One of the configured choice strings.</summary>
	Choice
}
