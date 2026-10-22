
using System.Diagnostics.CodeAnalysis;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.Extensions.Logging;
using Umbrella.AspNetCore.Blazor.Components.Dialog.Abstractions;
using Umbrella.Utilities.Primitives.Abstractions;

namespace Umbrella.AspNetCore.Blazor.Components.FileUpload;

/// <summary>
/// A component that can be used to upload files that wraps the built-in <see cref="InputFile"/> component with support for upload progress
/// and providing the user with options to upload or clear the current file selection.
/// </summary>
/// <seealso cref="ComponentBase" />
/// <seealso cref="IDisposable" />
public partial class UmbrellaFileUpload : ComponentBase, IDisposable
{
	[SuppressMessage("Usage", "CA2213:Disposable fields should be disposed", Justification = "UploadClickAsync owns and disposes the attempt in its finally block after the handler and refresh loop finish. Component disposal only cancels it.")]
	private UmbrellaFileUploadAttempt? _uploadAttempt;
	private bool _disposed;

	[Inject]
	private ILogger<UmbrellaFileUpload> Logger { get; set; } = null!;

	[Inject]
	private IUmbrellaDialogService DialogUtility { get; set; } = null!;

	internal int UploadPercentage { get; private set; }
	internal Task ProgressRefreshTask => _uploadAttempt?.RefreshTask ?? Task.CompletedTask;

	/// <summary>
	/// Gets or sets the progress source. Defaults to source-stream reads, which may precede network transmission.
	/// Select <see cref="UmbrellaFileUploadProgressMode.HandlerReported"/> to use transport byte counts
	/// reported by the handler through <see cref="UmbrellaFileUploadRequestEventArgs.UploadProgress"/>.
	/// Changes apply to the next upload attempt.
	/// </summary>
	[Parameter]
	public UmbrellaFileUploadProgressMode ProgressMode { get; set; }

	/// <summary>
	/// Gets or sets the maximum file size in bytes that can be uploaded.
	/// </summary>
	/// <remarks>
	/// Defaults to 512000 bytes.
	/// </remarks>
	[Parameter]
	public int? MaxFileSizeBytes { get; set; } = 512000;

	/// <summary>
	/// Gets or sets whether a warning message should be shown to the user when they clear the current file selection.
	/// </summary>
	[Parameter]
	public bool ShowClearWarning { get; set; } = true;

	/// <summary>
	/// Gets or sets whether a warning message should be shown to the user when they cancel the file upload.
	/// </summary>
	[Parameter]
	public bool ShowCancelWarning { get; set; } = true;

	/// <summary>
	/// Gets or sets a comma-delimited list of file extensions and/or MIME types that this component will accept.
	/// </summary>
	[Parameter]
	public string? Accept { get; set; }

	/// <summary>
	/// Gets or sets the delegate that is invoked when the Upload button is clicked.
	/// </summary>
	[Parameter]
	[EditorRequired]
	public Func<UmbrellaFileUploadRequestEventArgs, Task<IOperationResult?>>? OnRequestUpload { get; set; }

	private string Id { get; } = Guid.NewGuid().ToString();
	private IBrowserFile? SelectedFile { get; set; }
	internal UmbrellaFileUploadStatus Status { get; private set; }
	private string? AcceptTypesMessage { get; set; }
	private int? MaxFileSizeMegaBytes { get; set; }

	/// <inheritdoc />
	protected override async Task OnParametersSetAsync()
	{
		try
		{
			if (MaxFileSizeBytes.HasValue)
				MaxFileSizeMegaBytes = MaxFileSizeBytes / 1024 / 1024;

			if (!string.IsNullOrWhiteSpace(Accept))
			{
				string[] types = Accept.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(x => x.TrimToLowerInvariant()).Distinct().ToArray();

				if (types.Length is 0)
				{
					Accept = null;
					return;
				}

				// NB: The types[..^1] was originally types.SkipLast(1)
				// The types[^1] was originally types[types.Length - 1]. The syntax is using the new C#8 Index stuff. ^1 effectively means get the item at the end.
				AcceptTypesMessage = types.Length is 1
					? $"Please select a {types[0]} file to upload."
					: $"Please select a {string.Join(", ", types[..^1])} or a {types[^1]} file to upload.";
			}
		}
		catch (Exception exc) when (Logger.WriteError(exc))
		{
			await DialogUtility.ShowDangerMessageAsync();
		}
	}

	internal async Task OnFileSelectedAsync(InputFileChangeEventArgs args)
	{
		try
		{
			Cleanup(UmbrellaFileUploadStatus.None);
			SelectedFile = args.File;

			if (SelectedFile is not null)
			{
				Status = UmbrellaFileUploadStatus.Selected;

				if (SelectedFile.Size > MaxFileSizeBytes)
				{
					await DialogUtility.ShowDangerMessageAsync($"Please select a file with a maximum size of {MaxFileSizeMegaBytes} MB.", "Maximum File Size Exceeded");
					return;
				}

				return;
			}

			Status = UmbrellaFileUploadStatus.None;
		}
		catch (Exception exc) when (Logger.WriteError(exc))
		{
			await DialogUtility.ShowDangerMessageAsync();
		}
	}

