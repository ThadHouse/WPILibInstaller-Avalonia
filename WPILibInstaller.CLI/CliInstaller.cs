using System.Net.Http;
using System.Security.Cryptography;
using WPILibInstaller.Interfaces;
using WPILibInstaller.Models;
using WPILibInstaller.Services;
using WPILibInstaller.Utils;

namespace WPILibInstaller.CLI
{
    public sealed class CliInstaller : IConfigurationProvider, IToInstallProvider, IVsCodeInstallLocationProvider, IAsyncDisposable
    {
        private FileStream? artifactsStream;

        public VsCodeModel VsCodeModel { get; private set; } = null!;

        public IArchiveExtractor ZipArchive { get; private set; } = null!;

        public UpgradeConfig UpgradeConfig { get; private set; } = null!;

        public FullConfig FullConfig { get; private set; } = null!;

        public JdkConfig JdkConfig { get; private set; } = null!;

        public AdvantageScopeConfig AdvantageScopeConfig { get; private set; } = null!;

        public ElasticConfig ElasticConfig { get; private set; } = null!;

        public VsCodeConfig VsCodeConfig { get; private set; } = null!;

        public string InstallDirectory { get; private set; } = "";

        public InstallSelectionModel Model { get; } = new();

        VsCodeModel IVsCodeInstallLocationProvider.Model => VsCodeModel;

        InstallSelectionModel IToInstallProvider.Model => Model;

        public async Task<int> RunInstallAsync(bool allUsers, string installMode = "all", string? resourcesFileArgument = null, string? artifactsFileArgument = null, bool force = false)
        {
            try
            {
                Console.WriteLine("WPILib Installer - CLI Mode");
                Console.WriteLine("============================");
                Console.WriteLine();

                if (installMode != "all" && installMode != "tools")
                {
                    Console.Error.WriteLine($"Error: Invalid install mode '{installMode}'. Valid options are 'all' or 'tools'.");
                    return 1;
                }

                if (!TryResolveInstallerFiles(resourcesFileArgument, artifactsFileArgument, out var resourcesFile, out var artifactsFile, out var fileError))
                {
                    Console.Error.WriteLine($"Error: {fileError}");
                    return 1;
                }

                Console.WriteLine($"Found resources: {Path.GetFileName(resourcesFile)}");
                Console.WriteLine($"Found artifacts: {Path.GetFileName(artifactsFile)}");

                await LoadConfigurationAsync(resourcesFile, artifactsFile);

                string? neededInstaller = CheckInstallerType();
                if (neededInstaller != null)
                {
                    Console.Error.WriteLine($"Error: This installer is for {UpgradeConfig.InstallerType}, but this machine needs {neededInstaller}.");
                    return 1;
                }

                InstallDirectory = ComputeInstallDirectory();
                Model.InstallEverything = installMode == "all";
                Model.InstallTools = !Model.InstallEverything;
                Model.InstallAsAdmin = allUsers;

                Console.WriteLine($"Installing to: {InstallDirectory}");
                Console.WriteLine($"Install mode: {installMode}");
                Console.WriteLine();

                if (!force && !ConfirmInstall())
                {
                    Console.WriteLine("Installation cancelled.");
                    return 2;
                }

                using CancellationTokenSource cancellation = new();

                if (Model.InstallEverything)
                {
                    await DownloadAndPrepareVsCodeAsync(cancellation.Token);
                }

                var archiveService = new ArchiveExtractionService(this);
                var toolService = new ToolInstallationService(this);
                var vsCodeService = new VsCodeInstallationService(this, this);
                var shortcutService = new ShortcutService(this, this, this);

                var progress = new Progress<InstallProgress>(p =>
                {
                    if (!string.IsNullOrWhiteSpace(p.StatusText))
                    {
                        Console.WriteLine($"  {p.StatusText} ({p.Percentage}%)");
                    }
                });

                if (Model.InstallEverything)
                {
                    Console.WriteLine("[1/9] Extracting archive...");
                    await archiveService.ExtractArchive(cancellation.Token, null, progress);

                    Console.WriteLine("[2/9] Setting up Gradle...");
                    await toolService.RunGradleSetup(progress);

                    Console.WriteLine("[3/9] Setting up tools...");
                    await toolService.RunToolSetup(progress);

                    Console.WriteLine("[4/9] Setting up C++...");
                    await toolService.RunCppSetup(progress);

                    Console.WriteLine("[5/9] Fixing Maven metadata...");
                    await toolService.RunMavenMetaDataFixer(progress);

                    Console.WriteLine("[6/9] Installing VS Code...");
                    await vsCodeService.RunVsCodeSetup(cancellation.Token, progress);

                    Console.WriteLine("[7/9] Configuring VS Code settings...");
                    await vsCodeService.ConfigureVsCodeSettings();

                    Console.WriteLine("[8/9] Installing VS Code extensions...");
                    await vsCodeService.RunVsCodeExtensionsSetup(progress);

                    Console.WriteLine("[9/9] Creating shortcuts...");
                    await shortcutService.RunShortcutCreator(cancellation.Token);
                }
                else
                {
                    Console.WriteLine("[1/3] Extracting JDK and tools...");
                    await archiveService.ExtractJDKAndTools(cancellation.Token, progress);

                    Console.WriteLine("[2/3] Setting up tools...");
                    await toolService.RunToolSetup(progress);

                    Console.WriteLine("[3/3] Creating shortcuts...");
                    await shortcutService.RunShortcutCreator(cancellation.Token);
                }

                Console.WriteLine();
                Console.WriteLine("Installation completed successfully.");
                return 0;
            }
            catch (OperationCanceledException)
            {
                Console.Error.WriteLine("Installation cancelled.");
                return 2;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Installation failed: {ex.Message}");
                return 1;
            }
        }

