#nullable disable

using System.Text.Json.Serialization;

namespace WPILibInstaller.Models
{
    public class ArtifactConfig
    {
        [JsonPropertyName("classifier")]
        public string Classifier { get; set; }
        [JsonPropertyName("extension")]
        public string Extension { get; set; }
        [JsonPropertyName("groupId")]
        public string GroupId { get; set; }
        [JsonPropertyName("artifactId")]
        public string ArtifactId { get; set; }
        [JsonPropertyName("version")]
        public string Version { get; set; }
    }

    public class ToolConfig
    {
        [JsonPropertyName("name")]
        public string Name { get; set; }
        [JsonPropertyName("version")]
        public string Version { get; set; }
        [JsonPropertyName("artifact")]
        public ArtifactConfig Artifact { get; set; }
    }
}
