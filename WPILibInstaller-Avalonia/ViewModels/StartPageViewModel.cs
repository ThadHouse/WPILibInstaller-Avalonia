﻿using Newtonsoft.Json;
using ReactiveUI;
using System;
using System.IO;
using System.IO.Compression;
using System.Reactive;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using WPILibInstaller.Interfaces;
using WPILibInstaller.Models;
using WPILibInstaller.Utils;

namespace WPILibInstaller.ViewModels
{
    public class StartPageViewModel : PageViewModelBase, IConfigurationProvider
    {

        private readonly IProgramWindow programWindow;
        private readonly IViewModelResolver viewModelResolver;
        private readonly IMainWindowViewModel refresher;

        public override bool ForwardVisible => forwardVisible;
        private bool forwardVisible = false;

#pragma warning disable CS8618 // Non-nullable field is uninitialized. Consider declaring as nullable.
        public StartPageViewModel(IMainWindowViewModel mainRefresher, IProgramWindow mainWindow, IViewModelResolver viewModelResolver,
            ICatchableButtonFactory buttonFactory)
#pragma warning restore CS8618 // Non-nullable field is uninitialized. Consider declaring as nullable.
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

            SelectSupportFiles = buttonFactory.CreateCatchableButton(SelectSupportFilesFunc);
            SelectResourceFiles = buttonFactory.CreateCatchableButton(SelectResourceFilesFunc);

            this.programWindow = mainWindow;
            this.viewModelResolver = viewModelResolver;
            refresher = mainRefresher;

            var baseDir = AppContext.BaseDirectory;

            var version = typeof(StartPageViewModel).Assembly.GetName().Version!;
            var verString = $"{version.Major}.{version.Minor}.{version.Build}";

            if (OperatingSystem.IsWindows())
            {
                try
                {
                    verString = File.ReadAllText(Path.Join(baseDir, "WPILibInstallerVersion.txt")).Trim();
                }
                catch
                {
                }
            }

            var extension = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "zip" : "tar.gz";

            bool foundResources = false;
            bool foundSupport = false;

            // Enumerate all files in base dir
            foreach (var file in Directory.EnumerateFiles(baseDir))
            {
                if (file.EndsWith($"{verString}-resources.zip"))
                {
                    _ = SelectResourceFilesWithFile(file);
                    foundResources = true;
                }
                else if (file.EndsWith($"{verString}-artifacts.{extension}"))
                {
                    _ = SelectSupportFilesWithFile(file);
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
                    if (!foundResources && file.EndsWith($"{verString}-resources.zip"))
                    {
                        _ = SelectResourceFilesWithFile(file);
                        foundResources = true;
                    }
                    else if (!foundSupport && file.EndsWith($"{verString}-artifacts.{extension}"))
                    {
                        _ = SelectSupportFilesWithFile(file);
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
                    if (!foundResources && file.EndsWith($"{verString}-resources.zip"))
                    {
                        _ = SelectResourceFilesWithFile(file);
                        foundResources = true;
                    }
                    else if (!foundSupport && file.EndsWith($"{verString}-artifacts.{extension}"))
                    {
                        _ = SelectSupportFilesWithFile(file);
                        foundSupport = true;
                    }
                }
            }
        }

        public bool MissingSupportFiles
        {
            get => missingSupportFiles;
            set
            {
                this.RaiseAndSetIfChanged(ref missingSupportFiles, value);
                this.RaisePropertyChanged(nameof(MissingEitherFile));
            }
        }

        public bool MissingEitherFile => MissingSupportFiles || MissingResourceFiles;

        private bool missingSupportFiles = true;

        public bool MissingResourceFiles
        {
            get => missingResourceFiles;
            set
            {
                this.RaiseAndSetIfChanged(ref missingResourceFiles, value);
                this.RaisePropertyChanged(nameof(MissingEitherFile));
            }
        }

        private bool missingResourceFiles = true;


