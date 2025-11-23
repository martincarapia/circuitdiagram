using CircuitDiagram.TypeDescription;
using CircuitDiagram.TypeDescriptionIO.Xml;
using CircuitDiagram.TypeDescriptionIO.Xml.Extensions.Definitions;
using CircuitDiagram.TypeDescriptionIO.Xml.Logging;
using Microsoft.Extensions.Logging;
using System.Collections.ObjectModel;

namespace CircuitDiagram.UI.Services;

public class ComponentService
{
    public ObservableCollection<ComponentItem> Components { get; } = new();
    
    public async Task LoadComponentsAsync(string rootPath)
    {
        Components.Clear();
        if (!Directory.Exists(rootPath))
        {
            Console.WriteLine($"Directory not found: {rootPath}");
            return;
        }

        // Run on background thread to avoid blocking UI
        await Task.Run(() => 
        {
            var xmlFiles = Directory.GetFiles(rootPath, "*.xml", SearchOption.AllDirectories);
            
            foreach (var file in xmlFiles)
            {
                try 
                {
                    using var stream = File.OpenRead(file);
                    var loader = new XmlLoader();
                    loader.UseDefinitions();
                    var logger = new ServiceLogger();
                    
                    if (loader.Load(stream, logger, out var description))
                    {
                        // Infer category from path
                        // Expected: .../components/{Category}/{ComponentFolder}/{file.xml}
                        // or .../components/{Category}/{file.xml}
                        
                        string category = "Unknown";
                        var fileInfo = new FileInfo(file);
                        var parent = fileInfo.Directory;
                        
                        // Check if parent is directly under rootPath
                        // We need to find the relative path from rootPath
                        var relativePath = Path.GetRelativePath(rootPath, file);
                        var parts = relativePath.Split(Path.DirectorySeparatorChar);
                        
                        if (parts.Length >= 2)
                        {
                            category = parts[0];
                        }

                        var item = new ComponentItem 
                        { 
                            Description = description,
                            Category = category
                        };

                        // Marshal back to UI thread if needed, but ObservableCollection usually needs UI thread
                        MainThread.BeginInvokeOnMainThread(() => 
                        {
                            Components.Add(item);
                        });
                    }
                    else
                    {
                        Console.WriteLine($"Failed to load {file}: {string.Join(", ", logger.Errors)}");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error loading {file}: {ex.Message}");
                }
            }
        });
    }

    private class ServiceLogger : IXmlLoadLogger
    {
        public List<string> Errors { get; } = new List<string>();

        public void Log(LogLevel level, FileRange position, string message, Exception innerException)
        {
            if (level >= LogLevel.Warning)
            {
                Errors.Add($"{level}: {message} {innerException?.Message}");
            }
        }
    }
}

public class ComponentItem
{
    public ComponentDescription Description { get; set; } = new();
    public string Category { get; set; } = string.Empty;
    public string Name => Description?.ComponentName ?? "Unknown";
}
