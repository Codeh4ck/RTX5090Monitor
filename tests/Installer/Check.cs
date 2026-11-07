using System;

namespace Rtx5090Monitoring.Installer.Tests;

internal static class Check
{
    public static void That(bool condition, string message, bool report = true)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }

        if (report)
        {
            Console.WriteLine("PASS: " + message);
        }
    }
}
