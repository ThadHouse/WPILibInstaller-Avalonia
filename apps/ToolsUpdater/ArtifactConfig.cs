using System.Text.Json.Serialization;

namespace ToolsUpdater;

[JsonSourceGenerationOptions(WriteIndented = true, PropertyNameCaseInsensitive = true)]
[JsonSerializable(typeof(ArtifactConfig))]
internal sealed partial class ArtifactConfigContext : JsonSerializerContext
{
}

public record ArtifactConfig(string Classifier, string Extension, string GroupId, string ArtifactId, string Version);
