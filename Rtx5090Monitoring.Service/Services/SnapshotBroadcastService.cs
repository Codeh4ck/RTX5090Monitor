using System.Text.Json;
using Rtx5090Monitoring.Core;
using Rtx5090Monitoring.Core.Configuration;
using Rtx5090Monitoring.Core.Streaming;
using Rtx5090Monitoring.Core.Utilities;
using Rtx5090Monitoring.Warnings;

namespace Rtx5090Monitoring.Service.Services;

internal sealed class SnapshotBroadcastService(IRuntimeConfiguration configuration, TimeProvider clock,
    SensorStateStore store, SensorStreamHub hub, Rtx5090Monitoring.Core.Plugins.PluginRuntimeManager runtime) : BackgroundService
{
    private readonly AsyncSignal _wake = new();
    private readonly WarningEngine _warnings = new(runtime.WarningRegistry);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        configuration.Changed += _wake.Set;
        try
        {
            DateTimeOffset? lastPublication = null;
            long publishedWarnings = -1;
            long publishedConfiguration = -1;
            while (!stoppingToken.IsCancellationRequested)
            {
                var current = configuration.Current;
                var settings = current.Configuration.Monitoring;
                var now = clock.GetUtcNow();
                var snapshot = store.Snapshot(now, settings.Selections) with { ConfigurationRevision = current.Revision };
                snapshot = runtime.ConnectorProfiles.Resolve(snapshot, current.Configuration);
                snapshot = snapshot with { Warnings = _warnings.Evaluate(snapshot, current.Configuration, now) };
                if (lastPublication is null || now - lastPublication >= TimeSpan.FromMilliseconds(settings.StreamIntervalMs)
                    || publishedWarnings != _warnings.Revision || publishedConfiguration != current.Revision)
                {
                    hub.Publish(JsonSerializer.SerializeToUtf8Bytes(snapshot, AppJsonContext.Default.SensorSnapshot));
                    lastPublication = now;
                    publishedWarnings = _warnings.Revision;
                    publishedConfiguration = current.Revision;
                }
                await _wake.WaitAsync(Math.Min(settings.StreamIntervalMs, 250), stoppingToken);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Normal host shutdown.
        }
        finally
        {
            configuration.Changed -= _wake.Set;
        }
    }
}