        public async ValueTask DisposeAsync()
        {
            VsCodeModel?.Dispose();
            ZipArchive?.Dispose();
            if (artifactsStream != null)
            {
                await artifactsStream.DisposeAsync();
            }
        }

        private async Task LoadConfigurationAsync(string resourcesZipPath, string artifactsPath)
        {
            var configuration = await InstallerResources.LoadConfigurationAsync(resourcesZipPath);
            VsCodeConfig = configuration.VsCodeConfig;
            JdkConfig = configuration.JdkConfig;
            AdvantageScopeConfig = configuration.AdvantageScopeConfig;
            ElasticConfig = configuration.ElasticConfig;
            FullConfig = configuration.FullConfig;
            UpgradeConfig = configuration.UpgradeConfig;
            VsCodeModel = InstallerResources.BuildVsCodeModel(VsCodeConfig);

            if (!await InstallerResources.MacArtifactsChecksumMatchesAsync(artifactsPath, AppContext.BaseDirectory))
            {
                throw new InvalidDataException("The artifacts file was damaged. Make sure the correct macOS installer image is mounted and try again.");
            }

            artifactsStream = File.OpenRead(artifactsPath);
            ZipArchive = ArchiveUtils.OpenArchive(artifactsStream);
        }

        private static bool TryResolveInstallerFiles(
            string? resourcesFileArgument,
            string? artifactsFileArgument,
            out string resourcesFile,
            out string artifactsFile,
            out string errorMessage)
        {
            resourcesFile = "";
            artifactsFile = "";
            errorMessage = "";

            if (!TryNormalizeFileArgument(resourcesFileArgument, "Resources", out var providedResourcesFile, out errorMessage) ||
                !TryNormalizeFileArgument(artifactsFileArgument, "Artifacts", out var providedArtifactsFile, out errorMessage))
            {
                return false;
            }

            if (providedResourcesFile != null && providedArtifactsFile != null)
            {
                resourcesFile = providedResourcesFile;
                artifactsFile = providedArtifactsFile;
                return true;
            }

            var version = InstallerResources.ReadInstallerVersion(AppContext.BaseDirectory);
            var discoveredFiles = InstallerResources.FindInstallerFiles(version, AppContext.BaseDirectory);
            resourcesFile = providedResourcesFile ?? discoveredFiles.ResourcesFile ?? "";
            artifactsFile = providedArtifactsFile ?? discoveredFiles.ArtifactsFile ?? "";

            if (resourcesFile.Length == 0 && artifactsFile.Length == 0)
            {
                errorMessage = $"Could not find installer files. Expected {InstallerResources.ResourcesFileName(version)} and {InstallerResources.ArtifactsFileName(version)}.";
                return false;
            }

            if (resourcesFile.Length == 0)
            {
                errorMessage = $"Could not find resources file. Expected {InstallerResources.ResourcesFileName(version)} or pass --resources <path>.";
                return false;
            }

            if (artifactsFile.Length == 0)
            {
                errorMessage = $"Could not find artifacts file. Expected {InstallerResources.ArtifactsFileName(version)} or pass --artifacts <path>.";
                return false;
            }

            return true;
        }

        private static bool TryNormalizeFileArgument(string? fileArgument, string description, out string? file, out string errorMessage)
        {
            file = null;
            errorMessage = "";

            if (fileArgument == null)
            {
                return true;
            }

            file = Path.GetFullPath(fileArgument);
            if (File.Exists(file))
            {
                return true;
            }

            errorMessage = $"{description} file does not exist: {file}";
            return false;
        }

        private static IEnumerable<string> GetSearchDirectories()
        {
            HashSet<string> directories = [];

            foreach (var directory in InstallerResources.EnumerateBundledSearchDirectories(AppContext.BaseDirectory))
            {
                if (directories.Add(directory))
                {
                    yield return directory;
                }
            }

            var currentDirectory = Directory.GetCurrentDirectory();
            if (directories.Add(currentDirectory))
            {
                yield return currentDirectory;
            }
        }

        private string? CheckInstallerType()
        {
            return InstallerResources.GetNeededInstallerType(UpgradeConfig);
        }

        private string ComputeInstallDirectory()
        {
            return InstallerResources.GetDefaultInstallDirectory(UpgradeConfig);
        }

