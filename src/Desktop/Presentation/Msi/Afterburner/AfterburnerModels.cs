using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using RTXLens.Plugins.Msi.Contracts.Safeguard;

namespace RTXLens.Presentation.Msi.Afterburner;

public sealed record AfterburnerSettings(
    bool MonitoringEnabled,
    bool SafeguardEnabled,
    bool SoftwareAlarm,
    double MaxCurrent,
    double CurrentDifference,
    int Connector,
    bool UseLowPowerAction)
{
    public AfterburnerConnectorSettings[] ConnectorAlarms { get; init; } = [];

    public AfterburnerConnectorSettings[] GetConnectorAlarms() => ConnectorAlarms.Length > 0
        ? ConnectorAlarms : [new(Connector, SafeguardEnabled, UseLowPowerAction)];

    public void Validate()
    {
        if (Connector is not (1 or 2)
            || !double.IsFinite(MaxCurrent) || MaxCurrent is <= 0 or > 30
            || !double.IsFinite(CurrentDifference) || CurrentDifference is <= 0 or > 30)
        {
            throw new ArgumentException("Select connector 1 or 2 and enter current limits greater than 0 and at most 30 A.");
        }

        if (MaxCurrent != Math.Round(MaxCurrent, 1, MidpointRounding.AwayFromZero)
            || CurrentDifference != Math.Round(CurrentDifference, 1, MidpointRounding.AwayFromZero))
        {
            throw new ArgumentException("Afterburner current limits must have at most one decimal place.");
        }

        var alarms = GetConnectorAlarms();
        if (alarms.Length is < 1 or > 2 || alarms.Any(alarm => alarm.Connector is not (1 or 2))
            || alarms.Select(alarm => alarm.Connector).Distinct().Count() != alarms.Length)
        {
            throw new ArgumentException("Each connector must have one independent alarm configuration.");
        }

        if (alarms.Any(alarm => alarm.AlarmTimeoutMs is < 0))
        {
            throw new ArgumentException("Alarm timeout must be a non-negative whole number of milliseconds.");
        }

        if (alarms.Any(alarm => alarm.Enabled) && !MonitoringEnabled)
        {
            throw new ArgumentException("PSU monitoring must be enabled before enabling Safeguard+.");
        }
    }
}

public sealed record AfterburnerConnectorSettings(int Connector, bool Enabled, bool UseLowPowerAction)
{
    // Null preserves the native timeout, including an absent key using Afterburner's own default.
    public int? AlarmTimeoutMs { get; init; }
    public SafeguardBinding? Script { get; init; }
}

public sealed record AfterburnerConnectorInspection(int Connector, bool SourceEnabled, bool AlarmConfigured,
    bool LowPowerAction, string Minimum, string Maximum, string Flags, string Timeout, string Application, string Arguments, string Formula)
{
    public string? BuiltInScriptFileName { get; init; }
    public Guid? ScriptBindingId { get; init; }

    public int? AlarmTimeoutMs => Timeout == "Default" ? null
        : int.TryParse(Timeout, NumberStyles.None, CultureInfo.InvariantCulture, out var milliseconds) && milliseconds >= 0
            ? milliseconds : throw new InvalidDataException("Invalid alarm timeout for connector " + Connector + ".");

    public string Status => AlarmConfigured ? "Configured" : Flags is not ("0" or "3") ? "Needs review" : "Not configured";
}

public sealed record AfterburnerInstallation(string Directory, string Version, bool PluginPresent, bool ConfigPresent);

public sealed record AfterburnerFileChange(string RelativePath, byte[] Before, byte[] After)
{
    public bool BeforeExists { get; init; } = true;
    public bool Changed => !BeforeExists || !Before.AsSpan().SequenceEqual(After);
    public string BeforeHash => Convert.ToHexString(SHA256.HashData(Before));
    public string AfterHash => Convert.ToHexString(SHA256.HashData(After));
}

public sealed record AfterburnerChangeSet(string Installation, AfterburnerFileChange[] Files)
{
    public bool HasChanges => Files.Any(file => file.Changed);

    public string Id
    {
        get
        {
            var fingerprint = Installation + "\n" + string.Join("\n", Files.Select(file =>
                file.RelativePath + ":" + file.BeforeExists + ":" + file.BeforeHash + ":" + file.AfterHash));

            return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(fingerprint)));
        }
    }
}

public sealed record AfterburnerInspection(AfterburnerSettings Settings, bool Connector1Configured,
    bool Connector2Configured, string AlarmAction, string SoundAndTiming);

public sealed record AfterburnerBackup(string Id, DateTimeOffset CreatedAt, string Kind, string Installation,
    Dictionary<string, string> Hashes, bool Restorable = true);

public sealed record AfterburnerOperation(string Installation, string Action, AfterburnerSettings? Settings,
    string ExpectedPlanId, string? BackupId)
{
    public AfterburnerBackupDeletion[] BackupsToDelete { get; init; } = [];
}

public sealed record AfterburnerBackupDeletion(string Id, string ExpectedFingerprint);

public sealed record AfterburnerOperationResult(bool Success, string Message);