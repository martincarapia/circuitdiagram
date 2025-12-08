using CircuitDiagram.UI.Shared.Services;
using Microsoft.JSInterop;
using FileResult = CircuitDiagram.UI.Shared.Services.FileResult;

namespace CircuitDiagram.UI.Browser.Services;

/// <summary>
/// Browser implementation of IFileService using JavaScript interop.
/// </summary>
public class BrowserFileService : IFileService
{
    private readonly IJSRuntime _js;

    public BrowserFileService(IJSRuntime js)
    {
        _js = js;
    }

    public bool SupportsDirectoryAccess => false;

    public Task<FileResult?> PickFileAsync(string title, IEnumerable<string> allowedExtensions)
    {
        // File picking in browser is handled by InputFile component
        // This method is here for interface compliance but browser uses different flow
        return Task.FromResult<FileResult?>(null);
    }

    public async Task<bool> SaveFileAsync(string suggestedName, string content, string mimeType)
    {
        try
        {
            await _js.InvokeVoidAsync("downloadFile", suggestedName, content, mimeType);
            return true;
        }
        catch
        {
            return false;
        }
    }

    public async Task<bool> SaveFileAsync(string suggestedName, byte[] content, string mimeType)
    {
        try
        {
            var base64 = Convert.ToBase64String(content);
            await _js.InvokeVoidAsync("downloadFileFromBase64", suggestedName, base64, mimeType);
            return true;
        }
        catch
        {
            return false;
        }
    }

    public Task<IEnumerable<Stream>> LoadComponentsFromDirectoryAsync(string path)
    {
        // Directory access not supported in browser
        return Task.FromResult<IEnumerable<Stream>>(Array.Empty<Stream>());
    }
}