        public ReactiveCommand<Unit, Unit> SelectSupportFiles { get; }
        public ReactiveCommand<Unit, Unit> SelectResourceFiles { get; }

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
                VsCodeConfig = JsonConvert.DeserializeObject<VsCodeConfig>(vsConfigStr, new JsonSerializerSettings
                {
                    MissingMemberHandling = MissingMemberHandling.Error
                }) ?? throw new InvalidOperationException("Not Valid");
            }

            entry = zipArchive.GetEntry("jdkConfig.json");

            using (StreamReader reader = new StreamReader(entry!.Open()))
            {
                var configStr = await reader.ReadToEndAsync();
                JdkConfig = JsonConvert.DeserializeObject<JdkConfig>(configStr, new JsonSerializerSettings
                {
                    MissingMemberHandling = MissingMemberHandling.Error
                }) ?? throw new InvalidOperationException("Not Valid");
            }


            entry = zipArchive.GetEntry("fullConfig.json");

            using (StreamReader reader = new StreamReader(entry!.Open()))
            {
                var configStr = await reader.ReadToEndAsync();
                FullConfig = JsonConvert.DeserializeObject<FullConfig>(configStr, new JsonSerializerSettings
                {
                    MissingMemberHandling = MissingMemberHandling.Error
                }) ?? throw new InvalidOperationException("Not Valid");
            }


            entry = zipArchive.GetEntry("upgradeConfig.json");

            using (StreamReader reader = new StreamReader(entry!.Open()))
            {
                var configStr = await reader.ReadToEndAsync();
                UpgradeConfig = JsonConvert.DeserializeObject<UpgradeConfig>(configStr, new JsonSerializerSettings
                {
                    MissingMemberHandling = MissingMemberHandling.Error
                }) ?? throw new InvalidOperationException("Not Valid");
            }

            MissingResourceFiles = false;
            forwardVisible = !MissingEitherFile;
            refresher.RefreshForwardBackProperties();

            return true;
        }

        public async Task SelectResourceFilesFunc()
        {
            var file = await programWindow.ShowFilePicker("Select Resource File", Environment.GetFolderPath(Environment.SpecialFolder.Personal));

            if (file == null)
            {
                return;
            }

            await SelectResourceFilesWithFile(file);
        }

        private Task<bool> SelectSupportFilesWithFile(string file)
        {
            FileStream fileStream = File.OpenRead(file);

            ZipArchive = ArchiveUtils.OpenArchive(fileStream);

            // TODO Figure out how to verify this

            MissingSupportFiles = false;
            forwardVisible = !MissingEitherFile;
            refresher.RefreshForwardBackProperties();

            return Task.FromResult(true);
        }

        public async Task SelectSupportFilesFunc()
        {
            var file = await programWindow.ShowFilePicker("Select Support File", Environment.GetFolderPath(Environment.SpecialFolder.Personal));

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
                model.Platforms.Add(Utils.Platform.Win32, new VsCodeModel.PlatformData(VsCodeConfig.VsCode32Url, VsCodeConfig.VsCode32Name));
                model.Platforms.Add(Utils.Platform.Win64, new VsCodeModel.PlatformData(VsCodeConfig.VsCode64Url, VsCodeConfig.VsCode64Name));
                model.Platforms.Add(Utils.Platform.Linux64, new VsCodeModel.PlatformData(VsCodeConfig.VsCodeLinuxUrl, VsCodeConfig.VsCodeLinuxName));
                model.Platforms.Add(Utils.Platform.Mac64, new VsCodeModel.PlatformData(VsCodeConfig.VsCodeMacUrl, VsCodeConfig.VsCodeMacName));
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
                        publicFolder = Environment.GetFolderPath(Environment.SpecialFolder.Personal);
                    }
                }
                Console.WriteLine(publicFolder);
                return Path.Combine(publicFolder, "wpilib", UpgradeConfig.FrcYear);
            }
        }

        public override PageViewModelBase MoveNext()
        {
            return viewModelResolver.Resolve<VSCodePageViewModel>();
        }

        public IArchiveExtractor ZipArchive { get; private set; }

        public UpgradeConfig UpgradeConfig { get; private set; }

        public FullConfig FullConfig { get; private set; }

        public JdkConfig JdkConfig { get; private set; }

        public VsCodeConfig VsCodeConfig { get; private set; }

    }
}
