#nullable disable

using System.Text.Json.Serialization;

namespace WPILibInstaller.Models
{
    public class JdkConfig
    {
        [JsonPropertyName("tarFile")]
        public string TarFile { get; set; }
        [JsonPropertyName("folder")]
        public string Folder { get; set; }
    }
}
