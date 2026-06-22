using System.IO.Compression;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using WPILibInstaller.Models;

namespace WPILibInstaller.Utils;

public sealed class InstallerResourceFiles
{
    public InstallerResourceFiles(string? resourcesFile, string? artifactsFile)
    {
        ResourcesFile = resourcesFile;
        ArtifactsFile = artifactsFile;
    }

    public string? ResourcesFile { get; }

    public string? ArtifactsFile { get; }
}

public sealed class InstallerConfiguration
{
    public InstallerConfiguration(
        VsCodeConfig vsCodeConfig,
        JdkConfig jdkConfig,
        AdvantageScopeConfig advantageScopeConfig,
        ElasticConfig elasticConfig,
        FullConfig fullConfig,
        UpgradeConfig upgradeConfig)
    {
        VsCodeConfig = vsCodeConfig;
        JdkConfig = jdkConfig;
        AdvantageScopeConfig = advantageScopeConfig;
        ElasticConfig = elasticConfig;
        FullConfig = fullConfig;
        UpgradeConfig = upgradeConfig;
    }

    public VsCodeConfig VsCodeConfig { get; }

    public JdkConfig JdkConfig { get; }

    public AdvantageScopeConfig AdvantageScopeConfig { get; }

    public ElasticConfig ElasticConfig { get; }

    public FullConfig FullConfig { get; }

    public UpgradeConfig UpgradeConfig { get; }
}

public static class InstallerResources
{
    public const string VersionFileName = "WPILibInstallerVersion.txt";
    public const string ChecksumFileName = "checksum.txt";
    public const string DefaultVersion = "0.0.0";
    public const string MacInstallerVolumePath = "/Volumes/WPILibInstaller";

    public static string ReadInstallerVersion(string baseDirectory)
    {
        try
        {
            return File.ReadAllText(Path.Join(baseDirectory, VersionFileName)).Trim();
        }
        catch
        {
            return DefaultVersion;
        }
    }

    public static string ResourcesFileName(string version)
    {
        return $"{version}-resources.zip";
    }

    public static string ArtifactsFileName(string version)
    {
        return $"{version}-artifacts.{ArtifactsFileExtension()}";
    }

    public static string ArtifactsFileExtension()
    {
        return RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "zip" : "tar.gz";
    }

    public static InstallerResourceFiles FindInstallerFiles(string version, string baseDirectory)
    {
        string? resourcesFile = null;
        string? artifactsFile = null;

        var expectedResourcesFileName = ResourcesFileName(version);
        var expectedArtifactsFileName = ArtifactsFileName(version);

        foreach (var directory in EnumerateBundledSearchDirectories(baseDirectory))
        {
            FindInstallerFilesInDirectory(
                directory,
                expectedResourcesFileName,
                expectedArtifactsFileName,
                ref resourcesFile,
                ref artifactsFile);

            if (resourcesFile != null && artifactsFile != null)
            {
                break;
            }
        }

        return new InstallerResourceFiles(resourcesFile, artifactsFile);
    }

    public static IEnumerable<string> EnumerateBundledSearchDirectories(string baseDirectory)
    {
        yield return baseDirectory;

        if (!RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            yield break;
        }

        if (Directory.Exists(MacInstallerVolumePath))
        {
            yield return Path.GetFullPath(MacInstallerVolumePath);
        }

        // Look beside the .app by backing out of the macOS package.
        yield return Path.GetFullPath(Path.Join(baseDirectory, "..", "..", ".."));
    }

    public static async Task<InstallerConfiguration> LoadConfigurationAsync(string resourcesZipPath)
    {
        using var resourcesStream = File.OpenRead(resourcesZipPath);
        using var zipFile = new ZipArchive(resourcesStream, ZipArchiveMode.Read);

        return new InstallerConfiguration(
            await LoadJsonFromZipAsync(zipFile, "vscodeConfig.json", SourceGenerationContext.Default.VsCodeConfig),
            await LoadJsonFromZipAsync(zipFile, "jdkConfig.json", SourceGenerationContext.Default.JdkConfig),
            await LoadJsonFromZipAsync(zipFile, "advantageScopeConfig.json", SourceGenerationContext.Default.AdvantageScopeConfig),
            await LoadJsonFromZipAsync(zipFile, "elasticConfig.json", SourceGenerationContext.Default.ElasticConfig),
            await LoadJsonFromZipAsync(zipFile, "fullConfig.json", SourceGenerationContext.Default.FullConfig),
            await LoadJsonFromZipAsync(zipFile, "upgradeConfig.json", SourceGenerationContext.Default.UpgradeConfig));
    }

