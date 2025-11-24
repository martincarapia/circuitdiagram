using CircuitDiagram.Circuit;
using CircuitDiagram.Primitives;
using CircuitDiagram.Render;
using CircuitDiagram.Render.Skia;
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
        
        // Create a lookup for the renderer
        var lookup = new DictionaryComponentDescriptionLookup();
        var type = new TypeDescriptionComponentType(description.Metadata.GUID, new Uri("http://circuit-diagram.org/components"), description.ComponentName);
        lookup.AddDescription(type, description);
        
        var renderer = new CircuitRenderer(lookup);
        
        var component = new PositionalComponent(type);
        component.Layout.Size = description.MinSize;
        component.Layout.Location = new CDPoint(0, 0); // Start at 0,0 for measurement

        // 1. Measure the component
        var boundsContext = new BoundsDrawingContext();
        renderer.RenderComponent(component, boundsContext, ignoreOffset: false);
        var bounds = boundsContext.Bounds;

        // 2. Calculate scale to fit
        // We want to fit the component into the 60x60 box with some padding
        float padding = 5.0f;
        float availableWidth = width - (padding * 2);
        float availableHeight = height - (padding * 2);

        // Calculate the visual size (assuming 1.0 scale for now)
        float visualWidth = (float)bounds.Width;
        float visualHeight = (float)bounds.Height;

        // If bounds are empty (e.g. invisible component), default to something reasonable
        if (visualWidth <= 0) visualWidth = 10;
        if (visualHeight <= 0) visualHeight = 10;

        // Determine scale factor
        // We want to scale so that the largest dimension fits
        float scaleX = availableWidth / visualWidth;
        float scaleY = availableHeight / visualHeight;
        float scale = Math.Min(scaleX, scaleY);

        // Cap the scale so small components don't look huge
        // Also ensure we don't scale up too much if the component is tiny
        scale = Math.Min(scale, 2.0f); 

        // 3. Calculate position to center
        // We need to translate the canvas so that the center of the component aligns with the center of the image
        
        // Center of the component in its own local space
        float compCenterX = (float)(bounds.X + bounds.Width / 2.0);
        float compCenterY = (float)(bounds.Y + bounds.Height / 2.0);

        // Center of the image
        float imgCenterX = width / 2.0f;
        float imgCenterY = height / 2.0f;

        // We want: (compCenter * scale) + translate = imgCenter
        // translate = imgCenter - (compCenter * scale)
        float translateX = imgCenterX - (compCenterX * scale);
        float translateY = imgCenterY - (compCenterY * scale);

        // 4. Render
        canvas.Clear(SKColors.Transparent);
        canvas.Translate(translateX, translateY);
        
        using (var context = new SkiaDrawingContext(canvas, scale))
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
