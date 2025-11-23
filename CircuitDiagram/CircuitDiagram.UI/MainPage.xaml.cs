using CircuitDiagram.Circuit;
using CircuitDiagram.Render;
using CircuitDiagram.Render.Skia;
using SkiaSharp;
using SkiaSharp.Views.Maui;
using SkiaSharp.Views.Maui.Controls;
using CircuitDiagram.Document;
using CircuitDiagram.TypeDescriptionIO.Xml;
using CircuitDiagram.TypeDescriptionIO.Xml.Extensions.Definitions;
using CircuitDiagram.TypeDescription;
using CircuitDiagram.Primitives;
using CDPoint = CircuitDiagram.Primitives.Point;
using CircuitDiagram.TypeDescriptionIO.Xml.Logging;
using Microsoft.Extensions.Logging;
using CircuitDiagram.UI.Services;

namespace CircuitDiagram.UI;

public partial class MainPage : ContentPage
{
    private CircuitDocument _circuit = null!;
    private CircuitRenderer _renderer = null!;
    private DictionaryComponentDescriptionLookup _lookup = null!;
    private ComponentService _componentService;

    public MainPage()
    {
        InitializeComponent();
        InitializeCircuit();
        _componentService = new ComponentService();
        componentsList.ItemsSource = _componentService.Components;
        LoadComponents();
    }

    private void InitializeCircuit()
    {
        // Create a simple circuit
        _circuit = new CircuitDocument();

        // Setup renderer with empty lookup for now
        _lookup = new DictionaryComponentDescriptionLookup();
        _renderer = new CircuitRenderer(_lookup);
    }

    private async void LoadComponents()
    {
        // Try to find the components directory
        // This is a hack for development environment
        var currentDir = AppDomain.CurrentDomain.BaseDirectory;
        
        // Walk up to find the solution root
        var dir = new DirectoryInfo(currentDir);
        while (dir != null && !Directory.Exists(Path.Combine(dir.FullName, "components")))
        {
            dir = dir.Parent;
        }
        
        if (dir != null)
        {
            var componentsPath = Path.Combine(dir.FullName, "components");
            await _componentService.LoadComponentsAsync(componentsPath);
        }
        else
        {
            Console.WriteLine("Could not find components directory.");
            await DisplayAlert("Error", "Could not find components directory. Please ensure the submodule is initialized.", "OK");
        }
    }

    private void OnComponentSelected(object sender, SelectionChangedEventArgs e)
    {
        var item = e.CurrentSelection.FirstOrDefault() as ComponentItem;
        if (item == null) return;
        
        var description = item.Description;

        // Add component to circuit
        var componentType = new TypeDescriptionComponentType(
            description.Metadata.GUID, 
            new Uri("http://circuit-diagram.org/components"), 
            description.ComponentName);

        // Check if we already have this description, if not add it
        // Note: DictionaryComponentDescriptionLookup doesn't expose Contains easily for TypeDescriptionComponentType without implementing it, 
        // but we can just try to add or check if we can retrieve it.
        // Actually, let's just add it. The lookup might throw if it exists? 
        // Let's check DictionaryComponentDescriptionLookup source if possible, or just try/catch or check if we can.
        // For now, I'll just add it and assume it handles duplicates or I'll check.
        
        // To be safe, let's just re-add or ignore.
        // _lookup.AddDescription(componentType, description); 
        // But wait, _lookup is DictionaryComponentDescriptionLookup.
        
        try 
        {
             _lookup.AddDescription(componentType, description);
        }
        catch (ArgumentException) 
        {
            // Already exists, ignore
        }
        
        var component = new PositionalComponent(componentType);
        component.Layout.Location = new CDPoint(100, 100); 
        _circuit.Elements.Add(component);
        
        canvasView.InvalidateSurface();
        
        // Deselect
        componentsList.SelectedItem = null;
    }

