using System.Buffers;
using System.Net.Http;
using CommunityToolkit.Diagnostics;

namespace Umbrella.Utilities.Http;

/// <summary>
/// Streams a single request body and reports cumulative bytes written to the HTTP transport.
/// </summary>
/// <remarks>
/// Reports do not acknowledge receipt by the server. In browser applications, enable request
/// streaming on the request when supported; otherwise the browser HTTP handler may buffer the
/// entire content before sending it. This content can only be serialized once. Retries require
/// a new content instance and a source positioned at the beginning of the data to resend.
/// </remarks>
/// <example>
/// <code>
/// using var request = new HttpRequestMessage(HttpMethod.Post, "api/FileUpload")
/// {
///     Content = new ProgressStreamContent(fileStream, uploadProgress, leaveOpen: true,
///         cancellationToken: cancellationToken)
/// };
/// // In a .NET 9+ Blazor WebAssembly app with the WebAssembly HTTP extensions referenced:
/// // request.SetBrowserRequestStreamingEnabled(true);
/// using var response = await httpClient.SendAsync(request, cancellationToken);
/// </code>
/// </example>
public sealed class ProgressStreamContent : HttpContent
{
	private const int BufferSize = 64 * 1024;
	private readonly Stream _source;
	private readonly IProgress<long>? _progress;
	private readonly long? _contentLength;
	private readonly bool _leaveOpen;
	private readonly CancellationToken _cancellationToken;
	private int _serializationStarted;
	private int _disposed;

	/// <summary>
	/// Creates content from the source's current position without buffering it.
	/// </summary>
	/// <param name="source">A readable stream containing the request body.</param>
	/// <param name="progress">Receives zero, followed by cumulative bytes after successful writes.</param>
	/// <param name="contentLength">The remaining byte count, or null to infer it for a seekable source. Non-seekable sources otherwise have unknown length.</param>
	/// <param name="leaveOpen">Whether disposing this content leaves the source open.</param>
	/// <param name="cancellationToken">Cancels copying, including on targets without a serialization cancellation-token overload.</param>
	/// <exception cref="ArgumentNullException"><paramref name="source"/> is null.</exception>
	/// <exception cref="ArgumentException">The source cannot be read or its position exceeds its length.</exception>
	/// <exception cref="ArgumentOutOfRangeException"><paramref name="contentLength"/> is negative.</exception>
	public ProgressStreamContent(Stream source, IProgress<long>? progress = null, long? contentLength = null, bool leaveOpen = false, CancellationToken cancellationToken = default)
	{
		Guard.IsNotNull(source);
		if (!source.CanRead)
			throw new ArgumentException("The source must be readable.", nameof(source));
		if (contentLength < 0)
			throw new ArgumentOutOfRangeException(nameof(contentLength));

		_contentLength = contentLength ?? (source.CanSeek ? source.Length - source.Position : null);
		if (_contentLength < 0)
			throw new ArgumentException("The source position must not exceed its length.", nameof(source));

		_source = source;
		_progress = progress;
		_leaveOpen = leaveOpen;
		_cancellationToken = cancellationToken;
	}

	/// <inheritdoc />
	protected override bool TryComputeLength(out long length)
	{
		length = _contentLength.GetValueOrDefault();
		return _contentLength.HasValue;
	}

	/// <inheritdoc />
	protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context)
	{
		Guard.IsNotNull(stream);
		return CopyToTransportAsync(stream, CancellationToken.None);
	}

#if NET8_0_OR_GREATER
	/// <inheritdoc />
	protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context, CancellationToken cancellationToken)
	{
		Guard.IsNotNull(stream);
		return CopyToTransportAsync(stream, cancellationToken);
	}
#endif

	private async Task CopyToTransportAsync(Stream destination, CancellationToken cancellationToken)
	{
		if (Volatile.Read(ref _disposed) != 0)
			throw new ObjectDisposedException(nameof(ProgressStreamContent));
		if (Interlocked.Exchange(ref _serializationStarted, 1) != 0)
			throw new InvalidOperationException("This content has already been serialized. Create new content and a fresh source for each upload attempt.");

		using var linkedCancellation = CancellationTokenSource.CreateLinkedTokenSource(_cancellationToken, cancellationToken);
		CancellationToken token = linkedCancellation.Token;
		token.ThrowIfCancellationRequested();
		_progress?.Report(0);
		byte[] buffer = ArrayPool<byte>.Shared.Rent(BufferSize);
		try
		{
			long total = 0;
			while (true)
			{
				token.ThrowIfCancellationRequested();
#if NET8_0_OR_GREATER
				int read = await _source.ReadAsync(buffer.AsMemory(0, BufferSize), token).ConfigureAwait(false);
#else
				int read = await _source.ReadAsync(buffer, 0, BufferSize, token).ConfigureAwait(false);
#endif
				if (read == 0)
					break;
				token.ThrowIfCancellationRequested();
#if NET8_0_OR_GREATER
				await destination.WriteAsync(buffer.AsMemory(0, read), token).ConfigureAwait(false);
#else
				await destination.WriteAsync(buffer, 0, read, token).ConfigureAwait(false);
#endif
				total += read;
				token.ThrowIfCancellationRequested();
				_progress?.Report(total);
			}
		}
		finally
		{
			ArrayPool<byte>.Shared.Return(buffer);
		}
	}

	/// <inheritdoc />
	protected override void Dispose(bool disposing)
	{
		try
		{
			if (disposing && Interlocked.Exchange(ref _disposed, 1) == 0 && !_leaveOpen)
				_source.Dispose();
		}
		finally
		{
			base.Dispose(disposing);
		}
	}
}
