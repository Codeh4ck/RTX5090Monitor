using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Configuration;
using Rtx5090Monitoring.Contracts.Warnings;
using System.Text.Json.Serialization;
using Rtx5090Monitoring.Contracts.Configuration;

namespace Rtx5090Monitoring.Service.Configuration;

internal static class RuntimeJson
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public static WarningOptions ReadWarningOptions(IConfigurationSection section) =>
        SectionNode(section)?.Deserialize<WarningOptions>(Options) ?? new WarningOptions();

    private static JsonNode? SectionNode(IConfigurationSection section)
    {
        var children = section.GetChildren().ToArray();
        if (children.Length > 0)
        {
            return new JsonObject(children.Select(child => new KeyValuePair<string, JsonNode?>(child.Key, SectionNode(child))));
        }

        if (section.Value is not { } value)
        {
            return null;
        }

        if (bool.TryParse(value, out var boolean))
        {
            return JsonValue.Create(boolean);
        }

        try { return JsonNode.Parse(value); }
        catch (JsonException) { return JsonValue.Create(value); }
    }

    public static string Serialize(ServiceConfiguration configuration) => JsonSerializer.Serialize(configuration, Options);

    public static ServiceConfiguration Deserialize(string contents) =>
        JsonSerializer.Deserialize<ServiceConfiguration>(contents, Options) ?? throw new ArgumentException("Configuration cannot be null.");

    public static ServiceConfiguration Clone(ServiceConfiguration configuration) => Deserialize(Serialize(configuration));
}
