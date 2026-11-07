using System;
using Rtx5090Monitoring.Installer.Actions.Configuration;
using Rtx5090Monitoring.Installer.Actions.Startup;
using Rtx5090Monitoring.Installer.Actions.Services;
using Rtx5090Monitoring.Installer.Actions.Validation;
using WixToolset.Dtf.WindowsInstaller;

namespace Rtx5090Monitoring.Installer.Actions;

public static class CustomActions
{
    [CustomAction]
    public static ActionResult Preflight(Session session) => Run(session, () =>
    {
        InstallationPreflight.CheckDesktopClosed(session["INSTALLFOLDER"]);
        InstallationPreflight.CheckServiceOwnership(session["INSTALLFOLDER"], !string.IsNullOrEmpty(session["Installed"]));
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
    public static ActionResult VerifyServiceHealth(Session session) => Run(session, ServiceHealth.Wait);

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
            record[0] = "RTX Monitor setup: [1]";
            record[1] = error.Message;
            session.Message(InstallMessage.Error, record);
            return ActionResult.Failure;
        }
    }
}
