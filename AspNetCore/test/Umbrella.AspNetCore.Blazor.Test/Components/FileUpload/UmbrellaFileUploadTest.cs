using System.Diagnostics.CodeAnalysis;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.Web.HtmlRendering;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;
using Moq;
using Umbrella.AspNetCore.Blazor.Components.Dialog.Abstractions;
using Umbrella.AspNetCore.Blazor.Components.DynamicImage.Options;
using Umbrella.AspNetCore.Blazor.Components.FileImagePreviewUpload;
using Umbrella.AspNetCore.Blazor.Components.FileUpload;
using Umbrella.AspNetCore.Blazor.Services.Abstractions;
using Umbrella.DynamicImage.Abstractions;
using Umbrella.Internal.Mocks;
using Umbrella.Utilities.Imaging.Abstractions;
using Umbrella.Utilities.Primitives;
using Umbrella.Utilities.Primitives.Abstractions;

namespace Umbrella.AspNetCore.Blazor.Test.Components.FileUpload;

[SuppressMessage("Usage", "VSTHRD003:Avoid awaiting foreign Tasks", Justification = "Test-owned completion sources deliberately hold handlers open while dispatcher operations verify progress and cancellation.")]
public sealed class UmbrellaFileUploadTest
{
	[Theory]
	[InlineData(0, 1000, 0)]
	[InlineData(250, 1000, 25)]
	[InlineData(500, 1000, 50)]
	[InlineData(750, 1000, 75)]
	[InlineData(5, 1000, 1)]
	[InlineData(4, 1000, 0)]
	[InlineData(999, 1000, 99)]
	[InlineData(1000, 1000, 100)]
	[InlineData(2000, 1000, 100)]
	[InlineData(-10, 1000, 0)]
	[InlineData(0, 0, 0)]
	[InlineData(1, 0, 0)]
	[InlineData(long.MaxValue - 1, long.MaxValue, 99)]
	public void Percentage_handles_partial_reads_rounding_and_boundaries(long position, long length, int expected)
		=> Assert.Equal(expected, UmbrellaFileUploadAttempt.CalculatePercentage(position, length));

	[Fact]
	public void Original_constructor_preserves_existing_arguments_without_a_reporter()
	{
		using var content = new MemoryStream();
		CancellationToken token = TestContext.Current.CancellationToken;
		var args = new UmbrellaFileUploadRequestEventArgs(content, "file.txt", "text/plain", token);

		Assert.Same(content, args.Content);
		Assert.Equal("file.txt", args.FileName);
		Assert.Equal("text/plain", args.Type);
		Assert.Equal(token, args.UploadCancellationToken);
		Assert.Null(args.UploadProgress);
	}

	[Fact]
	public async Task Default_mode_renders_intermediate_source_stream_progress()
	{
		await using var harness = new UploadHarness();
		await harness.RenderAsync();
		await harness.SelectFileAsync(1000);
		var completion = CreateCompletion();
		await harness.StartAsync(_ => completion.Task);

		Assert.Equal(UmbrellaFileUploadProgressMode.StreamRead, harness.Component.ProgressMode);
		Assert.Null(harness.Arguments.UploadProgress);

		foreach (int percentage in new[] { 25, 50, 75, 100 })
		{
			await harness.Arguments.Content.ReadExactlyAsync(new byte[250], TestContext.Current.CancellationToken);
			await harness.AssertProgressAsync(percentage);
		}

		Assert.Equal(UmbrellaFileUploadStatus.Uploading, harness.Component.Status);
		completion.SetResult(OperationResult.Success());
		await harness.FinishAsync();
		Assert.Equal(UmbrellaFileUploadStatus.Uploaded, harness.Component.Status);
	}

