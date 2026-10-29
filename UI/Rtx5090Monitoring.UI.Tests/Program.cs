using Rtx5090Monitoring.Tests.Infrastructure;
using Rtx5090Monitoring.UI.Tests.Configuration;
using Rtx5090Monitoring.UI.Tests.Settings;
using Rtx5090Monitoring.UI.Tests.Streaming;
using Rtx5090Monitoring.UI.Tests.Telemetry;

if (args is ["--verify-release", var directory])
{
    Rtx5090Monitoring.UI.Tests.BuildVerification.ReleaseBuildVerification.Run(directory);
    return 0;
}

var runner = new TestRunner();
TelemetryTests.Run(runner);
HardwarePanelTests.Run(runner);
WireViewPanelTests.Run(runner);
WarningDeliveryTests.Run(runner);
MsiPanelTests.Run(runner);
PsuComparisonTests.Run(runner);
DirtyTrackingTests.Run(runner);
DeviceRefreshTests.Run(runner);
GenericNvidiaEditorTests.Run(runner);
await EditorTests.RunAsync(runner);
await LegacyConfigurationTests.RunAsync(runner);
await PreferenceTests.RunAsync(runner);
await Rtx5090Monitoring.UI.Tests.Updates.UpdateTests.RunAsync(runner);
await SessionTests.RunAsync(runner);
await FrameReaderTests.RunAsync(runner);
#if TELEMETRY_DEBUG
await Rtx5090Monitoring.UI.Tests.Debugging.DebugSessionTests.RunAsync(runner);
#endif
runner.PrintSummary();
return runner.FailedCount == 0 ? 0 : 1;
