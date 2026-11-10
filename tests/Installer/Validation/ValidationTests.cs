using System;
using System.IO;
using RTXLens.Installer.Actions.Startup;
using RTXLens.Installer.Actions.Validation;

namespace RTXLens.Installer.Tests.Validation;

internal static class ValidationTests
{
    public static void Run()
    {
        const string root = @"C:\Program Files\RTXLens";
        Check.That(ServiceHealth.IsHealthy("{\"status\":\"running\",\"configurationRevision\":2,\"plugins\":[]}"), "Health verification accepts the actual service liveness contract without requiring hardware.");
        Check.That(!ServiceHealth.IsHealthy("{\"status\":\"running\"}"), "An uninitialized or unrelated HTTP response cannot pass the startup gate.");
        Check.That(!ServiceHealth.IsHealthy("{\"status\":\"stopped\",\"configurationRevision\":2}"), "A stopped service fails its health gate.");
        Check.That(StartupCleanup.MatchesInstalledApp('"' + root + "\\Desktop\\RTXLens.App.exe\" --startup", root), "Uninstall recognizes quoted installed startup entries with arguments.");
        Check.That(!StartupCleanup.MatchesInstalledApp("\"C:\\Portable\\RTXLens.App.exe\" --startup", root), "Uninstall preserves startup entries for portable or unrelated applications.");
        Check.That(!StartupCleanup.MatchesInstalledApp(null, root), "Missing startup entries need no cleanup.");
        Check.That(InstallationPreflight.IsInstalledApp(Path.Combine(root, "Desktop", "RTXLens.App.exe"), root), "Desktop process detection matches the exact installed executable.");
        Check.That(!InstallationPreflight.IsInstalledApp(Path.Combine(root + "-other", "Desktop", "RTXLens.App.exe"), root), "Similar directory prefixes do not identify another app as this installation.");
    }
}
