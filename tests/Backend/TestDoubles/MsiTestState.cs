using Rtx5090Monitoring.Plugins.Msi.Contracts.Configuration;

namespace Rtx5090Monitoring.Tests.TestDoubles;

internal sealed class MsiTestState
{
    public bool Available { get; set; } = true;
    public MsiOptions? LastOptions { get; set; }
    public int ReleaseCount { get; set; }
}
