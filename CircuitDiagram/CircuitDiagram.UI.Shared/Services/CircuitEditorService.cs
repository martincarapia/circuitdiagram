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

namespace CircuitDiagram.UI.Shared.Services;

/// <summary>
/// Core circuit editing service shared across all platforms.
/// Manages the circuit document, rendering, and component library.
/// </summary>
public class CircuitEditorService
{
    private CircuitDocument _circuit;
    private CircuitRenderer _renderer;
    private DictionaryComponentDescriptionLookup _lookup;
    
    public CircuitDocument Circuit => _circuit;
    public CircuitRenderer Renderer => _renderer;
    public DictionaryComponentDescriptionLookup Lookup => _lookup;
    
    public List<ComponentDescription> LoadedComponents { get; } = new();
    public List<PositionalComponent> SelectedComponents { get; } = new();
    
    public float RenderScale { get; set; } = 2.0f;
    public int GridSize { get; set; } = 20;
    
    public event Action? OnCircuitChanged;
    public event Action? OnSelectionChanged;
    public event Action? OnComponentsLoaded;

    public CircuitEditorService()
    {
        _circuit = new CircuitDocument();
        _lookup = new DictionaryComponentDescriptionLookup();
        _renderer = new CircuitRenderer(_lookup);
    }

    public void NotifyCircuitChanged() => OnCircuitChanged?.Invoke();
    public void NotifySelectionChanged() => OnSelectionChanged?.Invoke();
    public void NotifyComponentsLoaded() => OnComponentsLoaded?.Invoke();

    /// <summary>
    /// Loads a component description from an XML stream.
    /// </summary>
    public LoadComponentResult LoadComponent(Stream stream)
    {
        try
        {
            var loader = new XmlLoader();
            loader.UseDefinitions();
            
            var logger = new SimpleXmlLogger();
            if (loader.Load(stream, logger, out var description))
            {
                var componentType = new TypeDescriptionComponentType(
                    description.Metadata.GUID,
                    new Uri("http://circuit-diagram.org/components"),
                    description.ComponentName);

                try
                {
                    _lookup.AddDescription(componentType, description);
                    LoadedComponents.Add(description);
                    NotifyComponentsLoaded();
                    return LoadComponentResult.Success(description);
                }
                catch (ArgumentException)
                {
                    // Already loaded - still considered success
                    return LoadComponentResult.Success(description);
                }
            }
            
            return LoadComponentResult.Failure(string.Join("\n", logger.Errors));
        }
        catch (Exception ex)
        {
            return LoadComponentResult.Failure(ex.Message);
        }
    }

    /// <summary>
    /// Adds a component instance to the circuit at the specified position.
    /// </summary>
    public PositionalComponent AddComponent(ComponentDescription description, double x, double y)
    {
        var componentType = new TypeDescriptionComponentType(
            description.Metadata.GUID,
            new Uri("http://circuit-diagram.org/components"),
            description.ComponentName);

        // Ensure it's in the lookup
        try { _lookup.AddDescription(componentType, description); }
        catch (ArgumentException) { /* Already exists */ }

        var component = new PositionalComponent(componentType);
        component.Layout.Size = description.MinSize;
        component.Layout.Location = SnapToGrid(new CDPoint(x, y));
        _circuit.Elements.Add(component);
        
        NotifyCircuitChanged();
        return component;
    }

    /// <summary>
    /// Removes a component from the circuit.
    /// </summary>
    public void RemoveComponent(PositionalComponent component)
    {
        _circuit.Elements.Remove(component);
        SelectedComponents.Remove(component);
        NotifyCircuitChanged();
    }

    /// <summary>
    /// Adds a wire between two points, creating orthogonal segments.
    /// </summary>
    public void AddWire(CDPoint start, CDPoint end)
    {
        var snappedStart = SnapToGrid(start);
        var snappedEnd = SnapToGrid(end);
        
        var wires = CalculateWireSegments(snappedStart, snappedEnd);
        foreach (var wire in wires)
        {
            _circuit.Elements.Add(wire);
        }
        
        NotifyCircuitChanged();
    }

    /// <summary>
    /// Calculate wire segments for orthogonal routing.
    /// </summary>
    private List<Wire> CalculateWireSegments(CDPoint start, CDPoint end)
    {
        var wires = new List<Wire>();
        
        // Horizontal segment first
        if (Math.Abs(end.X - start.X) > 0.1)
        {
            var layout = new LayoutInformation
            {
                Location = start,
                Size = end.X - start.X,
                Orientation = Orientation.Horizontal
            };
            wires.Add(new Wire(layout));
        }
        
        // Vertical segment
        if (Math.Abs(end.Y - start.Y) > 0.1)
        {
            var layout = new LayoutInformation
            {
                Location = new CDPoint(end.X, start.Y),
                Size = end.Y - start.Y,
                Orientation = Orientation.Vertical
            };
            wires.Add(new Wire(layout));
        }
        
        return wires;
    }

    /// <summary>
    /// Moves a component to a new position.
    /// </summary>
    public void MoveComponent(PositionalComponent component, CDPoint newLocation)
    {
        component.Layout.Location = SnapToGrid(newLocation);
        NotifyCircuitChanged();
    }

