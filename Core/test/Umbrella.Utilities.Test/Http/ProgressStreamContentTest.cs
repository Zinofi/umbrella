using System.Net.Http;
using Umbrella.Utilities.Http;

namespace Umbrella.Utilities.Test.Http;

public class ProgressStreamContentTest
{
	[Fact]
	public void Constructor_RejectsInvalidArguments()
	{
		_ = Assert.Throws<ArgumentNullException>(() => new ProgressStreamContent(null!));
		using var unreadable = new MemoryStream();
		unreadable.Dispose();
		_ = Assert.Throws<ArgumentException>(() => new ProgressStreamContent(unreadable));
		using var source = new MemoryStream([1]);
		_ = Assert.Throws<ArgumentOutOfRangeException>(() => new ProgressStreamContent(source, contentLength: -1));
		source.Position = 2;
		_ = Assert.Throws<ArgumentException>(() => new ProgressStreamContent(source));
	}

	[Fact]
	public async Task PartialReads_ReportCumulativeBytesAndPreserveContent()
	{
		byte[] bytes = Enumerable.Range(0, 12).Select(x => (byte)x).ToArray();
		using var source = new ChunkedSource(bytes, 3);
		using var target = new MemoryStream();
		var progress = new RecordingProgress();
		using var content = new ProgressStreamContent(source, progress);
		Assert.Null(content.Headers.ContentLength);

		await content.CopyToAsync(target, TestContext.Current.CancellationToken);

		Assert.Equal(bytes, target.ToArray());
		Assert.Equal(new long[] { 0, 3, 6, 9, 12 }, progress.Values);
		Assert.True(target.CanWrite);
	}

	[Fact]
	public async Task SeekableSource_UsesRemainingLengthAndStartsCountAtZero()
	{
		using var source = new MemoryStream([1, 2, 3, 4]);
		source.Position = 2;
		using var target = new MemoryStream();
		var progress = new RecordingProgress();
		using var content = new ProgressStreamContent(source, progress);
		Assert.Equal(2, content.Headers.ContentLength);
		await content.CopyToAsync(target, TestContext.Current.CancellationToken);
		Assert.Equal(new byte[] { 3, 4 }, target.ToArray());
		Assert.Equal(new long[] { 0, 2 }, progress.Values);
	}

	[Fact]
	public void ExplicitLength_DoesNotReadStreamMetadata()
	{
		using var source = new ChunkedSource([1, 2, 3], 1);
		using var content = new ProgressStreamContent(source, contentLength: 3);
		Assert.Equal(3, content.Headers.ContentLength);
	}

	[Fact]
	public async Task EmptySource_ReportsOnlyZero()
	{
		using var source = new MemoryStream();
		using var target = new MemoryStream();
		var progress = new RecordingProgress();
		using var content = new ProgressStreamContent(source, progress);
		Assert.Equal(0, content.Headers.ContentLength);
		await content.CopyToAsync(target, TestContext.Current.CancellationToken);
		Assert.Equal(new long[] { 0 }, progress.Values);
	}

	[Fact]
	public async Task LargeSource_CopiesAcrossBufferBoundariesWithoutReporter()
	{
		byte[] bytes = new byte[150_000];
		new Random(42).NextBytes(bytes);
		using var source = new MemoryStream(bytes);
		using var target = new MemoryStream();
		using var content = new ProgressStreamContent(source);
		await content.CopyToAsync(target, TestContext.Current.CancellationToken);
		Assert.Equal(bytes, target.ToArray());
	}

	[Fact]
	public async Task PendingWrite_DoesNotAdvanceProgress_AndRejectsConcurrentCopy()
	{
		using var source = new MemoryStream([1, 2, 3]);
		using var target = new ControlledDestination();
		var progress = new RecordingProgress();
		using var content = new ProgressStreamContent(source, progress);
		Task copy = content.CopyToAsync(target, TestContext.Current.CancellationToken);
		await target.Entered.Task.WaitAsync(TestContext.Current.CancellationToken);
		Assert.Equal(new long[] { 0 }, progress.Values);
		_ = await Assert.ThrowsAsync<InvalidOperationException>(() => content.CopyToAsync(Stream.Null, TestContext.Current.CancellationToken));
		target.Release.SetResult();
		await copy;
		Assert.Equal(new long[] { 0, 3 }, progress.Values);
		_ = await Assert.ThrowsAsync<InvalidOperationException>(() => content.CopyToAsync(Stream.Null, TestContext.Current.CancellationToken));
	}

	[Theory]
	[InlineData(true)]
	[InlineData(false)]
	public async Task PendingWrite_CanBeCancelledByEitherToken(bool cancelConstructorToken)
	{
		using var source = new MemoryStream([1, 2, 3]);
		using var target = new ControlledDestination();
		using var constructorCancellation = new CancellationTokenSource();
		using var serializationCancellation = new CancellationTokenSource();
		var progress = new RecordingProgress();
		using var content = new ProgressStreamContent(source, progress, cancellationToken: constructorCancellation.Token);
		Task copy = content.CopyToAsync(target, serializationCancellation.Token);
		await target.Entered.Task.WaitAsync(TestContext.Current.CancellationToken);
		await (cancelConstructorToken ? constructorCancellation : serializationCancellation).CancelAsync();
		_ = await Assert.ThrowsAnyAsync<OperationCanceledException>(() => copy);
		Assert.Equal(new long[] { 0 }, progress.Values);
	}