	[SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "Exceptions from abandoned upload handlers must not affect a newer attempt or a disposed component.")]
	[SuppressMessage("Reliability", "CA2025:Do not pass IDisposable instances into unawaited tasks", Justification = "The refresh task is cancelled and awaited in the inner finally before the stream is disposed; the attempt is disposed in the outer finally.")]
	internal async Task UploadClickAsync()
	{
		if (_disposed || _uploadAttempt is not null)
			return;

		UmbrellaFileUploadAttempt? attempt = null;

		try
		{
			UploadPercentage = 0;

			if (SelectedFile is null)
				throw new InvalidOperationException("File Info should not be null here.");

			if (OnRequestUpload is null)
				throw new InvalidOperationException($"The {OnRequestUpload} property must have an assigned delegate.");

			attempt = new UmbrellaFileUploadAttempt(SelectedFile.Size, ProgressMode);
			_uploadAttempt = attempt;
			Status = UmbrellaFileUploadStatus.Uploading;
			StateHasChanged();

			IOperationResult? result;
			using (Stream stream = SelectedFile.OpenReadStream(MaxFileSizeBytes ?? 512000, attempt.Cancellation.Token))
			{
				attempt.Stream = stream;
				Task refreshTask = RefreshProgressAsync(attempt);
				attempt.RefreshTask = refreshTask;

				try
				{
					result = await OnRequestUpload(new UmbrellaFileUploadRequestEventArgs(stream, SelectedFile.Name, SelectedFile.ContentType,
						attempt.Cancellation.Token, ProgressMode is UmbrellaFileUploadProgressMode.HandlerReported ? attempt : null)
					{
						RegisterSuccessCallback = callback => attempt.OnSucceeded += callback
					});
				}
				finally
				{
					attempt.StopReporting();
					await refreshTask;
				}
			}

			if (!IsCurrentAttempt(attempt))
				return;

			if (result is { IsSuccess: true })
			{
				UploadPercentage = 100;
				Cleanup(UmbrellaFileUploadStatus.Uploaded);
				StateHasChanged();
				// Success is now committed. A wrapper may safely replace and dispose this uploader.
				attempt.OnSucceeded?.Invoke();
				await DialogUtility.ShowSuccessMessageAsync("Your file has been uploaded successfully.");
			}
			else
			{
				Cleanup(UmbrellaFileUploadStatus.Selected);
				StateHasChanged();
				await DialogUtility.ShowOperationResultErrorMessageAsync(result);
			}
		}
		catch (OperationCanceledException) when (attempt?.Cancellation.IsCancellationRequested is true)
		{
			// Cancellation already detached this attempt and restored the selection.
		}
		catch (Exception) when (attempt is not null && !IsCurrentAttempt(attempt))
		{
			// An abandoned handler must not affect a newer upload or a disposed component.
		}
		catch (Exception exc) when (Logger.WriteError(exc))
		{
			Cleanup(UmbrellaFileUploadStatus.Selected);
			StateHasChanged();
			await DialogUtility.ShowDangerMessageAsync("There has been a problem uploading the selected file. Please try again.");
		}
		finally
		{
			attempt?.Dispose();
			if (!_disposed)
				StateHasChanged();
		}
	}

	private bool IsCurrentAttempt(UmbrellaFileUploadAttempt attempt) => !_disposed && ReferenceEquals(_uploadAttempt, attempt);

	private async Task RefreshProgressAsync(UmbrellaFileUploadAttempt attempt)
	{
		using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(500));
		CancellationToken token = attempt.RefreshCancellation.Token;

		try
		{
			while (await timer.WaitForNextTickAsync(token).ConfigureAwait(false))
			{
				await InvokeAsync(() =>
				{
					if (IsCurrentAttempt(attempt) && !token.IsCancellationRequested)
						SampleUploadProgress();
				}).ConfigureAwait(false);
			}
		}
		catch (OperationCanceledException) when (token.IsCancellationRequested)
		{
		}
	}

	internal void SampleUploadProgress()
	{
		if (_uploadAttempt is not { } attempt || !IsCurrentAttempt(attempt) || attempt.RefreshCancellation.IsCancellationRequested)
			return;

		int percentage = attempt.GetPercentage();
		if (percentage == UploadPercentage)
			return;

		UploadPercentage = percentage;
		StateHasChanged();
	}

	private async Task ClearClickAsync()
	{
		try
		{
			if (ShowClearWarning)
			{
				bool clear = await DialogUtility.ShowConfirmWarningMessageAsync("Are you sure you want to clear the current file and choose a new one?", "Clear Selection");

				if (!clear)
					return;
			}

			ClearSelection();
		}
		catch (Exception exc) when (Logger.WriteError(exc))
		{
			await DialogUtility.ShowDangerMessageAsync();
		}
	}

	[SuppressMessage("Usage", "VSTHRD003:Avoid awaiting foreign Tasks", Justification = "The refresh loop belongs to this component and is cancelled before awaiting it; dispatcher work is awaited asynchronously.")]
	internal async Task CancelClickAsync()
	{
		UmbrellaFileUploadAttempt? attempt = _uploadAttempt;
		try
		{
			if (ShowCancelWarning)
			{
				bool cancel = await DialogUtility.ShowConfirmWarningMessageAsync("Are you sure you want to cancel the file upload?", "Cancel Upload");

				if (!cancel)
					return;
			}

			if (attempt is not null && IsCurrentAttempt(attempt))
			{
				Cleanup(UmbrellaFileUploadStatus.Selected);
				await attempt.RefreshTask;
			}
		}
		catch (Exception exc) when (Logger.WriteError(exc))
		{
			await DialogUtility.ShowDangerMessageAsync();
		}
	}

	/// <inheritdoc />
	public void Dispose()
	{
		_disposed = true;
		ClearSelection();
		GC.SuppressFinalize(this);
	}

	private void Cleanup(UmbrellaFileUploadStatus status)
	{
		UmbrellaFileUploadAttempt? attempt = _uploadAttempt;
		_uploadAttempt = null;
		Status = status;
		attempt?.Cancel();
	}

	private void ClearSelection()
	{
		Cleanup(UmbrellaFileUploadStatus.None);
		SelectedFile = null;
	}
}