	[Theory]
	[InlineData(typeof(UmbrellaFileUpload))]
	[InlineData(typeof(UmbrellaManagedFileUpload))]
	[InlineData(typeof(UmbrellaFileImagePreviewUpload))]
	public async Task Handler_mode_and_reporter_are_forwarded_and_independent_of_source_reads(Type componentType)
	{
		await using var harness = new UploadHarness();
		await harness.RenderAsync(componentType, UmbrellaFileUploadProgressMode.HandlerReported);
		await harness.SelectFileAsync(1000);
		var completion = CreateCompletion();
		await harness.StartAsync(_ => completion.Task);

		Assert.Equal(UmbrellaFileUploadProgressMode.HandlerReported, harness.Component.ProgressMode);
		IProgress<long> reporter = Assert.IsAssignableFrom<IProgress<long>>(harness.Arguments.UploadProgress);
		await harness.Arguments.Content.CopyToAsync(Stream.Null, TestContext.Current.CancellationToken);
		await harness.AssertProgressAsync(0);

		reporter.Report(250);
		await harness.AssertProgressAsync(25);
		reporter.Report(750);
		await harness.AssertProgressAsync(75);
		reporter.Report(1000);
		await harness.AssertProgressAsync(100);
		Assert.Equal(UmbrellaFileUploadStatus.Uploading, harness.Component.Status);

		completion.SetResult(OperationResult.Success());
		await harness.FinishAsync();
	}

	[Theory]
	[InlineData(typeof(UmbrellaManagedFileUpload))]
	[InlineData(typeof(UmbrellaFileImagePreviewUpload))]
	public async Task Wrappers_default_to_stream_read_mode(Type componentType)
	{
		await using var harness = new UploadHarness();
		await harness.RenderAsync(componentType);
		await harness.SelectFileAsync(1000);
		await harness.StartAsync(args =>
		{
			Assert.Null(args.UploadProgress);
			return Task.FromResult<IOperationResult?>(OperationResult.Success());
		});
		await harness.FinishAsync();
		Assert.Equal(UmbrellaFileUploadProgressMode.StreamRead, harness.Component.ProgressMode);
	}

	[Fact]
	public async Task Reports_are_clamped_monotonic_and_safe_from_background_threads()
	{
		await using var harness = new UploadHarness();
		await harness.RenderAsync(mode: UmbrellaFileUploadProgressMode.HandlerReported);
		await harness.SelectFileAsync(1000);
		var completion = CreateCompletion();
		await harness.StartAsync(_ => completion.Task);
		IProgress<long> reporter = Assert.IsAssignableFrom<IProgress<long>>(harness.Arguments.UploadProgress);

		reporter.Report(-100);
		await harness.AssertProgressAsync(0);
		await Task.WhenAll(Enumerable.Range(0, 751).Select(bytes => Task.Run(() => reporter.Report(bytes), TestContext.Current.CancellationToken)));
		await harness.AssertProgressAsync(75);
		reporter.Report(100);
		await harness.AssertProgressAsync(75);
		reporter.Report(long.MaxValue);
		await harness.AssertProgressAsync(100);

		completion.SetResult(OperationResult.Success());
		await harness.FinishAsync();
		// Retained reporters remain safe even after their attempt has been disposed.
		reporter.Report(0);
		Assert.Equal(100, harness.Component.UploadPercentage);
	}

