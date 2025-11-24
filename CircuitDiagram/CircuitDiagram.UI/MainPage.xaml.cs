using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Threading.Tasks;
using System.Collections.ObjectModel;
using System.Windows.Input;
using System.ComponentModel;
using System.Runtime.CompilerServices;
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
using Microsoft.Maui.Controls;
using Microsoft.Maui.Storage;
using Microsoft.Maui.Devices;

namespace CircuitDiagram.UI;

public partial class MainPage : ContentPage
{
    private CircuitDocument _circuit = null!;
    private CircuitRenderer _renderer = null!;
    private DictionaryComponentDescriptionLookup _lookup = null!;
    private ComponentService _componentService;

    public ObservableCollection<LayerViewModel> Layers { get; set; } = new ObservableCollection<LayerViewModel>();

    private void AddLayer(PositionalComponent component)
    {
        var layer = new LayerViewModel
        {
            Name = component.Type.CollectionItem,
            Component = component
        };
        Layers.Add(layer);
    }

    private void UpdateSelection()
    {
        _selectedComponents.Clear();
        
        foreach (var layer in _selectedLayers)
        {
            if (layer.Component != null) _selectedComponents.Add(layer.Component);
            if (layer.IsGroup) AddGroupComponentsToSelection(layer);
        }
        canvasView.InvalidateSurface();
    }

    private void AddGroupComponentsToSelection(LayerViewModel group)
    {
        foreach (var child in group.Children)
        {
            if (child.Component != null) _selectedComponents.Add(child.Component);
            if (child.IsGroup) AddGroupComponentsToSelection(child);
        }
    }

    private void DeleteLayer(PositionalComponent component)
    {
        _circuit.Elements.Remove(component);
        var layer = Layers.FirstOrDefault(l => l.Component == component);
        if (layer != null) Layers.Remove(layer);
        canvasView.InvalidateSurface();
    }

