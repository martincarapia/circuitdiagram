using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using CircuitDiagram.UI.Browser;
using CircuitDiagram.UI.Browser.Services;
using CircuitDiagram.UI.Shared.Services;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });

// Register shared services
builder.Services.AddSingleton<CircuitEditorService>();

// Register browser-specific services
builder.Services.AddScoped<IFileService, BrowserFileService>();

await builder.Build().RunAsync();
