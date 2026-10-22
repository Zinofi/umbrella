namespace Umbrella.AspNetCore.Blazor.Components.FileUpload;

/// <summary>
/// Specifies the source of file upload progress.
/// </summary>
public enum UmbrellaFileUploadProgressMode
{
	/// <summary>
	/// Measures bytes read from the source stream. This does not necessarily represent bytes sent over the network.
	/// </summary>
	StreamRead,

	/// <summary>
	/// Measures cumulative file bytes reported by the upload handler through
	/// <see cref="UmbrellaFileUploadRequestEventArgs.UploadProgress"/>.
	/// </summary>
	HandlerReported
}
