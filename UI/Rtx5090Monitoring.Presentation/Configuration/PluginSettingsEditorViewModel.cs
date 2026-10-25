using System.Text.Json;
using Rtx5090Monitoring.Client.Serialization;
using Rtx5090Monitoring.Plugins.Asus.Contracts.Configuration;
using Rtx5090Monitoring.Plugins.Msi.Contracts.Configuration;
using Rtx5090Monitoring.Contracts.Configuration;
using Rtx5090Monitoring.Presentation.Infrastructure;

namespace Rtx5090Monitoring.Presentation.Configuration;

public sealed class PluginSettingsEditorViewModel : EditableObject
{
    private readonly PluginSettings _original;
    private bool _enabled;
    private double _poll;
    private double _stale;

    public string Id { get; }
    public string Name { get; }
    public bool IsAsus => Id == "asus-astral";
    public bool IsWireView => Id == "wireview-pro2";
    public bool IsMsi => Id == "msi-psu";
    public int MinimumPollMs => IsMsi ? 1000 : 100;
    public MsiSettingsEditorViewModel Msi { get; }
    public AsusSettingsEditorViewModel Asus { get; }
    public bool Enabled { get => _enabled; set => Edit(ref _enabled, value); }
    public double PollIntervalMs { get => _poll; set => Edit(ref _poll, value); }
    public double StaleAfterMs { get => _stale; set => Edit(ref _stale, value); }

    public PluginSettingsEditorViewModel(string id, string name, PluginSettings settings)
    {
        Id = id;
        Name = name;
        _original = settings;
        _enabled = settings.Enabled;
        _poll = settings.PollIntervalMs;
        _stale = settings.StaleAfterMs;
        var options = IsAsus && settings.Options is { } json ? json.Deserialize(ClientJsonContext.Default.AsusOptions) : null;
        Asus = new AsusSettingsEditorViewModel(options ?? new AsusOptions());
        Asus.Edited += NotifyEdited;
        var msiOptions = IsMsi && settings.Options is { } msiJson ? msiJson.Deserialize(ClientJsonContext.Default.MsiOptions) : null;
        Msi = new MsiSettingsEditorViewModel(msiOptions ?? new MsiOptions());
        Msi.Edited += NotifyEdited;
    }

    public PluginSettings Build()
    {
        var result = _original with
        {
            Enabled = Enabled,
            PollIntervalMs = NumericInput.Integer(PollIntervalMs, Name + " polling interval"),
            StaleAfterMs = NumericInput.Integer(StaleAfterMs, Name + " freshness timeout")
        };
        if (!result.IsValid())
        {
            throw new ArgumentException($"{Name}: polling must be 100–30000 ms; freshness must be at least polling and at most 300000 ms.");
        }

        if (IsAsus)
        {
            result = result with { Options = JsonSerializer.SerializeToElement(Asus.Build(result.PollIntervalMs), ClientJsonContext.Default.AsusOptions) };
        }

        if (IsMsi)
        {
            if (result.PollIntervalMs < 1000)
            {
                throw new ArgumentException("MSI polling must be at least 1000 ms.");
            }

            result = result with { Options = JsonSerializer.SerializeToElement(Msi.Build(), ClientJsonContext.Default.MsiOptions) };
        }

        return result;
    }

    public bool Matches(PluginSettings settings)
    {
        if (IsMsi && !Msi.Matches(settings.Options is { } msiJson ? msiJson.Deserialize(ClientJsonContext.Default.MsiOptions) ?? new MsiOptions() : new MsiOptions()))
        {
            return false;
        }

        if (!double.IsFinite(PollIntervalMs) || !double.IsFinite(StaleAfterMs) || Enabled != settings.Enabled
            || Math.Abs(PollIntervalMs - settings.PollIntervalMs) > 0.0001 || Math.Abs(StaleAfterMs - settings.StaleAfterMs) > 0.0001)
        {
            return false;
        }

        var options = IsAsus && settings.Options is { } json ? json.Deserialize(ClientJsonContext.Default.AsusOptions) : null;
        return !IsAsus || Asus.Matches(options ?? new AsusOptions());
    }
}
