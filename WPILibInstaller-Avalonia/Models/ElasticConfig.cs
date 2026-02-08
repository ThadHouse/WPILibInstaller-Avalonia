#nullable disable

using System.Text.Json.Serialization;

namespace WPILibInstaller.Models
{
    public class ElasticConfig
    {
        [JsonPropertyName("zipFile")]
        public string ZipFile { get; set; }
        [JsonPropertyName("folder")]
        public string Folder { get; set; }
    }
}
