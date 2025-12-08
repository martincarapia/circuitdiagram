# CircuitDiagram.Web

Blazor WebAssembly version of Circuit Diagram that runs in the browser.

## Prerequisites

- .NET 9.0 SDK

## How to Run

```bash
cd CircuitDiagram/CircuitDiagram.Web
dotnet run
```

Then open your browser to `http://localhost:5184`

## Features

- **Load Components**: Click "Load XML" to load component definition files (`.xml`)
- **Add Components**: Double-click a loaded component to add it to the circuit, or single-click to select and then click on the canvas
- **Wire Mode**: Toggle wire mode to draw connections between components
- **Clear Circuit**: Reset the canvas

## Architecture

This project reuses the existing core libraries:

- `CircuitDiagramCore` - Core circuit models
- `CircuitDiagram.Render` - Rendering abstractions
- `CircuitDiagram.Render.Skia` - SkiaSharp-based rendering
- `CircuitDiagram.Document` - Document format handling
- `CircuitDiagram.TypeDescriptionIO` - Component XML parsing

The rendering uses **SkiaSharp.Views.Blazor** which compiles SkiaSharp to WebAssembly.

## Deployment

To publish for static hosting (e.g., GitHub Pages, Azure Static Web Apps):

```bash
dotnet publish -c Release -o publish
```

The output in `publish/wwwroot` can be deployed to any static file host.

## Limitations

- File system access is limited to browser file picker
- Cannot auto-load components directory (must load files manually)
- Performance may vary compared to native app
