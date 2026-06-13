#nullable disable


namespace WPILibInstaller.Models
{
    public class GradleConfig
    {
        public string Hash { get; set; }
        public string ZipName { get; set; }
        public string[] ExtractLocations { get; set; }
    }

    public class CppToolchainConfig
    {
        public string Version { get; set; }
        public string Directory { get; set; }
    }
    public class FullConfig
    {
        public GradleConfig Gradle { get; set; }
        public CppToolchainConfig CppToolchain { get; set; }
    }
}
