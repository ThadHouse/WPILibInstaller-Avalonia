using System.Formats.Tar;
using System.IO.Compression;
using System.Runtime.InteropServices;
using System.Text.Json;
using ToolsUpdater;

async Task InstallElastic(string toolsPath)
{
    if (OperatingSystem.IsMacOS())
    {
        var archiveFileName = "Elastic-WPILib-macOS.tar.gz";
        var elasticFolder = Path.Combine(Path.GetDirectoryName(toolsPath)!, "elastic");
        var archivePath = Path.Combine(elasticFolder, archiveFileName);
        await TarFile.ExtractToDirectoryAsync(archivePath, elasticFolder, overwriteFiles: true);
    }
    Console.WriteLine("Installed Elastic");
}

async Task InstallAdvantageScope(string toolsPath)
{
    if (OperatingSystem.IsMacOS())
    {
        string archName = RuntimeInformation.ProcessArchitecture == Architecture.Arm64 ? "arm64" : "x64";
        var archiveFileName = $"advantagescope-wpilib-mac-{archName}.tar.gz";
        var advantageScopeFolder = Path.Combine(Path.GetDirectoryName(toolsPath)!, "advantagescope");
        var archivePath = Path.Combine(advantageScopeFolder, archiveFileName);
        await TarFile.ExtractToDirectoryAsync(archivePath, advantageScopeFolder, overwriteFiles: true);
    }
    Console.WriteLine("Installed AdvantageScope");
}

static string DesktopArch()
{
    if (OperatingSystem.IsMacOS())
    {
        return "universal";
    }
    string archName = RuntimeInformation.ProcessArchitecture == Architecture.Arm64 ? "arm64" : "x86-64";
    return archName;
}

static string DesktopOS()
{
    return OperatingSystem.IsWindows() ? "windows" : OperatingSystem.IsMacOS() ? "osx" : "linux";
}

static string GetPlatformPath()
{
    return DesktopOS() + "/" + DesktopArch();
}

static void CopyDirectory(string sourceDir, string destDir)
{
    Directory.CreateDirectory(destDir);

    foreach (var file in Directory.GetFiles(sourceDir))
    {
        var destFile = Path.Combine(destDir, Path.GetFileName(file));
        File.Copy(file, destFile, overwrite: true);
    }

    foreach (var dir in Directory.GetDirectories(sourceDir))
    {
        var destSubDir = Path.Combine(destDir, Path.GetFileName(dir));
        CopyDirectory(dir, destSubDir);
    }
}

async Task InstallJavaTool(ToolConfig tool, string toolsPath)
{
    ArtifactConfig artifact = tool.Artifact!;
    string artifactFileName = $"{artifact.ArtifactId}-{tool.Version}";
    if (!string.IsNullOrWhiteSpace(artifact.Classifier))
    {
        artifactFileName += $"-{artifact.Classifier}";
    }
    artifactFileName += $".{artifact.Extension}";

    var artifactPath = Path.Combine(toolsPath, "artifacts", artifactFileName);
    Console.WriteLine("Copying from " + artifactPath);
    if (File.Exists(artifactPath))
    {
        var destPath = Path.Combine(toolsPath, $"{tool.Name}.jar");
        await Task.Run(() => File.Copy(artifactPath, destPath, overwrite: true));
    }
}

async Task InstallCppTool(ToolConfig tool, string toolsPath)
{
    ArtifactConfig artifact = tool.Artifact!;
    var artifactFileName = $"{artifact.ArtifactId}-{tool.Version}_{artifact.Classifier}.{artifact.Extension}";

    var artifactPath = Path.Combine(toolsPath, "artifacts", artifactFileName);
    Console.WriteLine("Extracting from " + artifactPath);
    if (File.Exists(artifactPath))
    {
        var tmpDir = Path.Combine(toolsPath, "tmp");
        await ZipFile.ExtractToDirectoryAsync(artifactPath, tmpDir, overwriteFiles: true);

        // Find glass folder
        var exeFolder = Path.Combine(tmpDir, GetPlatformPath());

        await Task.Run(() => CopyDirectory(exeFolder, toolsPath));

        Directory.Delete(tmpDir, recursive: true);
    }
}

var toolsPath = Path.GetDirectoryName(Environment.ProcessPath);
if (toolsPath is null)
{
    Console.WriteLine("Could not determine tools directory");
    return 1;
}

var jsonFile = Path.Combine(toolsPath, "tools.json");

if (!File.Exists(jsonFile))
{
    Console.WriteLine("Could not find tools.json");
    return 1;
}

var json = await File.ReadAllTextAsync(jsonFile);
var tools = JsonSerializer.Deserialize(json, ToolConfigContext.Default.ToolConfigArray);

if (tools is null || tools.Length == 0)
{
    Console.WriteLine("No tools to update");
    return 0;
}

List<Task> installTasks = [];

foreach (var tool in tools)
{
    if (!tool.IsValid)
    {
        Console.WriteLine($"Tool {tool.Name} is not valid, skipping");
        continue;
    }

    Console.WriteLine($"Updating tool {tool.Name} to version {tool.Version}");

    if (tool.Name == "AdvantageScope")
    {
        installTasks.Add(InstallAdvantageScope(toolsPath));
    }
    else if (tool.Name == "CMake")
    {
        installTasks.Add(InstallElastic(toolsPath));
    }
    else if (tool.Artifact is not null)
    {
        if (tool.Cpp)
        {
            installTasks.Add(InstallCppTool(tool, toolsPath));
        }
        else
        {
            installTasks.Add(InstallJavaTool(tool, toolsPath));
        }
    }
}

await Task.WhenAll(installTasks);
return 0;
