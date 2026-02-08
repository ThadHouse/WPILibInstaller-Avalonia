using System.Security.Cryptography;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Input;
using MsBox.Avalonia;
using WPILibInstaller.Interfaces;
using WPILibInstaller.Models;
using WPILibInstaller.Utils;
using static WPILibInstaller.Utils.ArchiveUtils;

namespace WPILibInstaller.ViewModels
{
    public partial class VSCodePageViewModel : PageViewModelBase, IVsCodeInstallLocationProvider
    {

        public override bool ForwardVisible => forwardVisible;

        private bool forwardVisible;

        private void SetLocalForwardVisible(bool value)
        {
            forwardVisible = value;
            refresher.RefreshForwardBackProperties();
        }

        public bool EnableSelectionButtons
        {
            get => enableSelectionButtons;
            set
            {
                this.SetProperty(ref enableSelectionButtons, value);
            }
        }

        private bool enableSelectionButtons = true;

        public string SingleDownloadText
        {
            get => singleDownloadText;
            set => this.SetProperty(ref singleDownloadText, value);
        }

        public string SkipVsCodeText
        {
            get => skipVsCodeText;
            set => this.SetProperty(ref skipVsCodeText, value);
        }

        public string AllDownloadText
        {
            get => allDownloadText;
            set => this.SetProperty(ref allDownloadText, value);
        }

        public string SelectText
        {
            get => selectText;
            set => this.SetProperty(ref selectText, value);
        }

        private string singleDownloadText = "Download for this computer only\n(fastest)";
        private string skipVsCodeText = "Skip and don't use VS Code\n(NOT RECOMMENDED)";
        private string allDownloadText = "Download VS Code archives to share with\nother computers/OSes for offline\ninstall";
        private string selectText = "Select existing VS Code archive for\noffline install on this computer";

        public double ProgressBar1
        {
            get => progressBar1;
            set => this.SetProperty(ref progressBar1, value);
        }

        private double progressBar1;

        public bool ProgressBar1Visible
        {
            get => progressBar1Visible;
            set => this.SetProperty(ref progressBar1Visible, value);
        }

        private bool progressBar1Visible;

        public double ProgressBar2
        {
            get => progressBar2;
            set => this.SetProperty(ref progressBar2, value);
        }

        private double progressBar2;

        public bool ProgressBarAllVisible
        {
            get => progressBarAllVisible;
            set => this.SetProperty(ref progressBarAllVisible, value);
        }

        private bool progressBarAllVisible;

        public double ProgressBar3
        {
            get => progressBar3;
            set => this.SetProperty(ref progressBar3, value);
        }

        private double progressBar3;

        public double ProgressBar4
        {
            get => progressBar4;
            set => this.SetProperty(ref progressBar4, value);
        }

        private double progressBar4;

        public string DoneText
        {
            get => doneText;
            set => this.SetProperty(ref doneText, value);
        }

        private string doneText = "";

        public VsCodeModel Model { get; }

        private readonly IProgramWindow programWindow;
        private readonly IMainWindowViewModel refresher;
        private readonly IViewModelResolver viewModelResolver;

        public VSCodePageViewModel(IMainWindowViewModel mainRefresher, IProgramWindow programWindow, IConfigurationProvider modelProvider, IViewModelResolver viewModelResolver)
            : base("Next", "Back")
        {
            this.refresher = mainRefresher;
            this.programWindow = programWindow;
            Model = modelProvider.VsCodeModel;
            this.viewModelResolver = viewModelResolver;

            refresher.RefreshForwardBackProperties();

            // Check to see if VS Code is already installed
            var rootPath = modelProvider.InstallDirectory;
            var vscodePath = Path.Join(rootPath, "vscode");
            if (Directory.Exists(vscodePath))
            {
                DoneText = "VS Code already Installed. You can either download to reinstall, or click Next to skip";
                SetLocalForwardVisible(true);
                Model.AlreadyInstalled = true;
            }
        }

        [RelayCommand]
        public async Task SkipVsCode()
        {
            if (Model.AlreadyInstalled)
            {
                await viewModelResolver.ResolveMainWindow().ExecuteGoNext();
                return;
            }

            var result = await MessageBoxManager.GetMessageBoxStandard("Confirmation",
                "Are you sure you want to skip installing VS Code?\nA WPILib VS Code install was not detected.",
                icon: MsBox.Avalonia.Enums.Icon.None, @enum: MsBox.Avalonia.Enums.ButtonEnum.YesNo).ShowWindowDialogAsync(programWindow.Window);

            if (result == MsBox.Avalonia.Enums.ButtonResult.Yes)
            {
                await viewModelResolver.ResolveMainWindow().ExecuteGoNext();
            }
        }

