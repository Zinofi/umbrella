using System.Globalization;
using System.Text.Json;
using Umbrella.FileSystem.Abstractions;

namespace Umbrella.FileSystem.SharePoint.Test;

[Trait("Category", "Component")]
public class UmbrellaSharePointMetadataTest
{
	private static CancellationToken Token => TestContext.Current.CancellationToken;

	[Fact]
	public async Task DefaultProviderReadsAndWritesTypedMappedFieldsUsingTranslatedPaths()
	{
		using var fixture = new SharePointGraphFixture();
		fixture.Map("IsVisible", SharePointMetadataColumnType.Boolean, "Visible");
		fixture.Map("Title", SharePointMetadataColumnType.Text, "Caption");
		fixture.Map("Notes", SharePointMetadataColumnType.MultilineText);
		fixture.Map("Amount", SharePointMetadataColumnType.Number);
		fixture.Map("Expiry", SharePointMetadataColumnType.DateTime);
		fixture.Map("Status", SharePointMetadataColumnType.Choice, "Status", "Draft", "Approved");
		using var provider = fixture.CreateProvider();
		var file = (await provider.GetAsync("/logical/source.bin", Token))!;
		Assert.True(await file.GetMetadataValueAsync<bool>("isvisible", cancellationToken: Token));
		Assert.Equal("initial", await file.GetMetadataValueAsync<string>("Title", cancellationToken: Token));
		Assert.Equal("fallback", await file.GetMetadataValueAsync("Unrelated", "fallback", cancellationToken: Token));
		Assert.Equal("fallback", await file.GetMetadataValueAsync("Notes", "fallback", cancellationToken: Token));
		await file.SetMetadataValueAsync("ISVISIBLE", false, false, Token);
		await file.SetMetadataValueAsync("Title", "changed", false, Token);
		await file.SetMetadataValueAsync("Notes", "line one\nline two", false, Token);
		await file.SetMetadataValueAsync("Amount", 12.5m, false, Token);
		await file.SetMetadataValueAsync("Expiry", new DateTimeOffset(2026, 10, 22, 12, 0, 0, TimeSpan.FromHours(1)), false, Token);
		await file.SetMetadataValueAsync("Status", "Approved", false, Token);
		Assert.DoesNotContain(fixture.Requests, x => x.Method == "PATCH");
		Assert.False(await file.GetMetadataValueAsync<bool>("IsVisible", cancellationToken: Token));
		await file.WriteMetadataChangesAsync(Token);
		var patch = Assert.Single(fixture.Requests, x => x.Method == "PATCH");
		using var json = JsonDocument.Parse(patch.Body!);
		Assert.False(json.RootElement.GetProperty("Visible").GetBoolean());
		Assert.Equal(12.5, json.RootElement.GetProperty("Amount").GetDouble());
		Assert.Equal("2026-10-22T11:00:00.0000000+00:00", json.RootElement.GetProperty("Expiry").GetString());
		Assert.Equal("Approved", json.RootElement.GetProperty("Status").GetString());
		Assert.Equal("keep", fixture.Files["source.bin"]["Unrelated"]);
		Assert.Contains(fixture.Requests, x => x.Url.Contains("root:/physical/source.bin", StringComparison.Ordinal));
		Assert.Contains(fixture.Requests, x => x.Method == "GET" && x.Url.Contains("/fields?", StringComparison.Ordinal) && x.Url.Contains("$select=Visible,Caption,Notes,Amount,Expiry,Status", StringComparison.Ordinal));
		Assert.DoesNotContain(fixture.Requests, x => x.Url.Contains("/columns", StringComparison.Ordinal));
		var fresh = (await provider.GetAsync(file.SubPath, Token))!;
		Assert.Equal(12.5m, await fresh.GetMetadataValueAsync<decimal>("Amount", cancellationToken: Token));
		Assert.False(await fresh.GetMetadataValueAsync<bool>("IsVisible", cancellationToken: Token));
		Assert.Equal(new DateTimeOffset(2026, 10, 22, 11, 0, 0, TimeSpan.Zero), await fresh.GetMetadataValueAsync<DateTimeOffset>("Expiry", cancellationToken: Token));
		Assert.Equal("converted:changed", await fresh.GetMetadataValueAsync("Title", "fallback", x => "converted:" + x, Token));
	}

