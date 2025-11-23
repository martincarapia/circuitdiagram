# CircuitDiagram.UI

This is a .NET MAUI based GUI for Circuit Diagram.

## Prerequisites

- .NET 9.0 SDK
- .NET MAUI workload (`dotnet workload install maui`)
- (macOS) Xcode (for MacCatalyst/iOS targets)

## Features

- Render circuit diagrams using SkiaSharp.
- Load custom component XML files.
- Basic circuit visualization.

## How to Run

1. Open the solution in Visual Studio or VS Code.
2. Select `CircuitDiagram.UI` as the startup project.
3. Select the target platform (e.g., MacCatalyst).
4. Run the application.

## Usage

- The application starts with a default circuit containing two wires.
- Click "Load Component" to select a custom component XML file.
- The component will be loaded and placed on the canvas.

## Troubleshooting

### MacCatalyst File Picker Issues

If the "Load Component" button appears to do nothing or the file picker returns null immediately on macOS:

1. **Entitlements**: Ensure `Entitlements.plist` is correctly configured.
   - `com.apple.security.app-sandbox` should be `false` for local development to avoid strict sandbox restrictions.
   - `com.apple.security.files.user-selected.read-only` must be `true`.
2. **Project Configuration**: Ensure the `.csproj` file correctly points to the entitlements file.
   - Use forward slashes `/` for paths (e.g., `Platforms/MacCatalyst/Entitlements.plist`), as backslashes `\` can fail on macOS build systems.
3. **File Types**: macOS uses UTIs. Ensure `FilePickerFileType` includes `public.xml` and `public.content` for `DevicePlatform.macOS` and `DevicePlatform.MacCatalyst`.