        [RelayCommand]
        public async Task SelectVsCode()
        {
            var currentPlatform = PlatformUtils.CurrentPlatform;
            string extension;

            if (currentPlatform == Platform.Linux64 || currentPlatform == Platform.LinuxArm64)
            {
                extension = "tar.gz";
            }
            else
            {
                extension = "zip";
            }
            var file = await programWindow.ShowFilePicker("Select VS Code Installer ZIP", extension);
            if (file == null)
            {
                // No need to error, user explicitly canceled.
                return;
            }
            try
            {
                FileStream fs = new FileStream(file, FileMode.Open);
                MemoryStream ms = new MemoryStream(100000000);
                await fs.CopyToAsync(ms);
                ms.Seek(0, SeekOrigin.Begin);

                using var sha = SHA256.Create();
                var hash = await sha.ComputeHashAsync(ms);

                if (!hash.AsSpan().SequenceEqual(Model.Platforms[currentPlatform].Sha256Hash))
                {
                    bool cont = await CheckIncorrectHash($"VS Code for {currentPlatform}. File Location {file}", Convert.ToHexString(Model.Platforms[currentPlatform].Sha256Hash), Convert.ToHexString(hash));
                    if (!cont)
                    {
                        throw new InvalidDataException("Invalid hash");
                    }
                }

                ms.Seek(0, SeekOrigin.Begin);

                if (OperatingSystem.IsMacOS())
                {
                    Model.ToExtractArchiveMacOs = ms;
                }
                else
                {
                    Model.ToExtractArchive = OpenArchive(ms);
                }
            }
            catch
            {
                await MessageBoxManager.GetMessageBoxStandard("Error",
                    "You must select a VS Code zip downloaded with this tool.",
                    icon: MsBox.Avalonia.Enums.Icon.None).ShowWindowDialogAsync(programWindow.Window);
                return;
            }

            DoneText = "Valid VS Code Selected. Press Next to continue";
            EnableSelectionButtons = false;
            SetLocalForwardVisible(true);
        }

        private async Task<bool> CheckIncorrectHash(string name, string expected, string actual)
        {
            string msg = $"Invalid Hash for {name}\nExpected: {expected}\nActual: {actual}\nOK to ignore, Abort to cancel.\nIf cancelled, problems may occur";
            var res = await MsBox.Avalonia.MessageBoxManager.GetMessageBoxStandard(new MsBox.Avalonia.Dto.MessageBoxStandardParams
            {
                ContentTitle = "Invalid Hash",
                ContentMessage = msg,
                Icon = MsBox.Avalonia.Enums.Icon.Error,
                ButtonDefinitions = MsBox.Avalonia.Enums.ButtonEnum.OkAbort
            }).ShowWindowDialogAsync(programWindow.Window);
            return res == MsBox.Avalonia.Enums.ButtonResult.Ok;
        }

