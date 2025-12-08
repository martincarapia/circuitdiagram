using CircuitDiagram.UI.Shared.Services;
using FileResult = CircuitDiagram.UI.Shared.Services.FileResult;

namespace CircuitDiagram.UI.Native.Services;

/// <summary>
/// MAUI implementation of IFileService using native file pickers.
/// </summary>
public class MauiFileService : IFileService
{
    public bool SupportsDirectoryAccess => true;

    public async Task<FileResult?> PickFileAsync(string title, IEnumerable<string> allowedExtensions)
    {
        try
        {
            var customFileType = new FilePickerFileType(new Dictionary<DevicePlatform, IEnumerable<string>>
            {
                { DevicePlatform.iOS, allowedExtensions.Select(e => "public.xml").Distinct() },
                { DevicePlatform.Android, allowedExtensions.Select(e => "application/xml") },
                { DevicePlatform.WinUI, allowedExtensions },
                { DevicePlatform.macOS, new[] { "public.xml", "public.content" } },
                { DevicePlatform.MacCatalyst, new[] { "public.xml", "public.content" } }
            });

            var result = await FilePicker.Default.PickAsync(new PickOptions
            {
                PickerTitle = title,
                FileTypes = customFileType
            });

            if (result == null) return null;

            var stream = await result.OpenReadAsync();
            return new FileResult
            {
                FileName = result.FileName,
                Stream = stream
            };
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"File picker error: {ex.Message}");
            return null;
        }
    }

    public async Task<bool> SaveFileAsync(string suggestedName, string content, string mimeType)
    {
        try
        {
            using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(content));
            var result = await CommunityToolkit.Maui.Storage.FileSaver.Default.SaveAsync(suggestedName, stream, default);
            return result.IsSuccessful;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Save file error: {ex.Message}");
            return false;
        }
    }

    public async Task<bool> SaveFileAsync(string suggestedName, byte[] content, string mimeType)
    {
        try
        {
            using var stream = new MemoryStream(content);
            var result = await CommunityToolkit.Maui.Storage.FileSaver.Default.SaveAsync(suggestedName, stream, default);
            return result.IsSuccessful;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Save file error: {ex.Message}");
            return false;
        }
    }

    public async Task<IEnumerable<Stream>> LoadComponentsFromDirectoryAsync(string path)
    {
        var streams = new List<Stream>();
        
        if (!Directory.Exists(path))
            return streams;

        await Task.Run(() =>
        {
            var xmlFiles = Directory.GetFiles(path, "*.xml", SearchOption.AllDirectories);
            foreach (var file in xmlFiles)
            {
                try
                {
                    streams.Add(File.OpenRead(file));
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error loading {file}: {ex.Message}");
                }
            }
        });

        return streams;
    }
}