    private static async Task<T> LoadJsonFromZipAsync<T>(ZipArchive archive, string entryName, JsonTypeInfo<T> jsonTypeInfo)
    {
        var entry = archive.GetEntry(entryName) ?? throw new InvalidOperationException($"Missing {entryName}");
        await using var entryStream = entry.Open();
        using var reader = new StreamReader(entryStream);
        var json = await reader.ReadToEndAsync();

        return JsonSerializer.Deserialize(json, jsonTypeInfo) ?? throw new InvalidOperationException($"Invalid {entryName}");
    }

    public static VsCodeModel BuildVsCodeModel(VsCodeConfig config)
    {
        VsCodeModel model = new(config.VsCodeVersion);
        model.Platforms.Add(Platform.Win64, new VsCodeModel.PlatformData(config.VsCodeWindowsUrl, config.VsCodeWindowsName, config.VsCodeWindowsHash, config.VsCodeWindowsSize));
        model.Platforms.Add(Platform.WinArm64, new VsCodeModel.PlatformData(config.VsCodeWindowsArm64Url, config.VsCodeWindowsArm64Name, config.VsCodeWindowsArm64Hash, config.VsCodeWindowsArm64Size));
        model.Platforms.Add(Platform.Linux64, new VsCodeModel.PlatformData(config.VsCodeLinuxUrl, config.VsCodeLinuxName, config.VsCodeLinuxHash, config.VsCodeLinuxSize));
        model.Platforms.Add(Platform.LinuxArm64, new VsCodeModel.PlatformData(config.VsCodeLinuxArm64Url, config.VsCodeLinuxArm64Name, config.VsCodeLinuxArm64Hash, config.VsCodeLinuxArm64Size));
        model.Platforms.Add(Platform.Mac64, new VsCodeModel.PlatformData(config.VsCodeMacUrl, config.VsCodeMacName, config.VsCodeMacHash, config.VsCodeMacSize));
        model.Platforms.Add(Platform.MacArm64, new VsCodeModel.PlatformData(config.VsCodeMacUrl, config.VsCodeMacName, config.VsCodeMacHash, config.VsCodeMacSize));
        return model;
    }

    public static string? GetNeededInstallerType(UpgradeConfig upgradeConfig)
    {
        if (OperatingSystem.IsWindows())
        {
            return upgradeConfig.InstallerType == UpgradeConfig.WindowsInstallerType ? null : UpgradeConfig.WindowsInstallerType;
        }

        if (OperatingSystem.IsMacOS())
        {
            if (PlatformUtils.CurrentPlatform == Platform.MacArm64)
            {
                return upgradeConfig.InstallerType == UpgradeConfig.MacArmInstallerType ? null : UpgradeConfig.MacArmInstallerType;
            }

            return upgradeConfig.InstallerType == UpgradeConfig.MacInstallerType ? null : UpgradeConfig.MacInstallerType;
        }

        if (OperatingSystem.IsLinux())
        {
            if (PlatformUtils.CurrentPlatform == Platform.LinuxArm64)
            {
                return upgradeConfig.InstallerType == UpgradeConfig.LinuxArm64InstallerType ? null : UpgradeConfig.LinuxArm64InstallerType;
            }

            return upgradeConfig.InstallerType == UpgradeConfig.LinuxInstallerType ? null : UpgradeConfig.LinuxInstallerType;
        }

        return "Unknown";
    }

    public static string GetDefaultInstallDirectory(UpgradeConfig upgradeConfig)
    {
        var publicFolder = Environment.GetEnvironmentVariable("PUBLIC");
        if (publicFolder == null)
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                publicFolder = "C:\\Users\\Public";
            }
            else
            {
                publicFolder = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            }
        }

        return Path.Combine(publicFolder, "wpilib", upgradeConfig.WpilibYear);
    }

    public static async Task<bool> MacArtifactsChecksumMatchesAsync(string artifactsFile, string baseDirectory)
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            return true;
        }

        string expectedHash = File.ReadAllText(Path.Join(baseDirectory, ChecksumFileName)).Trim();
        await using var fileStream = File.OpenRead(artifactsFile);
        using var sha256 = SHA256.Create();
        var actualHash = Convert.ToHexString(await sha256.ComputeHashAsync(fileStream));

        return actualHash.Equals(expectedHash, StringComparison.OrdinalIgnoreCase);
    }

    private static void FindInstallerFilesInDirectory(
        string directory,
        string expectedResourcesFileName,
        string expectedArtifactsFileName,
        ref string? resourcesFile,
        ref string? artifactsFile)
    {
        if (!Directory.Exists(directory))
        {
            return;
        }

        foreach (var file in Directory.EnumerateFiles(directory))
        {
            if (resourcesFile == null && file.EndsWith(expectedResourcesFileName, StringComparison.Ordinal))
            {
                resourcesFile = file;
            }
            else if (artifactsFile == null && file.EndsWith(expectedArtifactsFileName, StringComparison.Ordinal))
            {
                artifactsFile = file;
            }

            if (resourcesFile != null && artifactsFile != null)
            {
                return;
            }
        }
    }
}
