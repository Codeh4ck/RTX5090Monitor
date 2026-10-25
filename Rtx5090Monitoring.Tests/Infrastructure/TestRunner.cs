namespace Rtx5090Monitoring.Tests.Infrastructure;

internal sealed class TestRunner
{
    public int PassedCount { get; private set; }
    public int FailedCount { get; private set; }

    public void Run(Action test)
    {
        var name = $"{test.Method.DeclaringType?.Name}.{test.Method.Name}";

        try
        {
            test();
            ReportSuccess(name);
        }
        catch (Exception exception)
        {
            ReportFailure(name, exception);
        }
    }

    public async Task RunAsync(Func<Task> test)
    {
        var name = $"{test.Method.DeclaringType?.Name}.{test.Method.Name}";
        await RunAsync(name, test);
    }

    public async Task RunAsync(string name, Func<Task> test)
    {
        try
        {
            await test();
            ReportSuccess(name);
        }
        catch (Exception exception)
        {
            ReportFailure(name, exception);
        }
    }

    public void PrintSummary()
    {
        Console.WriteLine();
        Console.WriteLine($"{PassedCount} tests passed, {FailedCount} failed.");
    }

    private void ReportSuccess(string name)
    {
        PassedCount++;
        Console.WriteLine($"PASS: {name}");
    }

    private void ReportFailure(string name, Exception exception)
    {
        FailedCount++;
        Console.Error.WriteLine($"FAIL: {name}");
        Console.Error.WriteLine(exception);
    }
}
