using Microsoft.Extensions.Hosting.WindowsServices;
using Microsoft.Extensions.Logging.EventLog;
using Rtx5090Monitoring.Plugins.Msi;
using Rtx5090Monitoring.Plugins.Msi.Endpoints;
using Rtx5090Monitoring.Plugins.Asus;
using Rtx5090Monitoring.Plugins.Asus.Endpoints;
using Rtx5090Monitoring.Plugins.WireView;
using Rtx5090Monitoring.Plugins.WireView.Endpoints;
using Rtx5090Monitoring.Service;
using Rtx5090Monitoring.Service.Endpoints;

var builder = WebApplication.CreateSlimBuilder(args);
builder.Host.UseWindowsService(options => options.ServiceName = "Rtx5090MonitoringService");

builder.Logging.ClearProviders();
if (OperatingSystem.IsWindows())
{
    builder.Logging.AddEventLog(new EventLogSettings
    {
        SourceName = "Rtx5090MonitoringService",
        Filter = (_, level) => level >= LogLevel.Warning
    });
}

if (!WindowsServiceHelpers.IsWindowsService())
{
    builder.Logging.AddConsole();
}

builder.Services.AddMonitoringCore(builder.Configuration);

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
app.MapMsiEndpoints();

await app.RunAsync();
