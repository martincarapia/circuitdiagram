namespace CircuitDiagram.UI.Shared.Services;

/// <summary>
/// Platform abstraction for file operations.
/// Implementations differ between MAUI (native file pickers) and Browser (JS file API).
/// </summary>
public interface IFileService
{
    /// <summary>
    /// Opens a file picker and returns the selected file's content as a stream.
    /// </summary>
    Task<FileResult?> PickFileAsync(string title, IEnumerable<string> allowedExtensions);
    
    /// <summary>
    /// Saves content to a file using the platform's save dialog.
    /// </summary>
    Task<bool> SaveFileAsync(string suggestedName, string content, string mimeType);
    
    /// <summary>
    /// Saves binary content to a file.
    /// </summary>
    Task<bool> SaveFileAsync(string suggestedName, byte[] content, string mimeType);
    
    /// <summary>
    /// Loads components from a directory path (native only, may not work on browser).
    /// </summary>
    Task<IEnumerable<Stream>> LoadComponentsFromDirectoryAsync(string path);
    
    /// <summary>
    /// Whether this platform supports directory access.
    /// </summary>
    bool SupportsDirectoryAccess { get; }
}

/// <summary>
/// Result of picking a file.
/// </summary>
public class FileResult
{
    public required string FileName { get; init; }
    public required Stream Stream { get; init; }
}
