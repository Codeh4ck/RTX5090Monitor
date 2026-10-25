using System.Diagnostics;

namespace Rtx5090Monitoring.Plugins.WireView.Services;

internal sealed class WireViewApplicationWatcher : IDisposable
{
    private readonly CancellationTokenSource _stop = new();
    private readonly Task _worker;

    public WireViewApplicationWatcher(Action requestYield)
    {
        _worker = Task.Run(() => WatchAsync(requestYield));
    }

    private async Task WatchAsync(Action requestYield)
    {
        var startupDeadline = DateTimeOffset.UtcNow.AddSeconds(30);
        var wasRunning = false;

        try
        {
            while (!_stop.IsCancellationRequested)
            {
                var isRunning = IsApplicationRunning();

                if (isRunning && !wasRunning)
                {
                    requestYield();
                }

                wasRunning = isRunning;
                var interval = DateTimeOffset.UtcNow < startupDeadline ? 500 : 1500;
                await Task.Delay(interval, _stop.Token);
            }
        }
        catch (OperationCanceledException) when (_stop.IsCancellationRequested)
        {
            // The plugin was disabled or the host stopped.
        }
    }

    public void Dispose()
    {
        _stop.Cancel();
        _worker.GetAwaiter().GetResult();
        _stop.Dispose();
    }

    private static bool IsApplicationRunning()
    {
        try
        {
            var processes = Process.GetProcessesByName("WireView2");
            var running = processes.Length > 0;
            foreach (var process in processes)
            {
                process.Dispose();
            }

            return running;
        }
        catch
        {
            return false;
        }
    }
}
