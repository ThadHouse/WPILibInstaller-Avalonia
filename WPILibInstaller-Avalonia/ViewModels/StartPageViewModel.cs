using System.IO.Compression;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WPILibInstaller.Interfaces;
using WPILibInstaller.Models;
using WPILibInstaller.Utils;

namespace WPILibInstaller.ViewModels
{
    public partial class StartPageViewModel : PageViewModelBase, IConfigurationProvider
    {

        private readonly IProgramWindow programWindow;
        private readonly IViewModelResolver viewModelResolver;
        private readonly IMainWindowViewModel refresher;

        public override bool ForwardVisible => forwardVisible;
        private bool forwardVisible;
        public string VerString => verString;

        private readonly string verString = $"0.0.0";

        public void Initialize()
        {
            var baseDir = AppContext.BaseDirectory;

            var extension = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "zip" : "tar.gz";

            bool foundResources = false;
            bool foundSupport = false;

            Task? selectResources = null;
            Task? selectSupport = null;

            // Enumerate all files in base dir
            foreach (var file in Directory.EnumerateFiles(baseDir))
            {
                if (file.EndsWith($"{verString}-resources.zip", StringComparison.Ordinal))
                {
                    selectResources = SelectResourceFilesWithFile(file);
                    foundResources = true;
                }
                else if (file.EndsWith($"{verString}-artifacts.{extension}", StringComparison.Ordinal))
                {
                    selectSupport = SelectSupportFilesWithFile(file);
                    foundSupport = true;
                }
            }

            // Assume app is running in a translocated process.
            if ((!foundResources || !foundSupport) && RuntimeInformation.IsOSPlatform(OSPlatform.OSX)
                && Directory.Exists("/Volumes/WPILibInstaller"))
            {
                baseDir = Path.GetFullPath("/Volumes/WPILibInstaller");
                foreach (var file in Directory.EnumerateFiles(baseDir))
                {
                    if (!foundResources && file.EndsWith($"{verString}-resources.zip", StringComparison.Ordinal))
                    {
                        selectResources = SelectResourceFilesWithFile(file);
                        foundResources = true;
                    }
                    else if (!foundSupport && file.EndsWith($"{verString}-artifacts.{extension}", StringComparison.Ordinal))
                    {
                        selectSupport = SelectSupportFilesWithFile(file);
                        foundSupport = true;
                    }
                }
            }

            // Look beside the .app
            if ((!foundResources || !foundSupport) && RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                // Go back 3 directories to back out of mac package
                baseDir = Path.GetFullPath(Path.Join(baseDir, "..", "..", ".."));
                foreach (var file in Directory.EnumerateFiles(baseDir))
                {
                    if (!foundResources && file.EndsWith($"{verString}-resources.zip", StringComparison.Ordinal))
                    {
                        selectResources = SelectResourceFilesWithFile(file);
                        foundResources = true;
                    }
                    else if (!foundSupport && file.EndsWith($"{verString}-artifacts.{extension}", StringComparison.Ordinal))
                    {
                        selectSupport = SelectSupportFilesWithFile(file);
                        foundSupport = true;
                    }
                }
            }

            List<Task> awaitingTasks = [];
            if (selectResources != null)
            {
                awaitingTasks.Add(selectResources);
            }
            if (selectSupport != null)
            {
                awaitingTasks.Add(selectSupport);
            }

            _ = Task.WhenAll(awaitingTasks).ContinueWith(t =>
            {
                if (t.IsFaulted)
                {
                    Console.WriteLine("Error during initialization: " + t.Exception);
                    viewModelResolver.ResolveMainWindow().HandleException(t.Exception);
                }
            });
        }

