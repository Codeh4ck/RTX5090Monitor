using Microsoft.Extensions.Configuration;
using Rtx5090Monitoring.Contracts.Enums;
using Rtx5090Monitoring.Contracts.Monitoring;
using Rtx5090Monitoring.Tests.Fixtures;
using Rtx5090Monitoring.Tests.Infrastructure;

namespace Rtx5090Monitoring.Tests.Core;

internal static class MonitoringOptionsTests
{
    public static void Run(TestRunner runner)
    {
        runner.Run(RejectsDifferentMeasurementScopesByDefault);
        runner.Run(AllowsExplicitSelectionAcrossMeasurementScopes);
        runner.Run(RejectsInvalidStreamIntervals);
        runner.Run(RejectsDuplicateSelectionNames);
        runner.Run(BindsTheDocumentedSelectionConfiguration);
    }

    private static void RejectsDifferentMeasurementScopesByDefault()
    {
        var selection = CreateSelectionWithDifferentScopes();

        TestAssert.False(selection.IsValid(), "Different scopes require explicit opt-in.");
    }

    private static void AllowsExplicitSelectionAcrossMeasurementScopes()
    {
        var selection = CreateSelectionWithDifferentScopes();
        selection.AllowDifferentScope = true;

        TestAssert.True(selection.IsValid(), "The user must be able to explicitly choose another scope.");
    }

    private static void RejectsInvalidStreamIntervals()
    {
        var options = new MonitoringOptions
        {
            StreamIntervalMs = 0
        };

        TestAssert.False(options.IsValid(), "A zero stream interval must be rejected.");
    }

    private static void RejectsDuplicateSelectionNames()
    {
        var firstSelection = SensorTestData.CreatePowerSelection();
        var duplicateSelection = SensorTestData.CreatePowerSelection();
        var options = new MonitoringOptions
        {
            Selections = [firstSelection, duplicateSelection]
        };

        TestAssert.False(options.IsValid(), "Selection names must be unique.");
    }

    private static void BindsTheDocumentedSelectionConfiguration()
    {
        var examplePath = Path.Combine(AppContext.BaseDirectory, "source-selection.example.json");
        var configuration = new ConfigurationBuilder()
            .AddJsonFile(examplePath)
            .Build();

        var options = configuration.GetSection("Monitoring").Get<MonitoringOptions>();

        TestAssert.True(options is not null, "The documented options must bind successfully.");
        TestAssert.Equal(2, options!.Selections.Count);
        TestAssert.True(options.IsValid(), "The documented configuration must pass validation.");
    }

    private static SensorSelection CreateSelectionWithDifferentScopes()
    {
        var asusBoardPower = SensorTestData.AsusPower with
        {
            Component = SensorComponent.GpuBoard
        };

        return new SensorSelection
        {
            Name = "power",
            PreferredSources = [SensorTestData.WireViewPower, asusBoardPower]
        };
    }
}
