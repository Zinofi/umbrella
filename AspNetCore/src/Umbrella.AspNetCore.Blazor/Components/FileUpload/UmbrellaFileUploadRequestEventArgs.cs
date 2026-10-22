
using System.Runtime.InteropServices;

namespace Umbrella.AspNetCore.Blazor.Components.FileUpload;

/// <summary>
/// The event arguments for file upload events raised by the <see cref="UmbrellaFileUpload"/> component.
/// </summary>
[StructLayout(LayoutKind.Auto)]
public readonly record struct UmbrellaFileUploadRequestEventArgs
{
	// Wrappers must wait until the uploader has accepted success before rendering away its child.
	internal Action<Action>? RegisterSuccessCallback { get; init; }

	/// <summary>
	/// Initializes a new instance of the <see cref="UmbrellaFileUploadRequestEventArgs"/> struct.
	/// </summary>
	/// <param name="content">The content.</param>
	/// <param name="fileName">Name of the file.</param>
	/// <param name="type">The type.</param>
	/// <param name="uploadCancellationToken">The upload cancellation token.</param>
	public UmbrellaFileUploadRequestEventArgs(Stream content, string fileName, string type, CancellationToken uploadCancellationToken)
		: this(content, fileName, type, uploadCancellationToken, null)
	{
	}

	/// <summary>
	/// Initializes file upload arguments with an optional transport progress reporter.
	/// </summary>
	/// <param name="content">The content.</param>
	/// <param name="fileName">Name of the file.</param>
	/// <param name="type">The MIME type.</param>
	/// <param name="uploadCancellationToken">The upload cancellation token.</param>
	/// <param name="uploadProgress">The cumulative uploaded file byte reporter, or <see langword="null"/> for stream-read progress.</param>
	public UmbrellaFileUploadRequestEventArgs(Stream content, string fileName, string type, CancellationToken uploadCancellationToken, IProgress<long>? uploadProgress)
	{
		Content = content;
		FileName = fileName;
		Type = type;
		UploadCancellationToken = uploadCancellationToken;
		UploadProgress = uploadProgress;
	}

	/// <summary>
	/// Gets the content.
	/// </summary>
	public Stream Content { get; }

	/// <summary>
	/// Gets the name of the file.
	/// </summary>
	public string FileName { get; }

	/// <summary>
	/// Gets the MIME type of the file.
	/// </summary>
	public string Type { get; }

	/// <summary>
	/// Gets the upload cancellation token.
	/// </summary>
	public CancellationToken UploadCancellationToken { get; }

	/// <summary>
	/// Gets the reporter for cumulative uploaded file bytes when handler-reported progress is enabled.
	/// </summary>
	/// <remarks>
	/// Report bytes from transport progress notifications, not from reading or buffering <see cref="Content"/>.
	/// Values represent cumulative original-file bytes for this attempt, excluding multipart overhead,
	/// measured against the selected file size. Negative, excessive, decreasing and late reports are ignored or clamped.
	/// A report of 100% does not mark the operation successful; the handler must still return a successful result.
	/// Without reports the indicator remains at zero until successful completion. This property is
	/// <see langword="null"/> in stream-read mode and when using the original four-argument constructor.
	/// </remarks>
	/// <example>
	/// Forward the transport's cumulative file-byte notification from inside the upload handler:
	/// <code>
	/// void OnTransportProgress(long cumulativeFileBytesSent)
	/// {
	///     args.UploadProgress?.Report(cumulativeFileBytesSent);
	/// }
	/// </code>
	/// </example>
	public IProgress<long>? UploadProgress { get; }
}