    public MainPage()
    {
        InitializeComponent();
        InitializeCircuit();
        _componentService = new ComponentService();
        componentsList.ItemsSource = _componentService.Components;
        layersList.ItemsSource = Layers;
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
        component.Layout.Size = description.MinSize;
        component.Layout.Location = new CDPoint(100, 100); 
        _circuit.Elements.Add(component);
        AddLayer(component);
        
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
                    component.Layout.Size = description.MinSize;
                    component.Layout.Location = new CDPoint(200, 200); // Place it somewhere visible
                    _circuit.Elements.Add(component);
                    AddLayer(component);
                    
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

        // Draw Grid
        DrawGrid(canvas, e.Info.Width, e.Info.Height);

        if (_circuit != null && _renderer != null)
        {
            // Use scale 2.0 to match grid size (20px vs 10 units)
            using (var context = new SkiaDrawingContext(canvas, RenderScale))
            {
                try 
                {
                    _renderer.RenderCircuit(_circuit, context);

                    // Draw selection highlight
                    if (_selectedComponents.Any())
                    {
                        var paint = new SKPaint
                        {
                            Color = SKColors.Blue,
                            Style = SKPaintStyle.Stroke,
                            StrokeWidth = 2
                        };
                        foreach (var component in _selectedComponents)
                        {
                            // Calculate bounds using BoundsDrawingContext
                            var boundsContext = new BoundsDrawingContext();
                            _renderer.RenderComponent(component, boundsContext, ignoreOffset: false);
                            var bounds = boundsContext.Bounds;

                            // Scale bounds by 2.0f as SkiaDrawingContext does
                            // Update: The canvas is already scaled by 2.0f via SkiaDrawingContext constructor.
                            // So we should NOT scale the coordinates again.
                            var rect = new SKRect(
                                (float)bounds.X, 
                                (float)bounds.Y, 
                                (float)bounds.BottomRight.X, 
                                (float)bounds.BottomRight.Y);
                            
                            // Add some padding
                            rect.Inflate(5, 5);

                            canvas.DrawRect(rect, paint);
                        }
                    }
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

    private void DrawGrid(SKCanvas canvas, int width, int height)
    {
        var paint = new SKPaint
        {
            Color = new SKColor(240, 240, 240), // Light gray
            StrokeWidth = 1,
            IsAntialias = false
        };

        int gridSize = 20; // Match snap grid size

        // Vertical lines
        for (int x = 0; x < width; x += gridSize)
        {
            canvas.DrawLine(x, 0, x, height, paint);
        }

        // Horizontal lines
        for (int y = 0; y < height; y += gridSize)
        {
            canvas.DrawLine(0, y, width, y, paint);
        }
    }

    private PositionalComponent? _draggingComponent;
    private List<PositionalComponent> _selectedComponents = new List<PositionalComponent>();
    private CDPoint _dragStartLocation;
    private SKPoint _dragStartTouch;
    private CDPoint _dragRelativeOffset;
    private CircuitDiagram.Primitives.Size _dragBoundsSize;

    private List<LayerViewModel> _selectedLayers = new List<LayerViewModel>();

    private void OnLayerSelected(object sender, SelectionChangedEventArgs e)
    {
        // Deselect previous
        foreach (var layer in _selectedLayers)
        {
            layer.IsSelected = false;
        }

        _selectedLayers = e.CurrentSelection.Cast<LayerViewModel>().ToList();
        
        // Select new
        foreach (var layer in _selectedLayers)
        {
            layer.IsSelected = true;
        }

        UpdateSelection();
    }

    private void OnDeleteSelectedLayerClicked(object sender, EventArgs e)
    {
        var layersToDelete = _selectedLayers.ToList(); // Copy
        foreach (var layer in layersToDelete)
        {
            if (layer.IsGroup)
            {
                DeleteGroup(layer);
            }
            else if (layer.Component != null)
            {
                DeleteLayer(layer.Component);
            }
        }
        _selectedLayers.Clear();
        layersList.SelectedItems = null;
    }

    private void DeleteGroup(LayerViewModel group)
    {
        // Remove children components from circuit
        foreach (var child in group.Children)
        {
            if (child.Component != null)
            {
                _circuit.Elements.Remove(child.Component);
            }
        }
        
        // Remove from UI
        if (group.IsExpanded)
        {
            foreach (var child in group.Children)
            {
                Layers.Remove(child);
            }
        }
        Layers.Remove(group);
        canvasView.InvalidateSurface();
    }

    private void OnGroupClicked(object sender, EventArgs e)
    {
        // Use _selectedLayers instead of layersList.SelectedItems
        var selectedLayers = _selectedLayers.ToList();
        if (selectedLayers == null || selectedLayers.Count < 2) return;

        // Create new group
        var group = new LayerViewModel
        {
            Name = "Folder",
            IsGroup = true,
            IsExpanded = true
        };
        // group.ToggleSelectionCommand = new Command(() => ToggleLayerSelection(group)); // Removed as we use native selection

        // Find insertion index (use the first selected item's index)
        var firstIndex = Layers.IndexOf(selectedLayers.First());
        if (firstIndex == -1) firstIndex = Layers.Count;

        // Remove selected layers from root and add to group
        foreach (var layer in selectedLayers)
        {
            Layers.Remove(layer);
            layer.Level = 1; // Increase indentation
            layer.Parent = group;
            group.Children.Add(layer);
        }

        // Insert group
        Layers.Insert(firstIndex, group);
        
        // Re-insert children after group
        int index = firstIndex + 1;
        foreach (var child in group.Children)
        {
            Layers.Insert(index++, child);
        }
        
        // Update toggle command for the group
        group.ToggleExpandCommand = new Command(() => ToggleGroup(group));
        
        // Clear selection and select the new group?
        foreach(var l in selectedLayers) l.IsSelected = false;
        group.IsSelected = true;
        UpdateSelection();
    }

    private void ToggleGroup(LayerViewModel group)
    {
        if (!group.IsGroup) return;

        group.IsExpanded = !group.IsExpanded;
        var index = Layers.IndexOf(group);
        
        if (group.IsExpanded)
        {
            // Insert children
            var insertIndex = index + 1;
            foreach (var child in group.Children)
            {
                Layers.Insert(insertIndex++, child);
            }
        }
        else
        {
            // Remove children
            foreach (var child in group.Children)
            {
                Layers.Remove(child);
            }
        }
    }

    private LayerViewModel? FindLayerForComponent(PositionalComponent component)
    {
        // Search visible layers first
        var visible = Layers.FirstOrDefault(l => l.Component == component);
        if (visible != null) return visible;
        
        // Search recursively in all root layers (Level 0)
        foreach (var layer in Layers.Where(l => l.Level == 0))
        {
            var found = FindLayerInTree(layer, component);
            if (found != null) return found;
        }
        return null;
    }

    private LayerViewModel? FindLayerInTree(LayerViewModel root, PositionalComponent component)
    {
        if (root.Component == component) return root;
        foreach (var child in root.Children)
        {
            var found = FindLayerInTree(child, component);
            if (found != null) return found;
        }
        return null;
    }

    private void ExpandPathToLayer(LayerViewModel layer)
    {
        var current = layer.Parent;
        while (current != null)
        {
            if (!current.IsExpanded)
            {
                // Expand it
                current.IsExpanded = true;
                // We need to insert children into Layers if they are not there
                // But ToggleGroup handles this logic if we call the command or replicate logic.
                // Let's replicate logic or call ToggleGroup if we can ensure state consistency.
                // Actually, ToggleGroup toggles based on current state.
                // If IsExpanded is already true (set above), ToggleGroup logic might be confused if we just call it?
                // No, ToggleGroup checks IsExpanded.
                
                // Let's just manually insert children if needed.
                // Find index of current
                var index = Layers.IndexOf(current);
                if (index != -1)
                {
                    var insertIndex = index + 1;
                    foreach (var child in current.Children)
                    {
                        if (!Layers.Contains(child))
                        {
                            Layers.Insert(insertIndex++, child);
                        }
                    }
                }
            }
            current = current.Parent;
        }
    }

    private const float RenderScale = 2.0f;

    private void OnTouch(object sender, SKTouchEventArgs e)
    {
        // Convert screen coordinates to logical coordinates
        var touchPoint = new CDPoint(e.Location.X / RenderScale, e.Location.Y / RenderScale);

        switch (e.ActionType)
        {
            case SKTouchAction.Pressed:
                // Hit test
                _draggingComponent = _circuit.Elements
                    .OfType<PositionalComponent>()
                    .Reverse()
                    .FirstOrDefault(c => IsHit(c, touchPoint));
                
                if (_draggingComponent != null)
                {
                    var layer = FindLayerForComponent(_draggingComponent);
                    if (layer != null)
                    {
                        ExpandPathToLayer(layer);
                        
                        // Single select: replace selection
                        layersList.SelectedItems = new ObservableCollection<object> { layer };
                    }

                    _dragStartLocation = _draggingComponent.Layout.Location;
                    _dragStartTouch = e.Location;

                    // Calculate relative offset for smart snapping
                    try
                    {
                        var boundsContext = new BoundsDrawingContext();
                        _renderer.RenderComponent(_draggingComponent, boundsContext, ignoreOffset: false);
                        var bounds = boundsContext.Bounds;
                        _dragRelativeOffset = new CDPoint(bounds.X - _draggingComponent.Layout.Location.X, 
                                                          bounds.Y - _draggingComponent.Layout.Location.Y);
                        _dragBoundsSize = bounds.Size;
                    }
                    catch
                    {
                        _dragRelativeOffset = new CDPoint(0, 0);
                        _dragBoundsSize = new CircuitDiagram.Primitives.Size(0, 0);
                    }

                    e.Handled = true;
                }
                else
                {
                    layersList.SelectedItems = null; // Clear selection
                }
                canvasView.InvalidateSurface();
                break;

            case SKTouchAction.Moved:
                if (_draggingComponent != null)
                {
                    var dx = (e.Location.X - _dragStartTouch.X) / RenderScale;
                    var dy = (e.Location.Y - _dragStartTouch.Y) / RenderScale;
                    
                    var rawX = _dragStartLocation.X + dx;
                    var rawY = _dragStartLocation.Y + dy;
                    
                    // Default Snap (10 units)
                    double snapX = 10.0;
                    double snapY = 10.0;
                    double phaseX = 0.0;
                    double phaseY = 0.0;

                    // Smart Snap for Large Components (> 20 units)
                    // If component is large, align its visual bounds to the grid
                    if (_dragBoundsSize.Width > 20)
                    {
                        phaseX = -_dragRelativeOffset.X;
                    }
                    
                    if (_dragBoundsSize.Height > 20)
                    {
                        phaseY = -_dragRelativeOffset.Y;
                    }

                    // Apply Snap
                    // Formula: Round((Val - Phase) / Snap) * Snap + Phase
                    var newX = Math.Round((rawX - phaseX) / snapX) * snapX + phaseX;
                    var newY = Math.Round((rawY - phaseY) / snapY) * snapY + phaseY;

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
        try
        {
            var boundsContext = new BoundsDrawingContext();
            _renderer.RenderComponent(component, boundsContext, ignoreOffset: false);
            var bounds = boundsContext.Bounds;

            // Add a small margin
            double margin = 5.0;

            return point.X >= bounds.X - margin &&
                   point.X <= bounds.X + bounds.Width + margin &&
                   point.Y >= bounds.Y - margin &&
                   point.Y <= bounds.Y + bounds.Height + margin;
        }
        catch
        {
            return false;
        }
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

    private void OnRenameEntryUnfocused(object sender, FocusEventArgs e)
    {
        if (sender is Entry entry && entry.BindingContext is LayerViewModel layer)
        {
            layer.FinishEditCommand.Execute(null);
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

public class LayerViewModel : INotifyPropertyChanged
{
    public LayerViewModel()
    {
        StartEditCommand = new Command(() => IsEditing = true);
        FinishEditCommand = new Command(() => IsEditing = false);
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    private string? _name;
    public string? Name 
    { 
        get => _name; 
        set { _name = value; OnPropertyChanged(); } 
    }

    public PositionalComponent? Component { get; set; }
    public ObservableCollection<LayerViewModel> Children { get; set; } = new ObservableCollection<LayerViewModel>();
    
    private bool _isGroup;
    public bool IsGroup 
    { 
        get => _isGroup; 
        set 
        { 
            _isGroup = value; 
            OnPropertyChanged(); 
            OnPropertyChanged(nameof(Icon)); 
            OnPropertyChanged(nameof(ExpandIcon));
        } 
    }

    private bool _isExpanded = true;
    public bool IsExpanded 
    { 
        get => _isExpanded; 
        set 
        { 
            _isExpanded = value; 
            OnPropertyChanged(); 
            OnPropertyChanged(nameof(Icon)); 
            OnPropertyChanged(nameof(ExpandIcon));
        } 
    }

    private bool _isEditing;
    public bool IsEditing
    {
        get => _isEditing;
        set
        {
            _isEditing = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(IsNotEditing));
        }
    }

    public bool IsNotEditing => !IsEditing;

    public int Level { get; set; } = 0;
    public Thickness Indentation => new Thickness(Level * 20, 0, 0, 0);
    
    public string ExpandIcon => IsGroup ? (IsExpanded ? "▼" : "▶") : " ";
    public string Icon => IsGroup ? (IsExpanded ? "📂" : "📁") : "📄";
    
    public ICommand? ToggleExpandCommand { get; set; }
    public ICommand StartEditCommand { get; private set; }
    public ICommand FinishEditCommand { get; private set; }
    public ICommand? ToggleSelectionCommand { get; set; }

    private bool _isSelected;
    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (_isSelected != value)
            {
                _isSelected = value;
                OnPropertyChanged();
            }
        }
    }

    public LayerViewModel? Parent { get; set; }
}