	[Theory]
	[InlineData(0, UmbrellaFileUploadProgressMode.StreamRead)]
	[InlineData(0, UmbrellaFileUploadProgressMode.HandlerReported)]
	[InlineData(1000, UmbrellaFileUploadProgressMode.HandlerReported)]
	public async Task Empty_files_and_missing_reports_stay_at_zero_until_success(int size, UmbrellaFileUploadProgressMode mode)
	{
		await using var harness = new UploadHarness();
		await harness.RenderAsync(mode: mode);
		await harness.SelectFileAsync(size);
		var completion = CreateCompletion();
		await harness.StartAsync(_ => completion.Task);
		await harness.AssertProgressAsync(0);
		Task refresh = harness.Component.ProgressRefreshTask;

		completion.SetResult(OperationResult.Success());
		await harness.FinishAsync();

		Assert.True(refresh.IsCompletedSuccessfully);
		Assert.False(harness.Arguments.Content.CanRead);
		Assert.Equal(100, harness.Component.UploadPercentage);
		Assert.Equal(UmbrellaFileUploadStatus.Uploaded, harness.Component.Status);
	}

	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task Failure_stops_refresh_and_ignores_late_reports(bool throws)
	{
		await using var harness = new UploadHarness();
		await harness.RenderAsync(mode: UmbrellaFileUploadProgressMode.HandlerReported);
		await harness.SelectFileAsync(1000);
		var completion = CreateCompletion();
		await harness.StartAsync(_ => completion.Task);
		IProgress<long> reporter = Assert.IsAssignableFrom<IProgress<long>>(harness.Arguments.UploadProgress);
		reporter.Report(250);
		await harness.AssertProgressAsync(25);
		Task refresh = harness.Component.ProgressRefreshTask;

		if (throws)
			completion.SetException(new IOException("Upload failed."));
		else
			completion.SetResult(OperationResult.GenericFailure("Upload failed."));
		await harness.FinishAsync();

		reporter.Report(1000);
		await harness.Renderer.Dispatcher.InvokeAsync(harness.Component.SampleUploadProgress);
		Assert.Equal(25, harness.Component.UploadPercentage);
		Assert.Equal(UmbrellaFileUploadStatus.Selected, harness.Component.Status);
		Assert.True(refresh.IsCompletedSuccessfully);
		Assert.False(harness.Arguments.Content.CanRead);
		Assert.NotEmpty(harness.Dialogs.Invocations);
	}

	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task Cancelled_attempt_cannot_update_retry_through_reports_or_completion(bool oldHandlerThrows)
	{
		await using var harness = new UploadHarness();
		await harness.RenderAsync(mode: UmbrellaFileUploadProgressMode.HandlerReported);
		await harness.SelectFileAsync(1000);
		var oldCompletion = CreateCompletion();
		await harness.StartAsync(_ => oldCompletion.Task);
		Task oldUpload = harness.UploadTask;
		Task oldRefresh = harness.Component.ProgressRefreshTask;
		UmbrellaFileUploadRequestEventArgs oldArguments = harness.Arguments;
		await harness.Renderer.Dispatcher.InvokeAsync(harness.Component.CancelClickAsync);

		Assert.True(oldArguments.UploadCancellationToken.IsCancellationRequested);
		Assert.True(oldRefresh.IsCompletedSuccessfully);
		Assert.Equal(UmbrellaFileUploadStatus.Selected, harness.Component.Status);

		var newCompletion = CreateCompletion();
		await harness.StartAsync(_ => newCompletion.Task);
		await harness.AssertProgressAsync(0);
		harness.Arguments.UploadProgress!.Report(250);
		await harness.AssertProgressAsync(25);
		oldArguments.UploadProgress!.Report(1000);
		if (oldHandlerThrows)
			oldCompletion.SetException(new IOException("Late failure."));
		else
			oldCompletion.SetResult(OperationResult.Success());
		await oldUpload.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);

		await harness.AssertProgressAsync(25);
		Assert.Equal(UmbrellaFileUploadStatus.Uploading, harness.Component.Status);
		Assert.Empty(harness.Dialogs.Invocations);
		Assert.False(harness.Arguments.UploadCancellationToken.IsCancellationRequested);
		Assert.False(oldArguments.Content.CanRead);

