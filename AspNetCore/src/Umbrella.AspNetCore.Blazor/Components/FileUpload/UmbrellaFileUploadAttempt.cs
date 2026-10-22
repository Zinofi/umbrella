namespace Umbrella.AspNetCore.Blazor.Components.FileUpload;

internal sealed class UmbrellaFileUploadAttempt(long length, UmbrellaFileUploadProgressMode mode) : IProgress<long>, IDisposable
{
#if NET9_0_OR_GREATER
	private readonly Lock _sync = new();
#else
	private readonly object _sync = new();
#endif
	private long _bytesUploaded;
	private bool _stopped;

	internal CancellationTokenSource Cancellation { get; } = new();
	internal CancellationTokenSource RefreshCancellation { get; } = new();
	internal Stream? Stream { get; set; }
	internal Task RefreshTask { get; set; } = Task.CompletedTask;
	internal Action? OnSucceeded { get; set; }

	public void Report(long value)
	{
		lock (_sync)
		{
			if (!_stopped)
				_bytesUploaded = Math.Max(_bytesUploaded, Math.Clamp(value, 0, length));
		}
	}

	internal int GetPercentage()
	{
		lock (_sync)
		{
			long position = mode is UmbrellaFileUploadProgressMode.HandlerReported ? _bytesUploaded : Stream?.Position ?? 0;
			return CalculatePercentage(position, length);
		}
	}

	internal static int CalculatePercentage(long position, long length)
	{
		if (length <= 0 || position <= 0)
			return 0;

		if (position >= length)
			return 100;

		return Math.Min(99, (int)Math.Round(position * 100d / length, MidpointRounding.AwayFromZero));
	}

	internal void StopReporting()
	{
		lock (_sync)
			_stopped = true;

		RefreshCancellation.Cancel();
	}

	internal void Cancel()
	{
		StopReporting();
		Cancellation.Cancel();
	}

	public void Dispose()
	{
		StopReporting();
		RefreshCancellation.Dispose();
		Cancellation.Dispose();
	}
}
