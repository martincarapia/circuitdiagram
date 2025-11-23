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

namespace CircuitDiagram.UI;

public partial class MainPage : ContentPage
{
    private CircuitDocument _circuit = null!;
    private CircuitRenderer _renderer = null!;
    private DictionaryComponentDescriptionLookup _lookup = null!;

    public MainPage()
    {
        InitializeComponent();
        InitializeCircuit();
    }

    private void InitializeCircuit()
    {
        // Create a simple circuit
        _circuit = new CircuitDocument();

        // Setup renderer with empty lookup for now
        _lookup = new DictionaryComponentDescriptionLookup();
        _renderer = new CircuitRenderer(_lookup);
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
