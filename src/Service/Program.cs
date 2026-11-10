using Microsoft.Extensions.Hosting.WindowsServices;
using Microsoft.Extensions.Logging.EventLog;
using RTXLens.Plugins.Msi;
using RTXLens.Plugins.GenericNvidia;
using RTXLens.Plugins.GenericNvidia.Endpoints;
using RTXLens.Plugins.Msi.Endpoints;
using RTXLens.Plugins.Asus;
using RTXLens.Plugins.Asus.Endpoints;
using RTXLens.Plugins.WireView;
using RTXLens.Plugins.WireView.Endpoints;
using RTXLens.Service;
using RTXLens.Service.Endpoints;

var builder = WebApplication.CreateSlimBuilder(args);
builder.Host.UseWindowsService(options => options.ServiceName = "RTXLensService");

builder.Logging.ClearProviders();
if (OperatingSystem.IsWindows())
{
    builder.Logging.AddEventLog(new EventLogSettings
    {
        SourceName = "RTXLensService",
        Filter = (_, level) => level >= LogLevel.Warning
    });
}

if (!WindowsServiceHelpers.IsWindowsService())
{
    builder.Logging.AddConsole();
}

builder.Services.AddMonitoringCore(builder.Configuration);

builder.Services.AddGenericNvidiaPlugin();
builder.Services.AddWireViewPlugin();
builder.Services.AddAsusPlugin();
builder.Services.AddMsiPlugin();

builder.Services.AddCors(options => options.AddDefaultPolicy(policy => policy.AllowAnyOrigin().WithMethods("GET", "POST", "PUT").AllowAnyHeader()));

builder.WebHost.ConfigureKestrel(options => options.ListenLocalhost(59273));

await using var app = builder.Build();

app.UseCors().UseWebSockets();
app.MapMonitoringEndpoints();
app.MapWireViewEndpoints();
app.MapAsusEndpoints();
app.MapGenericNvidiaEndpoints();
app.MapMsiEndpoints();

await app.RunAsync();
