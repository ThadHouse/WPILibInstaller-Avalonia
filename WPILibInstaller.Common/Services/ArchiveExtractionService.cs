using System.Diagnostics;
using WPILibInstaller.Interfaces;
using WPILibInstaller.Models;

namespace WPILibInstaller.Services
{
    public sealed class ArchiveExtractionService : IArchiveExtractionService
    {
        private readonly IConfigurationProvider configurationProvider;
        private readonly IProgramWindow? programWindow;

        public ArchiveExtractionService(IConfigurationProvider configurationProvider, IProgramWindow? programWindow = null)
        {
            this.configurationProvider = configurationProvider;
            this.programWindow = programWindow;
        }

        public Task ExtractJDKAndTools(CancellationToken token, IProgress<InstallProgress>? progress = null)
        {
            return ExtractArchive(token, new[]
            {
                configurationProvider.JdkConfig.Folder + "/",
                configurationProvider.UpgradeConfig.Tools.Folder + "/",
                configurationProvider.AdvantageScopeConfig.Folder + "/",
                configurationProvider.ElasticConfig.Folder + "/",
                "installUtils/",
                "icons",
            }, progress);
        }

        public async Task ExtractArchive(CancellationToken token, string[]? filter = null, IProgress<InstallProgress>? progress = null)
        {
            progress?.Report(new InstallProgress(0, "Starting extraction"));

            if (OperatingSystem.IsWindows())
            {
                progress?.Report(new InstallProgress(0, "Checking for currently running JDKs"));
                bool foundRunningExe = await Task.Run(() => FindRunningJdkProcess(), token);
                if (foundRunningExe)
                {
                    string message = "Running JDK processes have been found. Installation cannot continue. Please restart your computer, and rerun this installer without running anything else (including VS Code)";
                    if (programWindow != null)
                    {
                        await programWindow.ShowMessageDialog("JDKs Running", message);
                    }

                    throw new InvalidOperationException(message);
                }
            }

            var extractor = configurationProvider.ZipArchive;

            double totalSize = extractor.TotalUncompressSize;
            // Workaround for https://github.com/wpilibsuite/WPILibInstaller-Avalonia/issues/632.
            // Works as long as archive is > 2 GB and < 6 GB uncompressed.
            if (OperatingSystem.IsLinux() && totalSize < (long)2 * 1024 * 1024 * 1024)
            {
                totalSize += (long)4 * 1024 * 1024 * 1024;
            }

            long currentSize = 0;
            string intoPath = configurationProvider.InstallDirectory;

            while (await extractor.MoveToNextEntryAsync())
            {
                if (token.IsCancellationRequested)
                {
                    return;
                }

                currentSize += extractor.EntrySize;
                if (extractor.EntryIsDirectory)
                {
                    continue;
                }

                var entryName = extractor.EntryKey;
                if (filter != null && !filter.Any(keep => entryName.StartsWith(keep, StringComparison.Ordinal)))
                {
                    continue;
                }

                double currentPercentage = (currentSize / totalSize) * 100;
                currentPercentage = Math.Clamp(currentPercentage, 0, 100);
                progress?.Report(new InstallProgress((int)currentPercentage, "Installing " + entryName));

                string fullZipToPath = Path.Combine(intoPath, entryName);
                string? directoryName = Path.GetDirectoryName(fullZipToPath);
                if (directoryName?.Length > 0)
                {
                    try
                    {
                        Directory.CreateDirectory(directoryName);
                    }
                    catch (IOException)
                    {
                    }
                }

                await extractor.CopyToFileAsync(fullZipToPath, token);

                if (extractor.EntryIsExecutable && !OperatingSystem.IsWindows())
                {
                    var currentMode = File.GetUnixFileMode(fullZipToPath);
                    File.SetUnixFileMode(fullZipToPath, currentMode | UnixFileMode.GroupExecute | UnixFileMode.UserExecute | UnixFileMode.OtherExecute);
                }
            }
        }

        private bool FindRunningJdkProcess()
        {
            try
            {
                var jdkBinFolder = Path.Join(configurationProvider.InstallDirectory, configurationProvider.JdkConfig.Folder, "bin");
                var jdkExes = Directory.EnumerateFiles(jdkBinFolder, "*.exe", SearchOption.AllDirectories);
                foreach (var exe in jdkExes)
                {
                    try
                    {
                        var name = Path.GetFileNameWithoutExtension(exe)!;
                        var processes = Process.GetProcessesByName(name);
                        foreach (var process in processes)
                        {
                            if (process.MainModule?.FileName == exe)
                            {
                                return true;
                            }
                        }
                    }
                    catch
                    {
                        // Best-effort check only.
                    }
                }

                return false;
            }
            catch
            {
                // Best-effort check only.
                return false;
            }
        }
    }
}
