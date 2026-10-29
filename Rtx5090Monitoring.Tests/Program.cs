using Rtx5090Monitoring.Tests.Core;
using Rtx5090Monitoring.Tests.Infrastructure;
using Rtx5090Monitoring.Tests.Service;

var runner = new TestRunner();
if (args.Contains("--verify-msi-hardware"))
{
    await MsiHardwareVerification.RunAsync();
    return 0;
}

GenericNvidiaTests.Run(runner);
WarningEngineTests.Run(runner);
WarningRegistryTests.Run(runner);
SourceSelectionTests.Run(runner);
SensorStateStoreTests.Run(runner);
SensorAlertTests.Run(runner);
MonitoringOptionsTests.Run(runner);
SensorIdentityTests.Run(runner);
SensorStreamHubTests.Run(runner);
WireViewConfigParserTests.Run(runner);
AsusAlertTests.Run(runner);
await AsusPluginTests.RunAsync(runner);
await MsiPluginTests.RunAsync(runner);
await MsiEndpointTests.RunAsync(runner);

await WarningConfigurationTests.RunAsync(runner);
await HealthEndpointTests.RunAsync(runner);
await TestHostConfigurationTests.RunAsync(runner);
await SensorEndpointTests.RunAsync(runner);
await RuntimeConfigurationTests.RunAsync(runner);
await WireViewEndpointTests.RunAsync(runner);
await AsusEndpointTests.RunAsync(runner);
await DesktopClientTests.RunAsync(runner);

runner.PrintSummary();
return runner.FailedCount == 0 ? 0 : 1;
