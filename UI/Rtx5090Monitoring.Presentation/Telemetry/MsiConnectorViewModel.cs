using System.Collections.ObjectModel;
using System.Globalization;
using Rtx5090Monitoring.Contracts.Enums;
using Rtx5090Monitoring.Contracts.Sensors;
using Rtx5090Monitoring.Presentation.Infrastructure;
using Rtx5090Monitoring.Presentation.Settings;

namespace Rtx5090Monitoring.Presentation.Telemetry;

public sealed class MsiConnectorViewModel(int connector) : ObservableObject
{
    public string Id { get; } = connector.ToString(CultureInfo.InvariantCulture);
    public string Name => "12V-2x6 · Connector " + Id;
    public string Total
    {
        get;
        private set => Set(ref field, value);
    } = "—";
    public string Spread
    {
        get;
        private set => Set(ref field, value);
    } = "—";
    public ObservableCollection<PinViewModel> Pins { get; } = [.. Enumerable.Range(1, 6).Select(pin => new PinViewModel(pin))];

    public void Update(SensorReading[] readings, AppPreferences preferences)
    {
        foreach (var pin in Pins)
        {
            var reading = readings.SingleOrDefault(item => item.Identity.Component == SensorComponent.PsuPin
                && item.Identity.ConnectorId == Id && item.Identity.Channel == pin.Channel && item.Identity.Metric == SensorMetric.Current);
            pin.Update(reading?.Value, null, null, PinMetric.Current, null, preferences.ShowPeakHold, false);
        }
        var currents = Pins.Select(pin => pin.Current).ToArray();
        var complete = currents.All(value => value.HasValue);
        var total = readings.FirstOrDefault(reading => reading.Identity is { Component: SensorComponent.PsuConnector, Metric: SensorMetric.Current } && reading.Identity.ConnectorId == Id)?.Value;
        total ??= complete ? currents.Sum(value => value!.Value) : null;
        Total = total is { } value ? value.ToString("0.##", CultureInfo.CurrentCulture) + " A" : "—";
        Spread = complete ? (currents.Max() - currents.Min())!.Value.ToString("0.##", CultureInfo.CurrentCulture) + " A" : "—";
    }
}