    /// <summary>
    /// Updates selection state.
    /// </summary>
    public void SetSelection(IEnumerable<PositionalComponent> components)
    {
        SelectedComponents.Clear();
        SelectedComponents.AddRange(components);
        NotifySelectionChanged();
    }

    public void ClearSelection()
    {
        SelectedComponents.Clear();
        NotifySelectionChanged();
    }

    public void SelectComponent(PositionalComponent component, bool addToSelection = false)
    {
        if (!addToSelection)
            SelectedComponents.Clear();
        
        if (!SelectedComponents.Contains(component))
            SelectedComponents.Add(component);
        
        NotifySelectionChanged();
    }

    /// <summary>
    /// Clears the entire circuit.
    /// </summary>
    public void ClearCircuit()
    {
        _circuit = new CircuitDocument();
        SelectedComponents.Clear();
        NotifyCircuitChanged();
        NotifySelectionChanged();
    }

    /// <summary>
    /// Snaps a point to the grid.
    /// </summary>
    public CDPoint SnapToGrid(CDPoint point)
    {
        double snapSize = GridSize / RenderScale;
        return new CDPoint(
            Math.Round(point.X / snapSize) * snapSize,
            Math.Round(point.Y / snapSize) * snapSize
        );
    }

    /// <summary>
    /// Renders the circuit to a Skia canvas.
    /// </summary>
    public void RenderToCanvas(SKCanvas canvas, int width, int height, bool showGrid = true)
    {
        canvas.Clear(SKColors.White);
        
        if (showGrid)
            DrawGrid(canvas, width, height);

        using var context = new SkiaDrawingContext(canvas, RenderScale);
        
        try
        {
            _renderer.RenderCircuit(_circuit, context);
            DrawSelection(canvas);
        }
        catch (Exception ex)
        {
            using var paint = new SKPaint { Color = SKColors.Red };
            canvas.DrawText($"Render error: {ex.Message}", 10, 30, new SKFont(), paint);
        }
    }

    private void DrawGrid(SKCanvas canvas, int width, int height)
    {
        using var paint = new SKPaint
        {
            Color = new SKColor(240, 240, 240),
            StrokeWidth = 1,
            IsAntialias = false
        };

        for (int x = 0; x < width; x += GridSize)
            canvas.DrawLine(x, 0, x, height, paint);

        for (int y = 0; y < height; y += GridSize)
            canvas.DrawLine(0, y, width, y, paint);
    }

    private void DrawSelection(SKCanvas canvas)
    {
        if (!SelectedComponents.Any()) return;

        using var paint = new SKPaint
        {
            Color = SKColors.Blue,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 2
        };

        foreach (var component in SelectedComponents)
        {
            var boundsContext = new BoundsDrawingContext();
            _renderer.RenderComponent(component, boundsContext, ignoreOffset: false);
            var bounds = boundsContext.Bounds;

            var rect = new SKRect(
                (float)bounds.X * RenderScale,
                (float)bounds.Y * RenderScale,
                (float)bounds.BottomRight.X * RenderScale,
                (float)bounds.BottomRight.Y * RenderScale);
            
            rect.Inflate(5, 5);
            canvas.DrawRect(rect, paint);
        }
    }

    /// <summary>
    /// Hit test to find component at a point.
    /// </summary>
    public PositionalComponent? HitTest(CDPoint point)
    {
        // Convert screen coordinates to circuit coordinates
        var circuitPoint = new CDPoint(point.X / RenderScale, point.Y / RenderScale);
        
        foreach (var element in _circuit.Elements.OfType<PositionalComponent>().Reverse())
        {
            var boundsContext = new BoundsDrawingContext();
            _renderer.RenderComponent(element, boundsContext, ignoreOffset: false);
            var bounds = boundsContext.Bounds;
            
            // Expand bounds slightly for easier selection
            var rect = new Rect(bounds.X - 5, bounds.Y - 5, bounds.Width + 10, bounds.Height + 10);
            
            if (circuitPoint.X >= rect.X && circuitPoint.X <= rect.X + rect.Width &&
                circuitPoint.Y >= rect.Y && circuitPoint.Y <= rect.Y + rect.Height)
            {
                return element;
            }
        }
        
        return null;
    }

    /// <summary>
    /// Gets all circuit elements for display in layers panel.
    /// </summary>
    public IEnumerable<IElement> GetElements() => _circuit.Elements;
}

/// <summary>
/// Result of loading a component.
/// </summary>
public record LoadComponentResult(bool IsSuccess, ComponentDescription? Description, string? Error)
{
    public static LoadComponentResult Success(ComponentDescription description) 
        => new(true, description, null);
    
    public static LoadComponentResult Failure(string error) 
        => new(false, null, error);
}

/// <summary>
/// Simple XML logger for component loading.
/// </summary>
internal class SimpleXmlLogger : IXmlLoadLogger
{
    public List<string> Errors { get; } = new();

    public void Log(LogLevel level, FileRange position, string message, Exception? innerException)
    {
        var msg = position.StartLine > 0 ? $"Line {position.StartLine}: {message}" : message;
        if (level >= LogLevel.Warning)
            Errors.Add(msg);
    }
}
