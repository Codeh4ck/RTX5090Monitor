using System;
using Rtx5090Monitoring.Installer.Tests.Configuration;
using Rtx5090Monitoring.Installer.Tests.Packaging;
using Rtx5090Monitoring.Installer.Tests.Validation;

namespace Rtx5090Monitoring.Installer.Tests;

internal static class Program
{
    private static int Main(string[] args)
    {
        try
        {
            if (args.Length == 3 && args[0] == "--msi")
            {
                PackageTests.Run(args[1], args[2]);
            }
            else
            {
                ValidationTests.Run();
                TcpListenerTests.Run();
                StartupCleanupTests.Run();
                ConfigurationBackupTests.Run();
            }

            Console.WriteLine("PASS: Installer checks completed without changing services, startup registry or installed applications.");
            return 0;
        }
        catch (Exception error)
        {
            Console.Error.WriteLine(error);
            return 1;
        }
    }
}
