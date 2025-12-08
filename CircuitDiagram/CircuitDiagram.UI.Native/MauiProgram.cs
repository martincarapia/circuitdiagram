using CircuitDiagram.UI.Native.Services;
using CircuitDiagram.UI.Shared.Services;
using CommunityToolkit.Maui;
using Microsoft.Extensions.Logging;

namespace CircuitDiagram.UI.Native;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .UseMauiCommunityToolkit()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
            });

        builder.Services.AddMauiBlazorWebView();

#if DEBUG
        builder.Services.AddBlazorWebViewDeveloperTools();
        builder.Logging.AddDebug();
#endif

        // Register shared services
        builder.Services.AddSingleton<CircuitEditorService>();
        
        // Register platform-specific services
        builder.Services.AddSingleton<IFileService, MauiFileService>();

        return builder.Build();
    }
}
