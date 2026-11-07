using System.Runtime.Serialization;

namespace Rtx5090Monitoring.Installer.Actions.Validation;

[DataContract]
internal sealed class HealthResponse
{
    [DataMember(Name = "status")]
    public string? Status { get; set; }

    [DataMember(Name = "configurationRevision")]
    public long ConfigurationRevision { get; set; }
}