        public StartPageViewModel(IMainWindowViewModel mainRefresher, IProgramWindow mainWindow, IViewModelResolver viewModelResolver)
            : base("Start", "")
        {
            try
            {
                var rootDirectory = Directory.GetDirectoryRoot(Environment.GetFolderPath(Environment.SpecialFolder.Personal));

                var driveInfo = new DriveInfo(rootDirectory);

                if (driveInfo.AvailableFreeSpace < 3L * 1000L * 1000L * 1000L)
                {
                    ;
                    // Fail
                }
            }
            catch
            {
                // Do nothing if we couldn't determine the drive
            }

            this.programWindow = mainWindow;
            this.viewModelResolver = viewModelResolver;
            refresher = mainRefresher;

            var baseDir = AppContext.BaseDirectory;

            try
            {
                verString = File.ReadAllText(Path.Join(baseDir, "WPILibInstallerVersion.txt")).Trim();
            }
            catch
            {
            }
        }

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(MissingEitherFile))]
        public partial bool MissingSupportFiles { get; set; } = true;

        [ObservableProperty]
        public partial bool MissingHash { get; set; } = false;

        public bool MissingEitherFile => MissingSupportFiles || MissingResourceFiles;

        public bool MacOSEject => MissingEitherFile && RuntimeInformation.IsOSPlatform(OSPlatform.OSX);

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(MissingEitherFile))]
        public partial bool MissingResourceFiles { get; set; } = true;

        private async Task<bool> SelectResourceFilesWithFile(string file)
        {
            var zipArchive = ZipFile.OpenRead(file);

            var entry = zipArchive.GetEntry("vscodeConfig.json");

            if (entry == null)
            {
                return false;
            }

            using (StreamReader reader = new StreamReader(entry!.Open()))
            {
                var vsConfigStr = await reader.ReadToEndAsync();
                VsCodeConfig = JsonSerializer.Deserialize(vsConfigStr, SourceGenerationContext.Default.VsCodeConfig) ?? throw new InvalidOperationException("Not Valid");
            }

            entry = zipArchive.GetEntry("jdkConfig.json");

            using (StreamReader reader = new StreamReader(entry!.Open()))
            {
                var configStr = await reader.ReadToEndAsync();
                JdkConfig = JsonSerializer.Deserialize(configStr, SourceGenerationContext.Default.JdkConfig) ?? throw new InvalidOperationException("Not Valid");
            }

            entry = zipArchive.GetEntry("advantageScopeConfig.json");

            using (StreamReader reader = new StreamReader(entry!.Open()))
            {
                var configStr = await reader.ReadToEndAsync();
                AdvantageScopeConfig = JsonSerializer.Deserialize(configStr, SourceGenerationContext.Default.AdvantageScopeConfig) ?? throw new InvalidOperationException("Not Valid");
            }

            entry = zipArchive.GetEntry("elasticConfig.json");

            using (StreamReader reader = new StreamReader(entry!.Open()))
            {
                var configStr = await reader.ReadToEndAsync();
                ElasticConfig = JsonSerializer.Deserialize(configStr, SourceGenerationContext.Default.ElasticConfig) ?? throw new InvalidOperationException("Not Valid");
            }

            entry = zipArchive.GetEntry("fullConfig.json");

            using (StreamReader reader = new StreamReader(entry!.Open()))
            {
                var configStr = await reader.ReadToEndAsync();
                FullConfig = JsonSerializer.Deserialize(configStr, SourceGenerationContext.Default.FullConfig) ?? throw new InvalidOperationException("Not Valid");
            }

            entry = zipArchive.GetEntry("upgradeConfig.json");

            using (StreamReader reader = new StreamReader(entry!.Open()))
            {
                var configStr = await reader.ReadToEndAsync();
                UpgradeConfig = JsonSerializer.Deserialize(configStr, SourceGenerationContext.Default.UpgradeConfig) ?? throw new InvalidOperationException("Not Valid");
            }

            string? neededInstaller = CheckInstallerType();
            if (neededInstaller == null)
            {
                MissingResourceFiles = false;
                forwardVisible = !MissingEitherFile && !MissingHash;
                refresher.RefreshForwardBackProperties();

                return true;
            }
            else
            {
                viewModelResolver.ResolveMainWindow().HandleException(new IncorrectPlatformException(neededInstaller, UpgradeConfig.InstallerType));
                return false;
            }
        }


        private string? CheckInstallerType()
        {
            if (OperatingSystem.IsWindows())
            {
                if (UpgradeConfig.InstallerType != UpgradeConfig.WindowsInstallerType)
                {
                    return UpgradeConfig.WindowsInstallerType;
                }
            }
            else if (OperatingSystem.IsMacOS())
            {
                if (PlatformUtils.CurrentPlatform == Platform.MacArm64)
                {
                    if (UpgradeConfig.InstallerType != UpgradeConfig.MacArmInstallerType)
                    {
                        return UpgradeConfig.MacArmInstallerType;
                    }
                }
                else
                {
                    if (UpgradeConfig.InstallerType != UpgradeConfig.MacInstallerType)
                    {
                        return UpgradeConfig.MacInstallerType;
                    }
                }

            }
            else if (OperatingSystem.IsLinux())
            {
                if (PlatformUtils.CurrentPlatform == Platform.LinuxArm64)
                {
                    if (UpgradeConfig.InstallerType != UpgradeConfig.LinuxArm64InstallerType)
                    {
                        return UpgradeConfig.LinuxArm64InstallerType;
                    }
                }
                else
                {
                    if (UpgradeConfig.InstallerType != UpgradeConfig.LinuxInstallerType)
                    {
                        return UpgradeConfig.LinuxInstallerType;
                    }
                }
            }
            else
            {
                return "Unknown";
            }
            return null;
        }

        [RelayCommand]
        public async Task SelectResourceFiles()
        {
            var file = await programWindow.ShowFilePicker("Select Resource File", "zip", Environment.GetFolderPath(Environment.SpecialFolder.Personal));

            if (file == null)
            {
                return;
            }

            await SelectResourceFilesWithFile(file);
        }

        private async Task<bool> SelectSupportFilesWithFile(string file)
        {
            FileStream fileStream = File.OpenRead(file);
            MissingSupportFiles = false;

            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                MissingHash = true;
                // Read the original hash.
                string hash = File.ReadAllText(Path.Join(AppContext.BaseDirectory, "checksum.txt")).Trim();

                // Compute the hash of the file that exists.
                string s;
                using (SHA256 SHA256 = SHA256.Create())
                {
                    s = Convert.ToHexString(await SHA256.ComputeHashAsync(fileStream));
                }

                // Make sure they match.
                if (!s.Equals(hash, StringComparison.OrdinalIgnoreCase))
                {
                    viewModelResolver.ResolveMainWindow().HandleException(new InvalidDataException("The artifacts file was damaged.\nThis is either caused by a bad download,\nor on macOS you originally download the wrong dmg\nand its still mounted. Make sure to eject\nall dmg's and try again (And maybe reboot)."));
                    return false;
                }
                MissingHash = false;
            }

            MissingSupportFiles = false;
            forwardVisible = !MissingEitherFile && !MissingHash;
            refresher.RefreshForwardBackProperties();

            fileStream.Position = 0;
            ZipArchive = ArchiveUtils.OpenArchive(fileStream);

            return true;
        }

        [RelayCommand]
        public async Task SelectSupportFiles()
        {
            var file = await programWindow.ShowFilePicker("Select Artifact File", RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "zip" : "gz", Environment.GetFolderPath(Environment.SpecialFolder.Personal));

            if (file == null)
            {
                return;
            }

            await SelectSupportFilesWithFile(file);
        }

        public VsCodeModel VsCodeModel
        {
            get
            {
                VsCodeModel model = new VsCodeModel(VsCodeConfig.VsCodeVersion);
                model.Platforms.Add(Utils.Platform.Win64, new VsCodeModel.PlatformData(VsCodeConfig.VsCodeWindowsUrl, VsCodeConfig.VsCodeWindowsName, VsCodeConfig.VsCodeWindowsHash, VsCodeConfig.VsCodeWindowsSize));
                model.Platforms.Add(Utils.Platform.Linux64, new VsCodeModel.PlatformData(VsCodeConfig.VsCodeLinuxUrl, VsCodeConfig.VsCodeLinuxName, VsCodeConfig.VsCodeLinuxHash, VsCodeConfig.VsCodeLinuxSize));
                model.Platforms.Add(Utils.Platform.LinuxArm64, new VsCodeModel.PlatformData(VsCodeConfig.VsCodeLinuxArm64Url, VsCodeConfig.VsCodeLinuxArm64Name, VsCodeConfig.VsCodeLinuxArm64Hash, VsCodeConfig.VsCodeLinuxArm64Size));
                model.Platforms.Add(Utils.Platform.Mac64, new VsCodeModel.PlatformData(VsCodeConfig.VsCodeMacUrl, VsCodeConfig.VsCodeMacName, VsCodeConfig.VsCodeMacHash, VsCodeConfig.VsCodeMacSize));
                model.Platforms.Add(Utils.Platform.MacArm64, new VsCodeModel.PlatformData(VsCodeConfig.VsCodeMacUrl, VsCodeConfig.VsCodeMacName, VsCodeConfig.VsCodeMacHash, VsCodeConfig.VsCodeMacSize));
                return model;
            }
        }

        public string InstallDirectory
        {
            get
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
                return Path.Combine(publicFolder, "wpilib", UpgradeConfig.WpilibYear);
            }
        }

        public override PageViewModelBase MoveNext()
        {
            if (OperatingSystem.IsWindows())
            {
                bool isWindows10 = OperatingSystem.IsWindowsVersionAtLeast(10);
                bool is64Bit = IntPtr.Size == 8;
                if (!isWindows10 || !is64Bit || (isWindows10 && Environment.OSVersion.Version.Build < 22000))
                {
                    return viewModelResolver.Resolve<DeprecatedOsPageViewModel>();
                }
            }

            return viewModelResolver.Resolve<ConfigurationPageViewModel>();
        }

        public IArchiveExtractor ZipArchive { get; private set; } = null!;

        public UpgradeConfig UpgradeConfig { get; private set; } = null!;

        public FullConfig FullConfig { get; private set; } = null!;

        public JdkConfig JdkConfig { get; private set; } = null!;

        public AdvantageScopeConfig AdvantageScopeConfig { get; private set; } = null!;

        public ElasticConfig ElasticConfig { get; private set; } = null!;

        public VsCodeConfig VsCodeConfig { get; private set; } = null!;

    }
}