	[Fact]
	public async Task ClearsOnlyMappedFieldsWithExplicitNullAndRetainsFailedBatch()
	{
		using var fixture = new SharePointGraphFixture();
		fixture.Map("IsVisible", SharePointMetadataColumnType.Boolean, "Visible");
		fixture.Map("Title", SharePointMetadataColumnType.Text, "Caption");
		using var provider = fixture.CreateProvider();
		var file = (await provider.GetAsync("/logical/source.bin", Token))!;
		await file.RemoveMetadataValueAsync("Title", cancellationToken: Token);
		using (var removal = JsonDocument.Parse(fixture.Requests.Last(x => x.Method == "PATCH").Body!))
		{
			Assert.Equal(JsonValueKind.Null, removal.RootElement.GetProperty("Caption").ValueKind);
			_ = Assert.Single(removal.RootElement.EnumerateObject());
		}

		await file.ClearMetadataAsync(false, Token);
		await file.SetMetadataValueAsync("Title", "after-clear", false, Token);
		fixture.FailPatch = true;
		_ = await Assert.ThrowsAsync<UmbrellaFileSystemException>(() => file.WriteMetadataChangesAsync(Token));
		Assert.Equal("after-clear", await file.GetMetadataValueAsync<string>("Title", cancellationToken: Token));
		fixture.FailPatch = false;
		await file.WriteMetadataChangesAsync(Token);
		using var clear = JsonDocument.Parse(fixture.Requests.Last(x => x.Method == "PATCH").Body!);
		Assert.Equal(JsonValueKind.Null, clear.RootElement.GetProperty("Visible").ValueKind);
		Assert.Equal("after-clear", clear.RootElement.GetProperty("Caption").GetString());
		Assert.Equal("keep", fixture.Files["source.bin"]["Unrelated"]);
	}

	[Fact]
	public async Task RejectsUnmappedAndInvalidValuesWithoutSendingWrites()
	{
		using var fixture = new SharePointGraphFixture();
		fixture.Map("Status", SharePointMetadataColumnType.Choice, "Status", "Draft");
		fixture.Map("Number", SharePointMetadataColumnType.Number);
		using var provider = fixture.CreateProvider();
		var file = (await provider.GetAsync("/logical/source.bin", Token))!;
		var exception = await Assert.ThrowsAsync<UmbrellaFileSystemException>(() => file.SetMetadataValueAsync("Unknown", 1, cancellationToken: Token));
		Assert.Contains("Unknown", exception.InnerException!.Message, StringComparison.Ordinal);
		_ = await Assert.ThrowsAsync<UmbrellaFileSystemException>(() => file.RemoveMetadataValueAsync("Unknown", cancellationToken: Token));
		_ = await Assert.ThrowsAsync<UmbrellaFileSystemException>(() => file.SetMetadataValueAsync("Status", "draft", cancellationToken: Token));
		_ = await Assert.ThrowsAsync<UmbrellaFileSystemException>(() => file.SetMetadataValueAsync("Number", double.NaN, cancellationToken: Token));
		Assert.DoesNotContain(fixture.Requests, x => x.Method == "PATCH");
	}

	[Fact]
	public async Task EmptyMappingsHaveNoMetadataIoAndCustomBackendTakesPrecedence()
	{
		using var fixture = new SharePointGraphFixture();
		using (var provider = fixture.CreateProvider())
		{
			var file = (await provider.GetAsync("/logical/source.bin", Token))!;
			fixture.Requests.Clear();
			Assert.Equal("fallback", await file.GetMetadataValueAsync("missing", "fallback", cancellationToken: Token));
			await file.ClearMetadataAsync(cancellationToken: Token);
			_ = await Assert.ThrowsAsync<UmbrellaFileSystemException>(() => file.SetMetadataValueAsync("missing", true, cancellationToken: Token));
			Assert.Empty(fixture.Requests);
		}

		fixture.Options.MetadataProvider = new UmbrellaUnsupportedFileMetadataProvider();
		fixture.Options.MetadataNamespace = "custom";
		fixture.Map("IsVisible", SharePointMetadataColumnType.Boolean, "Visible");
		using var custom = fixture.CreateProvider();
		var customFile = (await custom.GetAsync("/logical/source.bin", Token))!;
		fixture.Requests.Clear();
		_ = await Assert.ThrowsAsync<NotSupportedException>(() => customFile.GetMetadataValueAsync<bool>("IsVisible", cancellationToken: Token));
		Assert.Empty(fixture.Requests);
	}

	[Fact]
	public async Task MetadataLifecyclePreservesContentUpdatesCopiesPersistedValuesAndProtectsFailedMoves()
	{
		using var fixture = new SharePointGraphFixture();
		fixture.Map("Title", SharePointMetadataColumnType.Text, "Caption");
		using var provider = fixture.CreateProvider();
		var created = await provider.CreateAsync("/logical/new.bin", Token);
		_ = await Assert.ThrowsAsync<InvalidOperationException>(() => created.SetMetadataValueAsync("Title", "new", cancellationToken: Token));
		await created.WriteFromByteArrayAsync([1], cancellationToken: Token);
		await created.SetMetadataValueAsync("Title", "persisted", cancellationToken: Token);
		await created.WriteFromByteArrayAsync([2], cancellationToken: Token);
		Assert.Equal("persisted", fixture.Files["new.bin"]["Caption"]!.ToString());
		await created.SetMetadataValueAsync("Title", "pending", false, Token);
		fixture.Files["target.bin"] = new() { ["Caption"] = "old", ["Unrelated"] = "keep" };
		var target = (await provider.GetAsync("/logical/target.bin", Token))!;
		_ = await created.CopyAsync(target, Token);
		Assert.Equal("persisted", fixture.Files["target.bin"]["Caption"]!.ToString());
		Assert.Equal("keep", fixture.Files["target.bin"]["Unrelated"]);
		fixture.FailPatch = true;
		_ = await Assert.ThrowsAsync<UmbrellaFileSystemException>(() => created.MoveAsync("/logical/moved.bin", Token));
		Assert.True(fixture.Files.ContainsKey("new.bin"));
		fixture.FailPatch = false;
		var moved = await created.MoveAsync("/logical/moved.bin", Token);
		Assert.False(fixture.Files.ContainsKey("new.bin"));
		Assert.Equal("persisted", await moved.GetMetadataValueAsync<string>("Title", cancellationToken: Token));
		fixture.Requests.Clear();
		Assert.True(await moved.DeleteAsync(Token));
		_ = Assert.Single(fixture.Requests);
		Assert.Equal("DELETE", fixture.Requests[0].Method);
	}

