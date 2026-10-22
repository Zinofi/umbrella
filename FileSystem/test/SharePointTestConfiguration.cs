using Microsoft.Extensions.Configuration;

namespace Umbrella.FileSystem.Testing;

internal static class SharePointTestConfiguration
{
	internal static IConfigurationRoot Create<T>() where T : class
		=> new ConfigurationBuilder()
			.AddInMemoryCollection(new Dictionary<string, string?>
			{
				["SharePoint:SiteId"] = "zinofi.sharepoint.com:/sites/Berkeley:",
				["SharePoint:DriveName"] = "General Document"
			})
			// Reuse the existing Umbrella.FileSystem.Test credentials without copying secrets.
			.AddUserSecrets("a3f1d9e2-7b4c-4f8a-b6e3-c2d5f0a1e8b4")
			.AddUserSecrets<T>(optional: true)
			.AddEnvironmentVariables()
			.Build();
}