		newCompletion.SetResult(OperationResult.Success());
		await harness.FinishAsync();
	}

	[Theory]
	[InlineData(false, false)]
	[InlineData(false, true)]
	[InlineData(true, false)]
	[InlineData(true, true)]
	public async Task Cancellation_and_disposal_stop_refresh_without_error_dialogs(bool dispose, bool cancellationException)
	{
		await using var harness = new UploadHarness();
		await harness.RenderAsync(mode: UmbrellaFileUploadProgressMode.HandlerReported);
		await harness.SelectFileAsync(1000);
		var completion = CreateCompletion();
		await harness.StartAsync(_ => completion.Task);
		Task refresh = harness.Component.ProgressRefreshTask;
		if (dispose)
			await harness.Renderer.Dispatcher.InvokeAsync(harness.Component.Dispose);
		else
			await harness.Renderer.Dispatcher.InvokeAsync(harness.Component.CancelClickAsync);

		await refresh.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
		harness.Arguments.UploadProgress!.Report(1000);
		if (cancellationException)
			completion.SetCanceled(harness.Arguments.UploadCancellationToken);
		else
			completion.SetResult(OperationResult.Success());
		await harness.FinishAsync();

		Assert.Equal(0, harness.Component.UploadPercentage);
		Assert.Equal(dispose ? UmbrellaFileUploadStatus.None : UmbrellaFileUploadStatus.Selected, harness.Component.Status);
		Assert.Empty(harness.Dialogs.Invocations);
		Assert.False(harness.Arguments.Content.CanRead);
	}

	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task Refresh_stops_and_stream_is_disposed_before_completion_dialog(bool success)
	{
		await using var harness = new UploadHarness();
		await harness.RenderAsync();
		await harness.SelectFileAsync(1000);
		var completion = CreateCompletion();
		await harness.StartAsync(_ => completion.Task);
		Task refresh = harness.Component.ProgressRefreshTask;
		bool refreshStoppedAtDialog = false;
		bool streamDisposedAtDialog = false;
		Action observe = () =>
		{
			refreshStoppedAtDialog = refresh.IsCompletedSuccessfully;
			streamDisposedAtDialog = !harness.Arguments.Content.CanRead;
		};
		if (success)
			_ = harness.Dialogs.Setup(x => x.ShowSuccessMessageAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>())).Callback(observe);
		else
			_ = harness.Dialogs.Setup(x => x.ShowOperationResultErrorMessageAsync(It.IsAny<IOperationResult?>(), It.IsAny<string>())).Callback(observe);

		completion.SetResult(success ? OperationResult.Success() : OperationResult.GenericFailure("Upload failed."));
		await harness.FinishAsync();

		Assert.True(refreshStoppedAtDialog);
		Assert.True(streamDisposedAtDialog);
	}

	[Theory]
	[InlineData(typeof(UmbrellaManagedFileUpload), UmbrellaFileUploadProgressMode.StreamRead)]
	[InlineData(typeof(UmbrellaManagedFileUpload), UmbrellaFileUploadProgressMode.HandlerReported)]
	[InlineData(typeof(UmbrellaFileImagePreviewUpload), UmbrellaFileUploadProgressMode.StreamRead)]
	[InlineData(typeof(UmbrellaFileImagePreviewUpload), UmbrellaFileUploadProgressMode.HandlerReported)]
	public async Task Successful_wrapper_transition_removes_uploader_and_notifies_once(Type wrapperType, UmbrellaFileUploadProgressMode mode)
	{
		await using var harness = new UploadHarness();
		await harness.PrepareWrapperAsync(wrapperType, mode);
		await harness.SelectFileAsync(1000);
		var completion = CreateCompletion();
		await harness.StartAsync(async _ =>
		{
			IOperationResult? result = await completion.Task;
			harness.Image?.Update("/images/replacement.jpg");
			return result;
		});
		Task refresh = harness.Component.ProgressRefreshTask;
		string? markupAtNotification = null;
		bool stoppedAtNotification = false;
		_ = harness.Dialogs.Setup(x => x.ShowSuccessMessageAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
			.Callback(() =>
			{
				markupAtNotification = harness.GetHtml();
				stoppedAtNotification = refresh.IsCompletedSuccessfully && !harness.Arguments.Content.CanRead;
			});

		completion.SetResult(OperationResult.Success());
		await harness.FinishAsync();

		harness.Dialogs.Verify(x => x.ShowSuccessMessageAsync("Your file has been uploaded successfully.", "Success", It.IsAny<string>()), Times.Once);
		Assert.True(stoppedAtNotification);
		Assert.NotNull(markupAtNotification);
		Assert.DoesNotContain("class=\"u-file-upload\"", markupAtNotification, StringComparison.Ordinal);
		Assert.Contains(wrapperType == typeof(UmbrellaManagedFileUpload) ? "View File" : "replacement.jpg", markupAtNotification, StringComparison.Ordinal);
		// The actual render transition disposed the child, rather than just changing its status to Uploaded.
		Assert.Equal(UmbrellaFileUploadStatus.None, harness.Component.Status);
	}

	[Theory]
	[InlineData(typeof(UmbrellaManagedFileUpload), false)]
	[InlineData(typeof(UmbrellaManagedFileUpload), true)]
	[InlineData(typeof(UmbrellaFileImagePreviewUpload), false)]
	[InlineData(typeof(UmbrellaFileImagePreviewUpload), true)]
	public async Task Abandoned_wrapper_upload_does_not_notify_or_restore_existing_file(Type wrapperType, bool dispose)
	{
		await using var harness = new UploadHarness();
		await harness.PrepareWrapperAsync(wrapperType, UmbrellaFileUploadProgressMode.HandlerReported);
		await harness.SelectFileAsync(1000);
		var completion = CreateCompletion();
		await harness.StartAsync(_ => completion.Task);
		if (dispose)
			await harness.Renderer.Dispatcher.InvokeAsync(harness.Component.Dispose);
		else
			await harness.Renderer.Dispatcher.InvokeAsync(harness.Component.CancelClickAsync);

		completion.SetResult(OperationResult.Success());
		await harness.FinishAsync();

		Assert.Empty(harness.Dialogs.Invocations);
		if (harness.Managed is { } managed)
			Assert.False(managed.IsShowingExistingFile);
	}

	private static TaskCompletionSource<IOperationResult?> CreateCompletion() => new(TaskCreationOptions.RunContinuationsAsynchronously);

	private sealed class UploadHarness : IAsyncDisposable
	{
		private readonly ServiceProvider _services;
		private readonly CapturingActivator _activator = new();
		private HtmlRootComponent _output;
		private Func<UmbrellaFileUploadRequestEventArgs, Task<IOperationResult?>> _handler = null!;

		internal UploadHarness()
		{
			var services = new ServiceCollection();
			_ = services.AddLogging();
			_ = services.AddSingleton(Dialogs.Object);
			_ = services.AddSingleton(Mock.Of<IJSRuntime>());
			_ = services.AddSingleton(Mock.Of<IUmbrellaBlazorInteropService>());
			_ = services.AddSingleton(new UmbrellaDynamicImageOptions());
			_ = services.AddSingleton<IResponsiveImageHelper>(CoreUtilitiesMocks.CreateResponsiveImageHelper());
			_ = services.AddSingleton<IDynamicImageUtility>(provider => new DynamicImageUtility(provider.GetRequiredService<ILogger<DynamicImageUtility>>()));
			_ = services.AddSingleton<IComponentActivator>(_activator);
			_services = services.BuildServiceProvider();
			Renderer = new HtmlRenderer(_services, _services.GetRequiredService<ILoggerFactory>());
		}

		internal HtmlRenderer Renderer { get; }
		internal Mock<IUmbrellaDialogService> Dialogs { get; } = new();
		internal UmbrellaFileUpload Component => _activator.Upload!;
		internal UmbrellaManagedFileUpload? Managed => _activator.Managed;
		internal UmbrellaFileImagePreviewUpload? Image => _activator.Image;
		internal UmbrellaFileUploadRequestEventArgs Arguments { get; private set; }
		internal Task UploadTask { get; private set; } = Task.CompletedTask;

		internal async Task RenderAsync(Type? type = null, UmbrellaFileUploadProgressMode? mode = null, string? existingFileUrl = null)
		{
			var parameters = new Dictionary<string, object?>
			{
				[nameof(UmbrellaFileUpload.ShowCancelWarning)] = false,
				[nameof(UmbrellaFileUpload.OnRequestUpload)] = new Func<UmbrellaFileUploadRequestEventArgs, Task<IOperationResult?>>(args =>
				{
					Arguments = args;
					return _handler(args);
				})
			};
			if (mode.HasValue)
				parameters[nameof(UmbrellaFileUpload.ProgressMode)] = mode.Value;
			if (existingFileUrl is not null)
				parameters[nameof(UmbrellaManagedFileUpload.ExistingFileUrl)] = existingFileUrl;

			await Renderer.Dispatcher.InvokeAsync(async () =>
			{
				_output = await Renderer.RenderComponentAsync(type ?? typeof(UmbrellaFileUpload), ParameterView.FromDictionary(parameters));
			});
		}

		internal async Task PrepareWrapperAsync(Type wrapperType, UmbrellaFileUploadProgressMode mode)
		{
			await RenderAsync(wrapperType, mode, wrapperType == typeof(UmbrellaManagedFileUpload) ? "/files/existing.txt" : null);
			if (Managed is { } managed)
				await Renderer.Dispatcher.InvokeAsync(() => EventCallback.Factory.Create(managed, managed.ReplaceFileClickAsync).InvokeAsync());
		}

		internal string GetHtml() => _output.ToHtmlString();

		internal async Task SelectFileAsync(int length)
		{
			var file = new Mock<IBrowserFile>();
			_ = file.SetupGet(x => x.Size).Returns(length);
			_ = file.SetupGet(x => x.Name).Returns("file.txt");
			_ = file.SetupGet(x => x.ContentType).Returns("text/plain");
			_ = file.Setup(x => x.OpenReadStream(It.IsAny<long>(), It.IsAny<CancellationToken>())).Returns(() => new MemoryStream(new byte[length]));
			await Renderer.Dispatcher.InvokeAsync(() => Component.OnFileSelectedAsync(new InputFileChangeEventArgs([file.Object])));
		}

		internal async Task StartAsync(Func<UmbrellaFileUploadRequestEventArgs, Task<IOperationResult?>> handler)
		{
			_handler = handler;
			await Renderer.Dispatcher.InvokeAsync(() => { UploadTask = Component.UploadClickAsync(); });
		}

		internal async Task AssertProgressAsync(int percentage)
		{
			await Renderer.Dispatcher.InvokeAsync(() =>
			{
				Component.SampleUploadProgress();
				string html = _output.ToHtmlString();
				Assert.Equal(percentage, Component.UploadPercentage);
				Assert.Contains($"progress-bar--{percentage}\"", html, StringComparison.Ordinal);
				Assert.Contains($"aria-valuenow=\"{percentage}\"", html, StringComparison.Ordinal);
			});
		}

		internal async Task FinishAsync() => await UploadTask.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);

		public async ValueTask DisposeAsync()
		{
			await Renderer.DisposeAsync();
			await _services.DisposeAsync();
		}
	}

	private sealed class CapturingActivator : IComponentActivator
	{
		internal UmbrellaFileUpload? Upload { get; private set; }
		internal UmbrellaManagedFileUpload? Managed { get; private set; }
		internal UmbrellaFileImagePreviewUpload? Image { get; private set; }

		public IComponent CreateInstance(Type componentType)
		{
			var component = (IComponent)Activator.CreateInstance(componentType)!;
			if (component is UmbrellaFileUpload upload)
				Upload = upload;
			if (component is UmbrellaManagedFileUpload managed)
				Managed = managed;
			if (component is UmbrellaFileImagePreviewUpload image)
				Image = image;
			return component;
		}
	}
}
