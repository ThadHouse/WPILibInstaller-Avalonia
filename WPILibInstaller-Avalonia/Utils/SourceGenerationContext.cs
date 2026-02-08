using System.Text.Json.Serialization;
using WPILibInstaller.Models;

namespace WPILibInstaller.Utils;

[JsonSourceGenerationOptions(
    WriteIndented = false,
    DefaultIgnoreCondition = JsonIgnoreCondition.Never,
    UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow)]
[JsonSerializable(typeof(VsCodeConfig))]
[JsonSerializable(typeof(JdkConfig))]
[JsonSerializable(typeof(AdvantageScopeConfig))]
[JsonSerializable(typeof(ElasticConfig))]
[JsonSerializable(typeof(FullConfig))]
[JsonSerializable(typeof(UpgradeConfig))]
[JsonSerializable(typeof(ShortcutData))]
[JsonSerializable(typeof(Extension))]
[JsonSerializable(typeof(ArtifactConfig))]
[JsonSerializable(typeof(ToolConfig))]
[JsonSerializable(typeof(GradleConfig))]
[JsonSerializable(typeof(CppToolchainConfig))]
[JsonSerializable(typeof(MavenConfig))]
[JsonSerializable(typeof(ToolsConfig))]
[JsonSerializable(typeof(ShortcutInfo))]
[JsonSerializable(typeof(NewEnvVariable))]
[JsonSerializable(typeof(AddedPathVariable))]
internal sealed partial class SourceGenerationContext : JsonSerializerContext
{
}