	[Fact]
	public async Task AuthorizationCanReadMetadataAndDeniedWritesRemainPending()
	{
		using var fixture = new SharePointGraphFixture();
		fixture.Map("IsVisible", SharePointMetadataColumnType.Boolean, "Visible");
		var handler = new AuthorizationHandler();
		using var provider = fixture.CreateProvider(handler);
		var file = (await provider.GetAsync("/logical/source.bin", Token))!;
		Assert.True(handler.ReadValue);
		_ = await Assert.ThrowsAsync<UmbrellaFileSystemException>(() => file.SetMetadataValueAsync("IsVisible", false, cancellationToken: Token));
		Assert.DoesNotContain(fixture.Requests, x => x.Method == "PATCH");
		handler.AllowUpdate = true;
		await file.WriteMetadataChangesAsync(Token);
		_ = Assert.Single(fixture.Requests, x => x.Method == "PATCH");
	}

	[Fact]
	public async Task CancellationDoesNotStageOrSendChanges()
	{
		using var fixture = new SharePointGraphFixture();
		fixture.Map("Title", SharePointMetadataColumnType.Text, "Caption");
		using var provider = fixture.CreateProvider();
		var file = (await provider.GetAsync("/logical/source.bin", Token))!;
		using var cancellation = new CancellationTokenSource();
		await cancellation.CancelAsync();
		fixture.Requests.Clear();
		_ = await Assert.ThrowsAnyAsync<OperationCanceledException>(() => file.SetMetadataValueAsync("Title", "canceled", cancellationToken: cancellation.Token));
		await file.WriteMetadataChangesAsync(Token);
		Assert.Empty(fixture.Requests);
	}

	[Fact]
	public async Task TextThatLooksLikeADateRoundTripsWithoutNormalization()
	{
		using var fixture = new SharePointGraphFixture();
		const string text = "2026-10-22T12:00:00+01:00";
		fixture.Files["source.bin"]["Caption"] = text;
		fixture.Map("Title", SharePointMetadataColumnType.Text, "Caption");
		using var provider = fixture.CreateProvider();
		var file = (await provider.GetAsync("/logical/source.bin", Token))!;
		Assert.Equal(text, await file.GetMetadataValueAsync<string>("Title", cancellationToken: Token));
	}

	[Fact]
	public async Task NumericWireValuesAreIndependentOfCurrentCulture()
	{
		using var fixture = new SharePointGraphFixture();
		fixture.Map("Number", SharePointMetadataColumnType.Number);
		using var provider = fixture.CreateProvider();
		var file = (await provider.GetAsync("/logical/source.bin", Token))!;
		var previous = CultureInfo.CurrentCulture;
		try
		{
			CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("fr-FR");
			await file.SetMetadataValueAsync("Number", 1.25m, cancellationToken: Token);
			Assert.Contains("1.25", fixture.Requests.Last(x => x.Method == "PATCH").Body!, StringComparison.Ordinal);
			Assert.Equal(1.25m, await file.GetMetadataValueAsync<decimal>("Number", cancellationToken: Token));
			var fresh = (await provider.GetAsync(file.SubPath, Token))!;
			Assert.Equal(1.25m, await fresh.GetMetadataValueAsync<decimal?>("Number", cancellationToken: Token));
		}
		finally
		{
			CultureInfo.CurrentCulture = previous;
		}
	}

	private sealed class AuthorizationHandler : IUmbrellaFileAuthorizationHandler
	{
		public string DirectoryName => "logical";
		internal bool AllowUpdate { get; set; }
		internal bool ReadValue { get; private set; }
		public async Task<bool> AuthorizeAsync(IUmbrellaFileInfo fileInfo, UmbrellaFileOperationType operationType, CancellationToken cancellationToken = default)
		{
			if (operationType == UmbrellaFileOperationType.Read)
				ReadValue = await fileInfo.GetMetadataValueAsync<bool>("IsVisible", cancellationToken: cancellationToken);
			return operationType != UmbrellaFileOperationType.Update || AllowUpdate;
		}
	}
}
