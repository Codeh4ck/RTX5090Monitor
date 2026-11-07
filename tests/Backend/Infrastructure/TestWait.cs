namespace Rtx5090Monitoring.Tests.Infrastructure;

internal static class TestWait
{
    public static async Task UntilAsync(Func<Task<bool>> condition, string description)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        while (!await condition())
        {
            try
            {
                await Task.Delay(20, timeout.Token);
            }
            catch (OperationCanceledException)
            {
                throw new InvalidOperationException($"Timed out waiting for {description}.");
            }
        }
    }
}
