using RTXLens.Contracts.Configuration;
using RTXLens.Core.Plugins;
using RTXLens.Warnings;

namespace RTXLens.Tests.TestDoubles;

internal sealed class WarningOnlyFactory : ISensorPluginFactory
{
    public string Id => "third-party";
    public string Name => "Third-party test plugin";
    public IWarningSource WarningSource { get; } = new CustomWarningSource();
    public int Created { get; private set; }
    public ISensorPlugin Create(PluginSettings settings)
    {
        Created++;
        throw new InvalidOperationException("Hardware must not open for catalogue discovery.");
    }
}
