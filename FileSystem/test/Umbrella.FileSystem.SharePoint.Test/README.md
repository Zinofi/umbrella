# SharePoint tests

This project distinguishes three test categories using the xUnit `Category` trait:

| Category | Tests | Dependencies |
| --- | --- | --- |
| Integration | `UmbrellaSharePointMetadataIntegrationTest` | Real Microsoft Graph and an existing SharePoint test library |
| Component | `UmbrellaSharePointMetadataTest`, `UmbrellaSharePointMetadataSchemaTest` | Real Graph SDK with simulated HTTP responses |
| Unit | Path translation and service registration tests | No external services |

The integration tests provision all six supported column types and exercise repeated schema setup, typed round trips, deferred writes, clearing, unrelated-field preservation, independent field updates, last-writer-wins, copy, move, and deletion. They generate unique internal column names and physical filenames, then delete those test resources during disposal, including after an assertion failure. They never create or delete the library itself. Cleanup errors fail the test and include its resource prefix; an interrupted process may leave resources requiring manual cleanup.

The component tests retain deterministic coverage of request payloads, pagination, cancellation, rejected writes, retries, and concurrent schema-creation errors. They do not establish compatibility with the live SharePoint service.

## Run against SharePoint

Configure an existing library intended for testing. The application identity needs permissions to create/delete library columns and read/write/delete files. These tests provision schema, so the existing `Sites.ReadWrite.All` application permission alone is insufficient: grant the Graph `Sites.Manage.All` application permission with administrator consent before running them. See the schema permission links in [METADATA.md](../../METADATA.md).

Both suites use `SharePointTestConfiguration`. It loads the existing `Umbrella.FileSystem.Test` user-secrets store, then this project's user secrets, then environment variables (later values take precedence). Credentials do not need to be copied. The existing Berkeley site and `General Document` library remain the defaults; override `SiteId` and `DriveName` to target another test library.

| User-secrets key | Environment variable | Value |
| --- | --- | --- |
| `SharePoint:RunIntegrationTests` | `SharePoint__RunIntegrationTests` | `true` |
| `SharePoint:TenantId` | `SharePoint__TenantId` | Entra tenant ID |
| `SharePoint:ClientId` | `SharePoint__ClientId` | Application ID |
| `SharePoint:ClientSecret` | `SharePoint__ClientSecret` | Application secret |
| `SharePoint:SiteId` | `SharePoint__SiteId` | Graph site ID or `contoso.sharepoint.com:/sites/TestSite:` |
| `SharePoint:DriveName` | `SharePoint__DriveName` | Existing document library name |

From the repository root, after building the test project:

```powershell
$env:SharePoint__RunIntegrationTests = "true"
dotnet run --project FileSystem/test/Umbrella.FileSystem.SharePoint.Test --no-build -- --filter-trait "Category=Integration" --progress off
```

Without `RunIntegrationTests=true`, the live tests report **skipped**. Once enabled, missing configuration, authentication failures, and SharePoint errors fail the tests.

To run only the offline tests:

```powershell
dotnet run --project FileSystem/test/Umbrella.FileSystem.SharePoint.Test --no-build -- --filter-not-trait "Category=Integration" --progress off
```

The broader `Umbrella.FileSystem.Test` project also contains a mixture: `UmbrellaFileProviderTest` exercises real storage providers (disk, Azurite, SharePoint, and Dataverse), while shared metadata tests combine disk integration checks with in-memory stores, mocked services, and simulated HTTP responses.
