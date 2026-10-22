namespace Umbrella.FileSystem.SharePoint;

internal static class SharePointGraphHelper
{
	internal static async Task<string> ResolveDriveIdAsync(UmbrellaSharePointFileStorageProviderOptions options, CancellationToken cancellationToken)
	{
		var request = options.GraphServiceClient.Sites[options.SiteId].Drives;
		var page = await request.GetAsync(config => config.QueryParameters.Select = ["id", "name"], cancellationToken).ConfigureAwait(false);
		while (page is not null)
		{
			var drive = page.Value?.FirstOrDefault(x => string.Equals(x.Name, options.DriveName, StringComparison.OrdinalIgnoreCase));
			if (!string.IsNullOrWhiteSpace(drive?.Id))
				return drive.Id;
			if (string.IsNullOrEmpty(page.OdataNextLink))
				break;
			page = await request.WithUrl(page.OdataNextLink).GetAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
		}

		throw new InvalidOperationException($"The SharePoint document library '{options.DriveName}' could not be found for site '{options.SiteId}'.");
	}
}
