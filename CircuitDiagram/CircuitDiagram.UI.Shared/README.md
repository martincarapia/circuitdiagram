# Circuit Diagram Unified UI

This is the unified Blazor-based UI for Circuit Diagram. It provides a **single codebase** that works across:

- 🖥️ **Desktop** (Windows, macOS) - via MAUI Blazor Hybrid
- 📱 **Mobile** (iOS, Android) - via MAUI Blazor Hybrid  
- 🌐 **Web Browsers** - via Blazor WebAssembly

## Architecture

```
CircuitDiagram.UI.Shared/     # Shared Razor Class Library
├── Components/               # Reusable Blazor components
│   ├── CircuitEditor.razor   # Main editor component
│   ├── CircuitCanvas.razor   # SkiaSharp canvas for rendering
│   ├── ComponentPanel.razor  # Component library sidebar
│   ├── LayersPanel.razor     # Layer management
│   └── Toolbar.razor         # Top toolbar
├── Services/                 # Platform-agnostic services
│   ├── CircuitEditorService.cs  # Core circuit editing logic
│   └── IFileService.cs          # Platform abstraction for files
└── wwwroot/                  # Shared CSS styles

CircuitDiagram.UI.Hybrid/     # MAUI Blazor Hybrid (Native)
├── Services/
│   └── MauiFileService.cs    # Native file picker implementation
└── ...                       # MAUI app structure

CircuitDiagram.UI.Browser/    # Blazor WebAssembly (Web)
├── Services/
│   └── BrowserFileService.cs # Browser file API implementation
└── ...                       # Blazor WASM structure
```

## Prerequisites

- .NET 9.0 SDK
- MAUI workload (for native apps): `dotnet workload install maui`
- wasm-tools workload (for browser): `dotnet workload install wasm-tools`

## Running the App

### Web Browser (Blazor WebAssembly)

```bash
cd CircuitDiagram/CircuitDiagram.UI.Browser
dotnet run
```

Open http://localhost:5200

### Native Desktop (MAUI Hybrid)

**macOS:**
```bash
cd CircuitDiagram/CircuitDiagram.UI.Hybrid
dotnet build -f net9.0-maccatalyst
dotnet run -f net9.0-maccatalyst
```

**Windows:**
```bash
cd CircuitDiagram/CircuitDiagram.UI.Hybrid
dotnet run -f net9.0-windows10.0.19041.0
```

### Mobile (MAUI Hybrid)

**iOS Simulator:**
```bash
dotnet build -f net9.0-ios
# Use Visual Studio or Rider for deployment
```

**Android:**
```bash
dotnet build -f net9.0-android
```

## Features

- **Load Components**: Import component XML files
- **Add Components**: Click to place components on the canvas
- **Wire Mode**: Draw orthogonal wires between components
- **Selection**: Click to select, Shift+click for multi-select
- **Drag & Drop**: Move components on the canvas
- **Layers Panel**: View and manage circuit elements

## Platform Differences

| Feature | Web | Desktop | Mobile |
|---------|-----|---------|--------|
| File Picker | Browser API | Native dialog | Native dialog |
| Directory Access | ❌ | ✅ | ✅ |
| Performance | Good | Best | Good |
| Offline | With PWA | ✅ | ✅ |

## Migrating from Old UI

This unified UI replaces both:
- `CircuitDiagram.UI` (MAUI XAML-based)
- `CircuitDiagram.Web` (standalone Blazor WASM)

Both old projects can be removed once migration is complete.

## Development

### Adding New Components

1. Create component in `CircuitDiagram.UI.Shared/Components/`
2. Import in both platform projects if needed
3. Platform-specific code goes in respective Services folders

### Styling

All styles are in `CircuitDiagram.UI.Shared/wwwroot/CircuitDiagram.UI.Shared.css`
- Uses CSS variables for theming
- Dark theme by default
- Supports reduced motion preferences
- High contrast mode support