	[Fact]
	public async Task PendingRead_ReceivesCancellation()
	{
		using var source = new PendingSource();
		using var cancellation = new CancellationTokenSource();
		using var content = new ProgressStreamContent(source, cancellationToken: cancellation.Token);
		Task copy = content.CopyToAsync(Stream.Null, TestContext.Current.CancellationToken);
		await source.Entered.Task.WaitAsync(TestContext.Current.CancellationToken);
		await cancellation.CancelAsync();
		_ = await Assert.ThrowsAnyAsync<OperationCanceledException>(() => copy);
	}

	[Fact]
	public async Task AlreadyCancelled_DoesNotReadOrReport()
	{
		using var source = new MemoryStream([1]);
		var progress = new RecordingProgress();
		using var content = new ProgressStreamContent(source, progress, cancellationToken: new CancellationToken(true));
		_ = await Assert.ThrowsAnyAsync<OperationCanceledException>(() => content.CopyToAsync(Stream.Null, TestContext.Current.CancellationToken));
		Assert.Empty(progress.Values);
		Assert.Equal(0, source.Position);
	}

	[Fact]
	public async Task FailedWrite_DoesNotReportUnwrittenBytes_AndCannotBeRetried()
	{
		using var source = new ChunkedSource([1, 2, 3, 4], 2);
		using var target = new ControlledDestination(failOnWrite: 2);
		target.Release.SetResult();
		var progress = new RecordingProgress();
		using var content = new ProgressStreamContent(source, progress);
		var error = await Assert.ThrowsAsync<HttpRequestException>(() => content.CopyToAsync(target, TestContext.Current.CancellationToken));
		_ = Assert.IsType<IOException>(error.InnerException);
		Assert.Equal(new long[] { 0, 2 }, progress.Values);
		_ = await Assert.ThrowsAsync<InvalidOperationException>(() => content.CopyToAsync(Stream.Null, TestContext.Current.CancellationToken));
	}

	[Fact]
	public async Task FailedRead_PropagatesWithoutReportingBytes()
	{
		using var source = new PendingSource(fail: true);
		var progress = new RecordingProgress();
		using var content = new ProgressStreamContent(source, progress);
		var error = await Assert.ThrowsAsync<HttpRequestException>(() => content.CopyToAsync(Stream.Null, TestContext.Current.CancellationToken));
		_ = Assert.IsType<IOException>(error.InnerException);
		Assert.Equal(new long[] { 0 }, progress.Values);
	}

	[Theory]
	[InlineData(true)]
	[InlineData(false)]
	public async Task Disposal_HonoursOwnershipAfterCopy(bool leaveOpen)
	{
		using var source = new MemoryStream([1]);
		var content = new ProgressStreamContent(source, leaveOpen: leaveOpen);
		await content.CopyToAsync(Stream.Null, TestContext.Current.CancellationToken);
		content.Dispose();
		content.Dispose();
		Assert.Equal(leaveOpen, source.CanRead);
		_ = await Assert.ThrowsAsync<ObjectDisposedException>(() => content.CopyToAsync(Stream.Null, TestContext.Current.CancellationToken));
	}

	[Fact]
	public async Task HttpClient_SendsContentAndReportsProgress()
	{
		using var handler = new RecordingHandler();
		using var client = new HttpClient(handler);
		using var source = new ChunkedSource([1, 2, 3, 4], 2);
		var progress = new RecordingProgress();
		using (var request = new HttpRequestMessage(HttpMethod.Post, "https://example.test/upload")
		{
			Content = new ProgressStreamContent(source, progress, leaveOpen: true)
		})
		using (var response = await client.SendAsync(request, TestContext.Current.CancellationToken))
		{
			Assert.Equal(HttpStatusCode.Created, response.StatusCode);
		}

		Assert.Equal(new byte[] { 1, 2, 3, 4 }, handler.Body);
		Assert.Equal(new long[] { 0, 2, 4 }, progress.Values);
		Assert.True(source.CanRead);
	}

	private sealed class RecordingProgress : IProgress<long>
	{
		public List<long> Values { get; } = [];
		public void Report(long value) => Values.Add(value);
	}

	private class ChunkedSource(byte[] bytes, int chunkSize) : Stream
	{
		private readonly MemoryStream _inner = new(bytes);
		public override bool CanRead => _inner.CanRead;
		public override bool CanSeek => false;
		public override bool CanWrite => false;
		public override long Length => throw new NotSupportedException();
		public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
		public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
		public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
			=> _inner.ReadAsync(buffer[..Math.Min(buffer.Length, chunkSize)], cancellationToken);
		public override void Flush() => throw new NotSupportedException();
		public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
		public override void SetLength(long value) => throw new NotSupportedException();
		public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
		protected override void Dispose(bool disposing)
		{
			if (disposing)
				_inner.Dispose();

			base.Dispose(disposing);
		}
	}

	private sealed class PendingSource(bool fail = false) : ChunkedSource([], 1)
	{
		public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
		public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
		{
			_ = Entered.TrySetResult();
			if (fail)
				throw new IOException("Read failed.");

			await Task.Delay(Timeout.Infinite, cancellationToken);
			return 0;
		}
	}

	private sealed class ControlledDestination(int failOnWrite = 0) : MemoryStream
	{
		private int _writes;
		public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
		public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
		public override async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
		{
			_ = Entered.TrySetResult();
			await Release.Task.WaitAsync(cancellationToken);
			if (++_writes == failOnWrite)
				throw new IOException("Write failed.");

			await base.WriteAsync(buffer, cancellationToken);
		}
	}

	private sealed class RecordingHandler : HttpMessageHandler
	{
		public byte[]? Body { get; private set; }
		protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
		{
			using var target = new MemoryStream();
			await request.Content!.CopyToAsync(target, cancellationToken);
			Body = target.ToArray();
			return new HttpResponseMessage(HttpStatusCode.Created);
		}
	}
}