        private static bool ConfirmInstall()
        {
            Console.Write("This will install WPILib on this computer. Continue? [y/N]: ");
            var response = Console.ReadLine();
            response = response?.Trim();
            return response != null &&
                (response.Equals("y", StringComparison.OrdinalIgnoreCase) ||
                response.Equals("yes", StringComparison.OrdinalIgnoreCase));
        }

        private async Task DownloadAndPrepareVsCodeAsync(CancellationToken token)
        {
            var currentPlatform = PlatformUtils.CurrentPlatform;
            var platformData = VsCodeModel.Platforms[currentPlatform];

            Console.WriteLine("Downloading VS Code...");

            try
            {
                var (stream, hash) = await DownloadVsCodeForPlatformAsync(platformData.DownloadUrl, token);
                if (!hash.AsSpan().SequenceEqual(platformData.Sha256Hash))
                {
                    throw new InvalidOperationException($"VS Code hash mismatch. Expected {Convert.ToHexString(platformData.Sha256Hash)}, got {Convert.ToHexString(hash)}.");
                }

                PrepareVsCodeModelForInstallation(stream, currentPlatform);
                Console.WriteLine("VS Code ready for installation.");
            }
            catch (Exception ex) when (ex is HttpRequestException || ex is IOException || ex is TaskCanceledException)
            {
                Console.WriteLine($"VS Code download failed: {ex.Message}");
                Console.WriteLine("Checking for offline VS Code archive...");

                if (!TryFindOfflineVsCodeArchive(currentPlatform, out var archivePath))
                {
                    throw;
                }

                Console.WriteLine($"Found offline VS Code archive: {Path.GetFileName(archivePath)}");
                await LoadOfflineVsCodeArchive(archivePath, currentPlatform, platformData.Sha256Hash.ToArray(), token);
            }
        }

        private static async Task<(MemoryStream stream, byte[] hash)> DownloadVsCodeForPlatformAsync(string downloadUrl, CancellationToken token)
        {
            MemoryStream stream = new();
            using var client = new HttpClientDownloadWithProgress(downloadUrl, stream);
            client.ProgressChanged += (_, _, progressPercentage) =>
            {
                if (progressPercentage != null)
                {
                    Console.WriteLine($"  Downloading VS Code... {progressPercentage.Value:F2}%");
                }
            };

            await client.StartDownload();
            if (stream.Length == 0)
            {
                throw new IOException("0 bytes downloaded");
            }

            stream.Seek(0, SeekOrigin.Begin);
            using var sha = SHA256.Create();
            var hash = await sha.ComputeHashAsync(stream, token);
            stream.Seek(0, SeekOrigin.Begin);
            return (stream, hash);
        }

        private bool TryFindOfflineVsCodeArchive(Platform platform, out string archivePath)
        {
            archivePath = "";
            var expectedName = VsCodeModel.Platforms[platform].NameInZip;

            foreach (var directory in GetSearchDirectories())
            {
                if (!Directory.Exists(directory))
                {
                    continue;
                }

                var exactPath = Path.Combine(directory, expectedName);
                if (File.Exists(exactPath))
                {
                    archivePath = exactPath;
                    return true;
                }

                foreach (var candidate in new[] { "vscode.zip", "vscode.tar.gz" })
                {
                    var candidatePath = Path.Combine(directory, candidate);
                    if (File.Exists(candidatePath))
                    {
                        archivePath = candidatePath;
                        return true;
                    }
                }

                foreach (var file in Directory.EnumerateFiles(directory))
                {
                    var name = Path.GetFileName(file);
                    var extension = Path.GetExtension(file);
                    if (name.Contains("vscode", StringComparison.OrdinalIgnoreCase) &&
                        (extension.Equals(".zip", StringComparison.OrdinalIgnoreCase) || extension.Equals(".gz", StringComparison.OrdinalIgnoreCase)))
                    {
                        archivePath = file;
                        return true;
                    }
                }
            }

            return false;
        }

        private async Task LoadOfflineVsCodeArchive(string archivePath, Platform platform, byte[] expectedHash, CancellationToken token)
        {
            await using var fileStream = File.OpenRead(archivePath);
            MemoryStream stream = new();
            await fileStream.CopyToAsync(stream, token);
            stream.Position = 0;

            using var sha = SHA256.Create();
            var hash = await sha.ComputeHashAsync(stream, token);

            if (!hash.AsSpan().SequenceEqual(expectedHash))
            {
                Console.WriteLine("WARNING: Hash mismatch for offline VS Code archive.");
                Console.WriteLine($"Expected: {Convert.ToHexString(expectedHash)}");
                Console.WriteLine($"Got:      {Convert.ToHexString(hash)}");
                Console.WriteLine("Continuing anyway.");
            }

            stream.Position = 0;
            PrepareVsCodeModelForInstallation(stream, platform);
            Console.WriteLine("VS Code ready for installation.");
        }

        private void PrepareVsCodeModelForInstallation(MemoryStream stream, Platform platform)
        {
            if (OperatingSystem.IsMacOS())
            {
                VsCodeModel.ToExtractArchiveMacOs = stream;
            }
            else
            {
                VsCodeModel.ToExtractArchive = ArchiveUtils.OpenArchive(stream);
            }
        }
    }
}