        [RelayCommand]
        public async Task DownloadVsCode()
        {
            var currentPlatform = PlatformUtils.CurrentPlatform;
            ProgressBar1Visible = true;
            ProgressBarAllVisible = true;

            var file = await programWindow.ShowFolderPicker("Select Directory For VS Code File", Environment.GetFolderPath(Environment.SpecialFolder.Personal));

            if (file == null)
            {
                return;
            }

            DoneText = "Downloading VS Code for all platforms. Please wait.";

            EnableSelectionButtons = false;
            SetLocalForwardVisible(false);

            var win64 = DownloadToMemoryStream(Platform.Win64, Model.Platforms[Platform.Win64].DownloadUrl, (d) => ProgressBar1 = d);
            var linux64 = DownloadToMemoryStream(Platform.Linux64, Model.Platforms[Platform.Linux64].DownloadUrl, (d) => ProgressBar2 = d);
            var linuxArm64 = DownloadToMemoryStream(Platform.LinuxArm64, Model.Platforms[Platform.LinuxArm64].DownloadUrl, (d) => ProgressBar3 = d);
            var mac64 = DownloadToMemoryStream(Platform.Mac64, Model.Platforms[Platform.Mac64].DownloadUrl, (d) => ProgressBar4 = d);

            var results = await Task.WhenAll(win64, linux64, linuxArm64, mac64);

            try
            {
                File.Delete(Path.Join(file, Model.Platforms[Platform.Win64].NameInZip));
                File.Delete(Path.Join(file, Model.Platforms[Platform.Linux64].NameInZip));
                File.Delete(Path.Join(file, Model.Platforms[Platform.LinuxArm64].NameInZip));
                File.Delete(Path.Join(file, Model.Platforms[Platform.Mac64].NameInZip));
            }
            catch
            {

            }

            MemoryStream? ms = null;

            DoneText = "Copying Archives. Please wait.";
            foreach (var (stream, platform, hash) in results)
            {
                if (!hash.AsSpan().SequenceEqual(Model.Platforms[platform].Sha256Hash))
                {
                    bool cont = await CheckIncorrectHash($"VS Code for {platform}", Convert.ToHexString(Model.Platforms[platform].Sha256Hash), Convert.ToHexString(hash));
                    if (!cont)
                    {
                        throw new InvalidDataException("Invalid hash");
                    }
                }

                using var toWriteStream = new FileStream(Path.Join(file, Model.Platforms[platform].NameInZip), FileMode.OpenOrCreate);
                stream.Seek(0, SeekOrigin.Begin);
                await stream.CopyToAsync(toWriteStream);
                if (platform == currentPlatform || (currentPlatform == Platform.MacArm64 && platform == Platform.Mac64))
                {
                    ms = stream;
                }
            }

            if (ms != null)
            {
                ms.Seek(0, SeekOrigin.Begin);

                if (OperatingSystem.IsMacOS())
                {
                    Model.ToExtractArchiveMacOs = ms;
                }
                else
                {
                    Model.ToExtractArchive = OpenArchive(ms);
                }

                DoneText = "Done Downloading. Press Next to continue";

                EnableSelectionButtons = true;
                SetLocalForwardVisible(true);
            }
        }

        [RelayCommand]
        public async Task DownloadSingleVsCode()
        {
            DoneText = "Downloading VS Code for current platform. Please wait.";
            Console.WriteLine("Single Download");
            var currentPlatform = PlatformUtils.CurrentPlatform;
            var url = Model.Platforms[currentPlatform].DownloadUrl;

            EnableSelectionButtons = false;
            SetLocalForwardVisible(false);
            ProgressBar1Visible = true;

            var (stream, platform, hash) = await DownloadToMemoryStream(currentPlatform, url, (d) => ProgressBar1 = d);

            if (!hash.AsSpan().SequenceEqual(Model.Platforms[platform].Sha256Hash))
            {
                bool cont = await CheckIncorrectHash($"VS Code for {platform}", Convert.ToHexString(Model.Platforms[platform].Sha256Hash), Convert.ToHexString(hash));
                if (!cont)
                {
                    throw new InvalidDataException("Invalid hash");
                }
            }
            Console.WriteLine("Trying to open archive");

            if (OperatingSystem.IsMacOS())
            {
                Model.ToExtractArchiveMacOs = stream;
            }
            else
            {
                Model.ToExtractArchive = OpenArchive(stream);
            }

            DoneText = "Done Downloading. Press Next to continue";
            EnableSelectionButtons = true;
            SetLocalForwardVisible(true);
            refresher.RefreshForwardBackProperties();
        }

        private static async Task<(MemoryStream stream, Platform platform, byte[] hash)> DownloadToMemoryStream(Platform platform, string downloadUrl, Action<double> progressChanged)
        {
            MemoryStream ms = new MemoryStream(100000000);
            await DownloadForPlatform(downloadUrl, ms, progressChanged);
            ms.Seek(0, SeekOrigin.Begin);
            using var sha = SHA256.Create();
            var hash = await sha.ComputeHashAsync(ms);
            return (ms, platform, hash);
        }

        private static async Task DownloadForPlatform(string downloadUrl, Stream outputStream, Action<double> progressChanged)
        {
            using var client = new HttpClientDownloadWithProgress(downloadUrl, outputStream);
            client.ProgressChanged += (totalFileSize, totalBytesDownloaded, progressPercentage) =>
            {
                if (progressPercentage != null)
                {
                    progressChanged?.Invoke(progressPercentage.Value);
                }
            };

            await client.StartDownload();
        }

        public override PageViewModelBase MoveNext()
        {
            return viewModelResolver.Resolve<InstallPageViewModel>();
        }
    }
}