    private async void OnLoadComponentClicked(object sender, EventArgs e)
    {
        Console.WriteLine("[DEBUG] OnLoadComponentClicked started");
        try
        {
            var result = await FilePicker.Default.PickAsync(new PickOptions
            {
                PickerTitle = "Select Component XML",
                FileTypes = new FilePickerFileType(new Dictionary<DevicePlatform, IEnumerable<string>>
                {
                    { DevicePlatform.iOS, new[] { "public.xml", "public.content" } },
                    { DevicePlatform.Android, new[] { "application/xml" } },
                    { DevicePlatform.WinUI, new[] { ".xml" } },
                    { DevicePlatform.macOS, new[] { "public.xml", "public.content" } },
                    { DevicePlatform.MacCatalyst, new[] { "public.xml", "public.content" } }
                })
            });

            if (result != null)
            {
                Console.WriteLine($"[DEBUG] File picked: {result.FullPath}");
                using var stream = await result.OpenReadAsync();
                Console.WriteLine("[DEBUG] Stream opened");
                
                var loader = new XmlLoader();
                loader.UseDefinitions();
                Console.WriteLine("[DEBUG] Loader configured");
                
                var logger = new StringListLogger();
                Console.WriteLine("[DEBUG] Starting Load...");
                if (loader.Load(stream, logger, out var description))
                {
                    Console.WriteLine($"[DEBUG] Load success: {description.ComponentName}");
                    var componentType = new TypeDescriptionComponentType(
                        description.Metadata.GUID, 
                        new Uri("http://circuit-diagram.org/components"), 
                        description.ComponentName);

                    _lookup.AddDescription(componentType, description);
                    
                    // Create an instance of the component
                    var component = new PositionalComponent(componentType);
                    component.Layout.Location = new CDPoint(200, 200); // Place it somewhere visible
                    _circuit.Elements.Add(component);
                    
                    canvasView.InvalidateSurface();
                    await DisplayAlert("Success", $"Loaded component: {description.ComponentName}", "OK");
                }
                else
                {
                    Console.WriteLine("[DEBUG] Load failed");
                    var errorMsg = string.Join("\n", logger.Errors);
                    Console.WriteLine($"[DEBUG] Errors: {errorMsg}");
                    if (string.IsNullOrEmpty(errorMsg)) errorMsg = "Unknown error.";
                    await DisplayAlert("Error", $"Failed to load component description.\n{errorMsg}", "OK");
                }
            }
            else
            {
                Console.WriteLine("[DEBUG] File picking cancelled or result is null");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[DEBUG] Exception: {ex}");
            await DisplayAlert("Error", $"An error occurred: {ex.Message}", "OK");
        }
    }

    private void OnRenderClicked(object sender, EventArgs e)
    {
        canvasView.InvalidateSurface();
    }

    private void OnPaintSurface(object sender, SKPaintSurfaceEventArgs e)
    {
        var canvas = e.Surface.Canvas;
        canvas.Clear(SKColors.White);

        if (_circuit != null && _renderer != null)
        {
            using (var context = new MauiDrawingContext(canvas))
            {
                try 
                {
                    _renderer.RenderCircuit(_circuit, context);
                }
                catch (Exception ex)
                {
                    // Draw error message on canvas if rendering fails
                    var paint = new SKPaint { Color = SKColors.Red };
                    canvas.DrawText($"Error rendering: {ex.Message}", 10, 30, new SKFont(), paint);
                }
            }
        }
    }

    private PositionalComponent? _draggingComponent;
    private CDPoint _dragStartLocation;
    private SKPoint _dragStartTouch;

    private void OnTouch(object sender, SKTouchEventArgs e)
    {
        var touchPoint = new CDPoint(e.Location.X, e.Location.Y);

        switch (e.ActionType)
        {
            case SKTouchAction.Pressed:
                // Hit test
                // Simple hit test: find component closest to touch point within a threshold
                _draggingComponent = _circuit.Elements
                    .OfType<PositionalComponent>()
                    .FirstOrDefault(c => IsHit(c, touchPoint));
                
                if (_draggingComponent != null)
                {
                    _dragStartLocation = _draggingComponent.Layout.Location;
                    _dragStartTouch = e.Location;
                    e.Handled = true;
                }
                break;

            case SKTouchAction.Moved:
                if (_draggingComponent != null)
                {
                    var dx = e.Location.X - _dragStartTouch.X;
                    var dy = e.Location.Y - _dragStartTouch.Y;
                    
                    // Snap to grid (10 units)
                    var newX = _dragStartLocation.X + dx;
                    var newY = _dragStartLocation.Y + dy;
                    
                    newX = Math.Round(newX / 10.0) * 10.0;
                    newY = Math.Round(newY / 10.0) * 10.0;

                    _draggingComponent.Layout.Location = new CDPoint(newX, newY);
                    canvasView.InvalidateSurface();
                    e.Handled = true;
                }
                break;

            case SKTouchAction.Released:
            case SKTouchAction.Cancelled:
                _draggingComponent = null;
                e.Handled = true;
                break;
        }
    }

    private bool IsHit(PositionalComponent component, CDPoint point)
    {
        // Simple distance check for now
        // Most components are drawn around their location or to the right/down
        // Let's assume a hit box around the location
        var dist = Math.Sqrt(Math.Pow(component.Layout.Location.X - point.X, 2) + Math.Pow(component.Layout.Location.Y - point.Y, 2));
        return dist < 40; // 40 units radius
    }

    private void OnSearchTextChanged(object sender, TextChangedEventArgs e)
    {
        var searchText = e.NewTextValue;
        if (string.IsNullOrWhiteSpace(searchText))
        {
            componentsList.ItemsSource = _componentService.Components;
        }
        else
        {
            componentsList.ItemsSource = _componentService.Components
                .Where(c => c.Name.Contains(searchText, StringComparison.OrdinalIgnoreCase) || 
                            c.Category.Contains(searchText, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }
    }
}

public class StringListLogger : IXmlLoadLogger
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
