using System.Runtime.InteropServices;
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
        public string VerString { get; } = $"0.0.0";

        public void Initialize()
        {
            var installerFiles = InstallerResources.FindInstallerFiles(VerString, AppContext.BaseDirectory);
            var selectResources = installerFiles.ResourcesFile == null ? null : SelectResourceFilesWithFile(installerFiles.ResourcesFile);
            var selectSupport = installerFiles.ArtifactsFile == null ? null : SelectSupportFilesWithFile(installerFiles.ArtifactsFile);

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

            VerString = InstallerResources.ReadInstallerVersion(AppContext.BaseDirectory);
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
            var configuration = await InstallerResources.LoadConfigurationAsync(file);
            VsCodeConfig = configuration.VsCodeConfig;
            JdkConfig = configuration.JdkConfig;
            AdvantageScopeConfig = configuration.AdvantageScopeConfig;
            ElasticConfig = configuration.ElasticConfig;
            FullConfig = configuration.FullConfig;
            UpgradeConfig = configuration.UpgradeConfig;

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
            return InstallerResources.GetNeededInstallerType(UpgradeConfig);
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
            MissingSupportFiles = false;

            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                MissingHash = true;

                // Make sure they match.
                if (!await InstallerResources.MacArtifactsChecksumMatchesAsync(file, AppContext.BaseDirectory))
                {
                    viewModelResolver.ResolveMainWindow().HandleException(new InvalidDataException("The artifacts file was damaged.\nThis is either caused by a bad download,\nor on macOS you originally download the wrong dmg\nand its still mounted. Make sure to eject\nall dmg's and try again (And maybe reboot)."));
                    return false;
                }
                MissingHash = false;
            }

            MissingSupportFiles = false;
            forwardVisible = !MissingEitherFile && !MissingHash;
            refresher.RefreshForwardBackProperties();

            var fileStream = File.OpenRead(file);
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
                return InstallerResources.BuildVsCodeModel(VsCodeConfig);
            }
        }

        public string InstallDirectory
        {
            get
            {
                return InstallerResources.GetDefaultInstallDirectory(UpgradeConfig);
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
