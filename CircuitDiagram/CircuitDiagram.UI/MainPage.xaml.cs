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
        
        // Add a wire
        var wire = new Wire(new LayoutInformation { Location = new CDPoint(100, 100), Size = 200, Orientation = Orientation.Horizontal });
        _circuit.Elements.Add(wire);

        // Add another wire
        var wire2 = new Wire(new LayoutInformation { Location = new CDPoint(300, 100), Size = 200, Orientation = Orientation.Vertical });
        _circuit.Elements.Add(wire2);

        // Setup renderer with empty lookup for now
        _lookup = new DictionaryComponentDescriptionLookup();
        _renderer = new CircuitRenderer(_lookup);
    }

    private async void OnLoadComponentClicked(object sender, EventArgs e)
    {
        try
        {
            var result = await FilePicker.Default.PickAsync(new PickOptions
            {
                PickerTitle = "Select Component XML",
                FileTypes = new FilePickerFileType(new Dictionary<DevicePlatform, IEnumerable<string>>
                {
                    { DevicePlatform.iOS, new[] { "public.xml" } },
                    { DevicePlatform.Android, new[] { "application/xml" } },
                    { DevicePlatform.WinUI, new[] { ".xml" } },
                    { DevicePlatform.macOS, new[] { "xml" } },
                    { DevicePlatform.MacCatalyst, new[] { "xml" } }
                })
            });

            if (result != null)
            {
                using var stream = await result.OpenReadAsync();
                var loader = new XmlLoader();
                loader.UseDefinitions();
                
                if (loader.Load(stream, out var description))
                {
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
                    await DisplayAlert("Error", "Failed to load component description.", "OK");
                }
            }
        }
        catch (Exception ex)
        {
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
