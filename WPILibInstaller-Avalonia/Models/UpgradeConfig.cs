#nullable disable


namespace WPILibInstaller.Models
{
    public class MavenConfig
    {
        public string Folder { get; set; }
        public string MetaDataFixerJar { get; set; }
    }

    public class ToolsConfig
    {
        public string Folder { get; set; }
        public string UpdaterExe { get; set; }
    }

    public class UpgradeConfig
    {
        public string FrcYear { get; set; }
        public string InstallerType { get; set; }

        public const string WindowsInstallerType = "Windows";
        public const string LinuxInstallerType = "Linux";
        public const string LinuxArm64InstallerType = "LinuxArm64";
        public const string MacInstallerType = "Mac";
        public const string MacArmInstallerType = "MacArm";

        public MavenConfig Maven { get; set; }
        public ToolsConfig Tools { get; set; }
        public string PathFolder { get; set; }
    }
}
