using System;
using System.IO;
using Microsoft.Win32;
using RTXLens.Installer.Actions.Configuration;
using RTXLens.Installer.Actions.Startup;
using RTXLens.Installer.Actions.Services;
using RTXLens.Installer.Actions.Validation;
using WixToolset.Dtf.WindowsInstaller;

namespace RTXLens.Installer.Actions;

public static class CustomActions
{
    [CustomAction]
    public static ActionResult SelectLegacyInstallFolder(Session session) => Run(session, () =>
    {
        var legacy = session["LEGACYINSTALLFOLDER"];
        var defaultPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Rtx5090Monitoring");
        if (!LegacyInstallationMigration.SamePath(legacy, defaultPath))
        {
            session["INSTALLFOLDER"] = legacy;
        }
    });

    [CustomAction]
    public static ActionResult PrepareLegacyMigration(Session session) => Run(session, () =>
    {
        var data = new CustomActionData
        {
            { "Previous", session["LEGACYINSTALLFOLDER"] },
            { "Destination", session["INSTALLFOLDER"] },
            { "Id", Guid.NewGuid().ToString("N") }
        };
        foreach (var action in new[] { "CaptureLegacyMigration", "ApplyLegacyMigration", "RestoreLegacyMigration", "CleanLegacyMigration" })
        {
            session[action] = data.ToString();
        }
    });

    [CustomAction]
    public static ActionResult CaptureLegacyMigration(Session session) => Run(session, () => LegacyMigration(session).Capture());

    [CustomAction]
    public static ActionResult ApplyLegacyMigration(Session session) => Run(session, () => LegacyMigration(session).Apply());

    [CustomAction]
    public static ActionResult RestoreLegacyMigration(Session session) => Run(session, () => LegacyMigration(session).Restore());

    [CustomAction]
    public static ActionResult CleanLegacyMigration(Session session) => Run(session, () => LegacyMigration(session).Clean());

    private static LegacyInstallationMigration LegacyMigration(Session session)
    {
        var data = session.CustomActionData;
        return new LegacyInstallationMigration(data["Previous"], data["Destination"], data["Id"]);
    }

    [CustomAction]
    public static ActionResult Preflight(Session session) => Run(session, () =>
    {
        InstallationPreflight.CheckDesktopClosed(session["INSTALLFOLDER"]);
        var previous = session["LEGACYINSTALLFOLDER"];
        if (!string.IsNullOrEmpty(previous) && !string.IsNullOrEmpty(session["WIX_UPGRADE_DETECTED"]))
        {
            using var hive = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64);
            using var registration = hive.OpenSubKey(@"SOFTWARE\Rtx5090Monitoring");
            if (registration?.GetValue("InstallDirectory") is not string registered || !LegacyInstallationMigration.SamePath(previous, registered))
            {
                throw new IOException("Setup could not validate the registered RTX Monitor installation location.");
            }

            InstallationPreflight.CheckDesktopClosed(previous, legacy: true);
            InstallationPreflight.CheckLegacyServiceOwnership(previous);
        }
        InstallationPreflight.CheckServiceOwnership(session["INSTALLFOLDER"], !string.IsNullOrEmpty(session["Installed"]), string.IsNullOrEmpty(session["WIX_UPGRADE_DETECTED"]) ? null : previous);
    });

    [CustomAction]
    public static ActionResult PortAvailable(Session session) => Run(session, InstallationPreflight.CheckPortAvailable);

    [CustomAction]
    public static ActionResult PrepareConfigurationBackup(Session session) => Run(session, () =>
    {
        var data = new CustomActionData { { "BackupId", Guid.NewGuid().ToString("N") } };
        foreach (var action in new[] { "BackupConfiguration", "RestoreConfiguration", "CleanConfigurationBackup" })
        {
            session[action] = data.ToString();
        }
    });

    [CustomAction]
    public static ActionResult BackupConfiguration(Session session) => Run(session, () =>
        new ConfigurationBackup(session.CustomActionData["BackupId"]).Capture());

    [CustomAction]
    public static ActionResult RestoreConfiguration(Session session) => Run(session, () =>
        new ConfigurationBackup(session.CustomActionData["BackupId"]).Restore());

    [CustomAction]
    public static ActionResult CleanConfigurationBackup(Session session) => Run(session, () =>
        new ConfigurationBackup(session.CustomActionData["BackupId"]).Clean());

    [CustomAction]
    public static ActionResult PrepareServiceHealth(Session session) => Run(session, () =>
    {
        session["VerifyServiceHealth"] = new CustomActionData { { "Version", session["ProductVersion"] } }.ToString();
    });

    [CustomAction]
    public static ActionResult VerifyServiceHealth(Session session) =>
        Run(session, () => ServiceHealth.Wait(session.CustomActionData["Version"]));

    [CustomAction]
    public static ActionResult ConfigureServiceStartup(Session session) => Run(session, ServiceStartupConfiguration.Apply);

    [CustomAction]
    public static ActionResult PrepareStartupCleanup(Session session) => Run(session, () =>
    {
        session["RemoveStartupRegistration"] = new CustomActionData { { "InstallDirectory", session["INSTALLFOLDER"] } }.ToString();
    });

    [CustomAction]
    public static ActionResult RemoveStartupRegistration(Session session) => Run(session, () =>
        StartupCleanup.Remove(session.CustomActionData["InstallDirectory"], message => session.Log(message)));

    [CustomAction]
    public static ActionResult PrepareStartupMigration(Session session) => Run(session, () =>
    {
        session["MigrateStartupRegistration"] = new CustomActionData
        {
            { "InstallDirectory", session["INSTALLFOLDER"] },
            { "PreviousDirectory", session["LEGACYINSTALLFOLDER"] }
        }.ToString();
    });

    [CustomAction]
    public static ActionResult MigrateStartupRegistration(Session session) => Run(session, () =>
        StartupCleanup.Migrate(session.CustomActionData["InstallDirectory"], message => session.Log(message),
            string.IsNullOrEmpty(session.CustomActionData["PreviousDirectory"]) ? null : session.CustomActionData["PreviousDirectory"]));

    [CustomAction]
    public static ActionResult PrepareAfterburnerBackupCleanup(Session session) => Run(session, () =>
    {
        using var hive = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64);
        using var registration = hive.OpenSubKey(@"SOFTWARE\RTXLens");
        var installation = registration?.GetValue("InstallDirectory") as string;
        session["RemoveAfterburnerBackups"] = new CustomActionData { { "InstallDirectory", installation ?? "" } }.ToString();
    });

    [CustomAction]
    public static ActionResult RemoveAfterburnerBackups(Session session) => Run(session, () =>
        AfterburnerBackupCleanup.Remove(session.CustomActionData["InstallDirectory"],
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), message => session.Log(message)));

    private static ActionResult Run(Session session, Action action)
    {
        try
        {
            action();
            return ActionResult.Success;
        }
        catch (Exception error)
        {
            session.Log(error.ToString());
            using var record = new Record(1);
            record[0] = "RTX Lens setup: [1]";
            record[1] = error.Message;
            session.Message(InstallMessage.Error, record);
            return ActionResult.Failure;
        }
    }
}
