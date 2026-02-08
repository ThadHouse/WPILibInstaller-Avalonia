using System.Text.Json.Serialization;

namespace ToolsUpdater;

[JsonSourceGenerationOptions(WriteIndented = true, PropertyNameCaseInsensitive = true)]
[JsonSerializable(typeof(ToolConfig[]))]
internal sealed partial class ToolConfigContext : JsonSerializerContext
{
}

public record ToolConfig(string? Name, string? Version, ArtifactConfig Artifact, bool Cpp)
{
    public bool IsValid => !string.IsNullOrEmpty(Name) &&
                           !string.IsNullOrEmpty(Version);
}
