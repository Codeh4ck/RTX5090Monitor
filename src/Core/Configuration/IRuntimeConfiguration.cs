using Rtx5090Monitoring.Contracts.Configuration;

namespace Rtx5090Monitoring.Core.Configuration;

public interface IRuntimeConfiguration
{
    ConfigurationSnapshot Current { get; }
    event Action? Changed;
    Task<ConfigurationSnapshot> UpdateAsync(Func<ServiceConfiguration, ServiceConfiguration> update,
        CancellationToken cancellationToken, string? expectedVersion = null);
}
