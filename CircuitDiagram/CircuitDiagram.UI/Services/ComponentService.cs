using CircuitDiagram.Circuit;
using CircuitDiagram.Primitives;
using CircuitDiagram.Render;
using CircuitDiagram.TypeDescription;
using SkiaSharp;
using CDPoint = CircuitDiagram.Primitives.Point;
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

                        // Generate Preview
                        try 
                        {
                            item.Preview = GeneratePreview(description);
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"Error generating preview for {description.ComponentName}: {ex.Message}");
                        }

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

    private ImageSource GeneratePreview(ComponentDescription description)
    {
        int width = 60;
        int height = 60;
        
        using var surface = SKSurface.Create(new SKImageInfo(width, height));
        var canvas = surface.Canvas;
        // Use transparent background, but maybe white is better for visibility if lines are black
        // canvas.Clear(SKColors.Transparent); 
        
        // Create a lookup for the renderer
        var lookup = new DictionaryComponentDescriptionLookup();
        var type = new TypeDescriptionComponentType(description.Metadata.GUID, new Uri("http://circuit-diagram.org/components"), description.ComponentName);
        lookup.AddDescription(type, description);
        
        var renderer = new CircuitRenderer(lookup);
        
        var component = new PositionalComponent(type);
        // Center the component roughly. 
        // Most components are drawn relative to (0,0) or centered.
        // Let's put it at center.
        component.Layout.Location = new CDPoint(width / 2, height / 2);
        
        using (var context = new MauiDrawingContext(canvas))
        {
            context.Color = SKColors.Black;
            renderer.RenderComponent(component, context, ignoreOffset: false);
        }
        
        using var image = surface.Snapshot();
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        var bytes = data.ToArray();
        return ImageSource.FromStream(() => new MemoryStream(bytes));
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
    public ImageSource? Preview { get; set; }
}
