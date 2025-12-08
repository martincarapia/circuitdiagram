using CircuitDiagram.Circuit;
using CircuitDiagram.Document;
using CircuitDiagram.Primitives;
using CircuitDiagram.Render;
using CircuitDiagram.Render.Skia;
using CircuitDiagram.TypeDescription;
using CircuitDiagram.TypeDescriptionIO.Xml;
using CircuitDiagram.TypeDescriptionIO.Xml.Extensions.Definitions;
using CircuitDiagram.TypeDescriptionIO.Xml.Logging;
using Microsoft.Extensions.Logging;
using SkiaSharp;
using CDPoint = CircuitDiagram.Primitives.Point;

namespace CircuitDiagram.Web.Services;

public class CircuitService
{
    public CircuitDocument Circuit { get; private set; }
    public CircuitRenderer Renderer { get; private set; }
    public DictionaryComponentDescriptionLookup Lookup { get; private set; }
    public List<ComponentDescription> LoadedComponents { get; } = new();
    
    public event Action? OnCircuitChanged;

    public CircuitService()
    {
        Circuit = new CircuitDocument();
        Lookup = new DictionaryComponentDescriptionLookup();
        Renderer = new CircuitRenderer(Lookup);
    }

    public void NotifyCircuitChanged()
    {
        OnCircuitChanged?.Invoke();
    }

    public bool LoadComponentFromStream(Stream stream, out string? componentName, out string? errorMessage)
    {
        componentName = null;
        errorMessage = null;

        try
        {
            var loader = new XmlLoader();
            loader.UseDefinitions();
            
            var logger = new StringListLogger();
            if (loader.Load(stream, logger, out var description))
            {
                var componentType = new TypeDescriptionComponentType(
                    description.Metadata.GUID,
                    new Uri("http://circuit-diagram.org/components"),
                    description.ComponentName);

                try
                {
                    Lookup.AddDescription(componentType, description);
                    LoadedComponents.Add(description);
                    componentName = description.ComponentName;
                    return true;
                }
                catch (ArgumentException)
                {
                    // Already exists
                    componentName = description.ComponentName;
                    return true;
                }
            }
            else
            {
                errorMessage = string.Join("\n", logger.Errors);
                if (string.IsNullOrEmpty(errorMessage))
                    errorMessage = "Unknown error loading component.";
                return false;
            }
        }
        catch (Exception ex)
        {
            errorMessage = ex.Message;
            return false;
        }
    }

    public void AddComponentToCircuit(ComponentDescription description, double x = 100, double y = 100)
    {
        var componentType = new TypeDescriptionComponentType(
            description.Metadata.GUID,
            new Uri("http://circuit-diagram.org/components"),
            description.ComponentName);

        var component = new PositionalComponent(componentType);
        component.Layout.Size = description.MinSize;
        component.Layout.Location = new CDPoint(x, y);
        Circuit.Elements.Add(component);
        
        NotifyCircuitChanged();
    }

    public void RemoveComponent(PositionalComponent component)
    {
        Circuit.Elements.Remove(component);
        NotifyCircuitChanged();
    }

    public void AddWire(CDPoint start, CDPoint end)
    {
        var layout = new LayoutInformation();
        layout.Location = start;
        layout.Size = end.X - start.X; // Use horizontal distance as size for now
        var wire = new Wire(layout);
        Circuit.Elements.Add(wire);
        NotifyCircuitChanged();
    }

    public void RenderToCanvas(SKCanvas canvas, int width, int height)
    {
        canvas.Clear(SKColors.White);
        
        // Draw grid
        DrawGrid(canvas, width, height);

        // Render circuit
        var context = new SkiaDrawingContext(canvas);
        Renderer.RenderCircuit(Circuit, context);
    }

    private void DrawGrid(SKCanvas canvas, int width, int height)
    {
        const int gridSize = 10;
        
        using var paint = new SKPaint
        {
            Color = new SKColor(230, 230, 230),
            StrokeWidth = 1,
            IsAntialias = false
        };

        // Draw vertical lines
        for (int x = 0; x < width; x += gridSize)
        {
            canvas.DrawLine(x, 0, x, height, paint);
        }

        // Draw horizontal lines
        for (int y = 0; y < height; y += gridSize)
        {
            canvas.DrawLine(0, y, width, y, paint);
        }
    }

    public void ClearCircuit()
    {
        Circuit = new CircuitDocument();
        NotifyCircuitChanged();
    }
}

// Simple logger for component loading
public class StringListLogger : IXmlLoadLogger
{
    public List<string> Errors { get; } = new();
    public List<string> Warnings { get; } = new();

    public void Log(LogLevel level, FileRange position, string message, Exception? innerException)
    {
        var msg = position.StartLine > 0 ? $"Line {position.StartLine}: {message}" : message;
        if (level == LogLevel.Error)
            Errors.Add(msg);
        else if (level == LogLevel.Warning)
            Warnings.Add(msg);
    }
}
