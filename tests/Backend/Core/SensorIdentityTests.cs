using Rtx5090Monitoring.Tests.Fixtures;
using Rtx5090Monitoring.Tests.Infrastructure;

namespace Rtx5090Monitoring.Tests.Core;

internal static class SensorIdentityTests
{
    public static void Run(TestRunner runner)
    {
        runner.Run(DistinguishesDifferentConnectors);
        runner.Run(EscapesReservedCharactersInDeviceIds);
        runner.Run(RequiresExplicitPermissionToMixConnectorScopes);
    }

    private static void DistinguishesDifferentConnectors()
    {
        var firstConnector = SensorTestData.WireViewPower with { ConnectorId = "a" };
        var secondConnector = SensorTestData.WireViewPower with { ConnectorId = "b" };

        TestAssert.False(firstConnector.Key == secondConnector.Key, "Different connectors must retain different sensor keys.");
    }

    private static void RequiresExplicitPermissionToMixConnectorScopes()
    {
        var first = SensorTestData.WireViewPower with { ConnectorId = "1" };
        var second = first with { ConnectorId = "2" };
        var selection = new Contracts.Monitoring.SensorSelection { Name = "current", PreferredSources = [first, second] };
        TestAssert.False(selection.IsValid(), "Different connectors must not silently become interchangeable sources.");
        selection.AllowDifferentScope = true;
        TestAssert.True(selection.IsValid(), "Cross-connector fallback requires explicit scope permission.");
    }

    private static void EscapesReservedCharactersInDeviceIds()
    {
        var identity = SensorTestData.WireViewPower with { DeviceId = "a/b" };

        TestAssert.True(identity.Key.Contains("a%2Fb", StringComparison.Ordinal), "A slash in a device ID must not become a key separator.");
    }
}
