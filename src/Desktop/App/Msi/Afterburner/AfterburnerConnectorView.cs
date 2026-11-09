using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.Globalization.NumberFormatting;
using Rtx5090Monitoring.Presentation.Msi.Afterburner;

namespace Rtx5090Monitoring.App.Msi.Afterburner;

internal sealed class AfterburnerConnectorView : UserControl
{
    private readonly CheckBox _enabled;
    private readonly RadioButton _lowPower;
    private readonly NumberBox _timeout;
    private readonly RadioButton _script;
    private readonly ComboBox _scripts;
    private bool _invalidNumericInput;

    internal int Connector { get; }
    internal bool HasPendingChanges => Saved is { } saved
        && (_invalidNumericInput || (_enabled.IsChecked == true) != saved.AlarmConfigured || (_lowPower.IsChecked == true) != saved.LowPowerAction
            || (double.IsFinite(_timeout.Value) ? (int?)checked((int)_timeout.Value) : null) != saved.AlarmTimeoutMs);
    internal AfterburnerConnectorSettings Draft
    {
        get
        {
            if (!double.IsFinite(_timeout.Value) && Saved?.AlarmTimeoutMs is not null)
            {
                throw new ArgumentException("Enter an alarm timeout for connector " + Connector + ".");
            }

            return new(Connector, _enabled.IsChecked == true, _lowPower.IsChecked == true)
            {
                AlarmTimeoutMs = double.IsFinite(_timeout.Value) ? checked((int)_timeout.Value) : null
            };
        }
    }

    internal AfterburnerConnectorInspection? Saved { get; private set; }

    internal AfterburnerConnectorView(int connector)
    {
        Connector = connector;
        _enabled = new CheckBox { Name = "Connector" + connector + "AlarmEnabled", Content = "Enable alarm for connector " + connector };
        _timeout = new NumberBox
        {
            Name = "Connector" + connector + "AlarmTimeout",
            Header = "Alarm timeout (ms)",
            PlaceholderText = "Afterburner default",
            Minimum = 0,
            Maximum = int.MaxValue,
            SmallChange = 100,
            LargeChange = 1000,
            SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Inline,
            NumberFormatter = new DecimalFormatter
            {
                IntegerDigits = 1,
                FractionDigits = 0,
                IsGrouped = false,
                NumberRounder = new IncrementNumberRounder
                {
                    Increment = 1,
                    RoundingAlgorithm = RoundingAlgorithm.RoundHalfAwayFromZero
                }
            }
        };
        _lowPower = new RadioButton
        {
            Name = "Connector" + connector + "LowPower",
            GroupName = "Connector" + connector + "Action",
            Content = "Run emergency low-power profile on alarm"
        };
        _script = new RadioButton
        {
            GroupName = _lowPower.GroupName,
            Content = "Run script",
            IsEnabled = false
        };
        ToolTipService.SetToolTip(_script, "Script actions will be available in a later update. Existing custom actions are preserved.");
        _scripts = new ComboBox
        {
            PlaceholderText = "Scripts coming soon",
            IsEnabled = false,
            HorizontalAlignment = HorizontalAlignment.Stretch
        };

        var body = new StackPanel { Spacing = 10 };
        body.Children.Add(_enabled);
        body.Children.Add(_timeout);
        body.Children.Add(_lowPower);
        body.Children.Add(_script);
        body.Children.Add(_scripts);
        Content = body;

        _timeout.ValueChanged += OnTimeoutChanged;
    }

    internal void SetInspection(AfterburnerConnectorInspection saved)
    {
        Saved = saved;
        _invalidNumericInput = false;
        _enabled.IsChecked = saved.AlarmConfigured;
        _timeout.Value = saved.AlarmTimeoutMs is { } timeout ? timeout : double.NaN;
        _lowPower.IsChecked = saved.LowPowerAction;
        _script.IsChecked = !saved.LowPowerAction;
        _scripts.PlaceholderText = saved.Application.Length > 0 && !saved.LowPowerAction
            ? "Existing custom action (preserved)" : "Scripts coming soon";
    }

    internal void CommitPendingEdits() => _invalidNumericInput = !AfterburnerNumericInput.Commit(_timeout);

    private void OnTimeoutChanged(NumberBox sender, NumberBoxValueChangedEventArgs args)
    {
        if (double.IsFinite(args.NewValue))
        {
            var rounded = Math.Round(args.NewValue, MidpointRounding.AwayFromZero);
            if (rounded != args.NewValue)
            {
                sender.Value = rounded;
            }
        }
    }
}