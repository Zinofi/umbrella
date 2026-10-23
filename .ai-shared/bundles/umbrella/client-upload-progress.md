# Client upload progress

Use this reference when adding progress to a raw-body upload service and its Blazor
callback. Inspect the installed Umbrella API and consuming target frameworks first:
`ProgressStreamContent`, `ProgressMode`, and `UploadProgress` must all be available.
Do not silently upgrade packages or change an existing upload protocol to use this example.
For older consumers, retain their compatible stream-read behaviour until migration is in scope.

## Dependencies and request streaming

The content class is in `Umbrella.Utilities.Http`. For a .NET 9+ Blazor WebAssembly
consumer, reference `Microsoft.AspNetCore.Components.WebAssembly` in the project that
constructs upload requests (for example, Thrive's `Client.Data` project). Use the
version matching the app's target framework and existing Microsoft package family;
with central package management, put the version in the app's central package file.
Import `Microsoft.AspNetCore.Components.WebAssembly.Http` to use the framework's
`SetBrowserRequestStreamingEnabled` extension. No delegating handler or additional
HTTP-client registration is needed. Ordinary API calls keep their existing behaviour.

`ProgressStreamContent` reports zero and then cumulative original-file bytes after
successful writes to the HTTP transport. This is transport-write progress, **not**
server acknowledgement or a guarantee of bytes sent over the network. Do not implement
progress by counting source reads or copying the file to a memory buffer first.
Browser buffering can still make the bar reach 100% before the network upload finishes;
the component only considers the operation successful after the handler succeeds.

Browser request streaming requires .NET 9+ and compatible browser/network support.
Microsoft documents Chromium browsers over HTTPS and HTTP/2. Streaming requests have
no `Content-Length` header, and cross-origin requests require CORS preflight. Redirects
other than 303 can fail because the body cannot be replayed. Unsupported browsers may
fall back to buffering; a browser that supports streams can still fail over HTTP/1.x.
Proxies or servers can also buffer. Validate the actual deployment; opting in does not
guarantee end-to-end streaming. .NET 8 browser consumers cannot use this request-streaming
extension. This limitation does not prevent using the content class with server-side HttpClient.

- [Microsoft browser request streaming guidance](https://learn.microsoft.com/en-us/aspnet/core/blazor/call-web-api?view=aspnetcore-10.0#client-side-request-streaming)
- [Fetch streaming restrictions](https://developer.chrome.com/docs/capabilities/web-apis/fetch-streaming-requests)

## Upload service example

This is a Thrive-shaped service, preserving its raw-body headers, response filename,
Location URL, and problem-details mapping. Adapt the application namespaces and upload
enum. Append the optional progress argument to both the existing interface and implementation;
existing source calls continue to compile. This is not binary compatibility for already
compiled consumers: rebuild them or retain a forwarding overload when that is required. The narrow CA1068 suppression documents the compatibility-driven parameter order; new APIs should normally put cancellation last.

```csharp
using CommunityToolkit.Diagnostics;
using Microsoft.AspNetCore.Components.WebAssembly.Http;
using Microsoft.Extensions.Logging;
using ThriveForSend.Shared.Common.Enumerations;
using Umbrella.AppFramework.Shared.Constants;
using Umbrella.Utilities.Extensions;
using Umbrella.Utilities.Http;
using Umbrella.Utilities.Http.Abstractions;

namespace UploadExample;

public interface IFileUploadService
{
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1068:CancellationToken parameters must come last",
        Justification = "Append optional progress while preserving existing positional source calls.")]
    Task<IHttpOperationResult<(string tempFileName, string? url)>> UploadAsync(
        Stream content, string fileName, FileUploadType uploadType, string mimeType,
        CancellationToken cancellationToken = default, IProgress<long>? uploadProgress = null);
}

internal sealed class FileUploadService(
    ILogger<FileUploadService> logger,
    HttpClient httpClient,
    IGenericHttpServiceUtility httpServiceUtility) : IFileUploadService
{
    public async Task<IHttpOperationResult<(string tempFileName, string? url)>> UploadAsync(
        Stream content, string fileName, FileUploadType uploadType, string mimeType,
        CancellationToken cancellationToken = default, IProgress<long>? uploadProgress = null)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Guard.IsNotNull(content);
        Guard.IsNotNull(fileName);
        Guard.IsNotNull(mimeType);

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, "api/FileUpload");
            if (uploadProgress is null)
            {
                // Preserve the existing no-progress path and its stream ownership.
                request.Content = new StreamContent(content);
            }
            else
            {
                request.Content = new ProgressStreamContent(content, uploadProgress,
                    leaveOpen: true, cancellationToken: cancellationToken);
                _ = request.SetBrowserRequestStreamingEnabled(true);
            }

            request.Headers.Add(FileUploadHeaderNames.Name, fileName);
            request.Headers.Add(FileUploadHeaderNames.UploadType, uploadType.ToString());
            request.Headers.Add(FileUploadHeaderNames.ContentType, mimeType);

            using var response = await httpClient.SendAsync(request, cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                string tempFileName = await response.Content.ReadAsStringAsync(cancellationToken);
                return new HttpOperationResult<(string tempFileName, string? url)>(
                    (tempFileName, response.Headers.Location?.ToString()));
            }

            return new HttpOperationResult<(string tempFileName, string? url)>(
                await httpServiceUtility.GetProblemDetailsAsync(response, cancellationToken));
        }
        catch (Exception exc) when (
            !(exc is OperationCanceledException && cancellationToken.IsCancellationRequested)
            && logger.WriteError(exc, new { fileName, uploadType, mimeType }, ignoreCancellationExceptions: false))
        {
            // Use the consuming application's existing exception type here.
            throw new InvalidOperationException("There was a problem uploading the specified file.", exc);
        }
    }
}
```

`leaveOpen: true` keeps the component-owned source alive until the component finishes
its upload lifecycle. Other callers should select ownership deliberately. The optional
content length describes remaining original-file bytes; omit it for a non-seekable
source of unknown length. The component already knows the selected file size for its
percentage calculation. Do not substitute multipart request length for that file size.

The content is single-use, including after failure or cancellation. A retry needs a new
request, new content, and a reopened source (or a correctly rewound reusable source).
Do not add automatic retries or alter timeouts in this workflow. Inspect the consuming
client's existing policies so a retry cannot silently send an exhausted stream.

## Component callback

The same mode is supported by `UmbrellaFileUpload`, `UmbrellaManagedFileUpload`, and
`UmbrellaFileImagePreviewUpload`. In a page derived from `UmbrellaComponentBase` with
`FileUploadService` injected, a minimal raw-upload example is:

```razor
<UmbrellaFileUpload ProgressMode="UmbrellaFileUploadProgressMode.HandlerReported"
                    OnRequestUpload="UploadFileToTempDirectoryAsync" />
```

```csharp
public async Task<IOperationResult?> UploadFileToTempDirectoryAsync(UmbrellaFileUploadRequestEventArgs evt)
{
    using var uploadCancellation = CancellationTokenSource.CreateLinkedTokenSource(
        CancellationToken, evt.UploadCancellationToken);

    var result = await FileUploadService.UploadAsync(evt.Content, evt.FileName,
        FileUploadType.EventImage, evt.Type,
        cancellationToken: uploadCancellation.Token, uploadProgress: evt.UploadProgress);

    // A handler can finish after cancellation; do not apply its result to the page.
    uploadCancellation.Token.ThrowIfCancellationRequested();
    // Apply successful filename/URL values to the application's model here.
    return result;
}
```

Import `Umbrella.Utilities.Primitives.Abstractions` for `IOperationResult` and
`Umbrella.AspNetCore.Blazor.Components.FileUpload` for the component types. The minimal
callback delegates error presentation to the uploader. If retaining a page's existing
catch/log/dialog handling, exclude token-confirmed cancellation using the **linked**
token and `ignoreCancellationExceptions: false`; allow expected cancellation to propagate.
Check cancellation before mutating the model or preview. Preserve replacement flags,
version tokens, and existing application result handling for image/managed uploads.

Verify progress stays at zero until a write completes, cancellation stops the upload,
and failed results never become success merely because all bytes were written. Exercise
browser streaming against the real deployment separately from unit tests.
