namespace Rtx5090Monitoring.Service.Services;

internal sealed class ServiceRestart : IServiceRestart
{
    public void RequestRestart() => _ = Task.Run(async () =>
    {
        await Task.Delay(500);
        Environment.Exit(1);
    });
}
