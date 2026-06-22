#nullable disable

using System.Text.Json.Serialization;

namespace WPILibInstaller.Models
{
    public class AdvantageScopeConfig
    {
        [JsonPropertyName("zipFile")]
        public string ZipFile { get; set; }
        [JsonPropertyName("folder")]
        public string Folder { get; set; }
    }
}
