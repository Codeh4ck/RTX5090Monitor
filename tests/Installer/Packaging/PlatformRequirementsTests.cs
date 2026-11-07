using System;
using System.Globalization;
using System.IO;
using Microsoft.Win32;
using WixToolset.Dtf.WindowsInstaller;
using MsiInstaller = WixToolset.Dtf.WindowsInstaller.Installer;

namespace Rtx5090Monitoring.Installer.Tests.Packaging;

internal static class PlatformRequirementsTests
{
    public static void Run(string path, Database database)
    {
        var condition = database.ExecuteStringQuery("SELECT `Condition` FROM `LaunchCondition` WHERE `Description`='RTX Monitor requires 64-bit Windows 10 (build 17763+) or Windows 11.'")[0];
        var previousUi = MsiInstaller.SetInternalUI(InstallUIOptions.Silent);
        try
        {
            using var session = MsiInstaller.OpenPackage(Path.GetFullPath(path), true);
            session["Installed"] = string.Empty;
            // AppSearch only reads detection data. Never run installation sequences in package tests.
            session.DoAction("AppSearch");
            using var machine = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64);
            using var windows = machine.OpenSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion");
            var build = Convert.ToInt32(windows!.GetValue("CurrentBuildNumber"), CultureInfo.InvariantCulture);
            Console.WriteLine($"OS detection: MSI WindowsBuild={session["WindowsBuild"]}; actual build={build}; detected build={session["WINDOWSBUILDNUMBER"]}.");
            Check.That(session.EvaluateCondition(condition) == (Environment.Is64BitOperatingSystem && build >= 17763), "The packaged OS condition correctly evaluates the current machine without installing anything.");
            Check.That(session["WINDOWSBUILDNUMBER"] == build.ToString(CultureInfo.InvariantCulture), "AppSearch reads the actual build from the 64-bit Windows registry.");

            session["WindowsBuild"] = "9600";
            AssertCondition(session, condition, "17763", true, false, true, "Windows 10 1809 meets the minimum build despite MSI's compatibility value.");
            AssertCondition(session, condition, "19045", true, false, true, "Windows 10 22H2 is accepted.");
            AssertCondition(session, condition, "22000", true, false, true, "Windows 11 is accepted.");
            AssertCondition(session, condition, "26300", true, false, true, "Newer Windows 11 builds are accepted.");
            AssertCondition(session, condition, "17762", true, false, false, "Builds below Windows 10 1809 are rejected.");
            AssertCondition(session, condition, "9600", true, false, false, "Windows 8.1 is rejected.");
            AssertCondition(session, condition, "26300", false, false, false, "32-bit Windows is rejected.");
            AssertCondition(session, condition, string.Empty, true, false, false, "Missing build detection cannot pass a fresh installation gate.");
            AssertCondition(session, condition, string.Empty, false, true, true, "Maintenance and uninstall remain available for an installed product.");
        }
        finally
        {
            MsiInstaller.SetInternalUI(previousUi);
        }

        var secureProperties = database.ExecuteStringQuery("SELECT `Value` FROM `Property` WHERE `Property`='SecureCustomProperties'")[0];
        Check.That(Array.IndexOf(secureProperties.Split(';'), "WINDOWSBUILDNUMBER") >= 0, "The detected Windows build is preserved across elevation.");
        foreach (var table in new[] { "InstallUISequence", "InstallExecuteSequence" })
        {
            var search = database.ExecuteIntegerQuery($"SELECT `Sequence` FROM `{table}` WHERE `Action`='AppSearch'")[0];
            var launch = database.ExecuteIntegerQuery($"SELECT `Sequence` FROM `{table}` WHERE `Action`='LaunchConditions'")[0];
            Check.That(search < launch, "OS detection precedes launch conditions in " + table + ".");
        }
    }

    private static void AssertCondition(Session session, string condition, string build, bool is64Bit, bool installed, bool expected, string message)
    {
        session["WINDOWSBUILDNUMBER"] = build;
        session["VersionNT64"] = is64Bit ? "603" : string.Empty;
        session["Installed"] = installed ? "1" : string.Empty;
        Check.That(session.EvaluateCondition(condition) == expected, message);
    }
}
