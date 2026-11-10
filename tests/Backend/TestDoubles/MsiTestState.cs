using RTXLens.Plugins.Msi.Contracts.Configuration;

namespace RTXLens.Tests.TestDoubles;

internal sealed class MsiTestState
{
    public bool Available { get; set; } = true;
    public MsiOptions? LastOptions { get; set; }
    public int ReleaseCount { get; set; }
}
