using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.Json.Nodes;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WPILibInstaller.Interfaces;
using WPILibInstaller.Models;
using WPILibInstaller.Utils;
using File = System.IO.File;

namespace WPILibInstaller.ViewModels
{
#pragma warning disable CA1001 // Types that own disposable fields should be disposable
    public partial class InstallPageViewModel : PageViewModelBase
#pragma warning restore CA1001 // Types that own disposable fields should be disposable
    {
        private readonly IViewModelResolver viewModelResolver;
        private readonly IToInstallProvider toInstallProvider;
        private readonly IConfigurationProvider configurationProvider;
        private readonly IVsCodeInstallLocationProvider vsInstallProvider;
        private readonly IProgramWindow programWindow;

        [ObservableProperty]
        private int _progress;

        [ObservableProperty]
        private string _text = "";
        [ObservableProperty]
        private int _progressTotal;
        [ObservableProperty]
        private string _textTotal = "";

        private async void CreateLinuxShortcut(string name, string frcYear, string wmClass, string iconName, CancellationToken token)
        {
            var launcherFile = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".local/share/applications", $@"{name.Replace(' ', '_').Replace(")", "").Replace("(", "")}_{frcYear}.desktop");
            string contents;
            if (name.Contains("WPILib"))
            {
                var nameNoWPILib = name.Remove(name.Length - " (WPILib)".Length);
                contents = $@"#!/usr/bin/env xdg-open
[Desktop Entry]
Version=1.0
Type=Application
Categories=Robotics;Science
Name={name} {frcYear}
Comment={nameNoWPILib} tool for the 2025 FIRST Robotics Competition season
Exec={configurationProvider.InstallDirectory}/tools/{nameNoWPILib}.sh
Icon={configurationProvider.InstallDirectory}/icons/{iconName}
Terminal=false
StartupNotify=true
StartupWMClass={wmClass}
";

            }
            else
            {
                contents = $@"#!/usr/bin/env xdg-open
[Desktop Entry]
Version=1.0
Type=Application
Categories=Robotics;Science
Name={name} {frcYear}
Comment={name} tool for the 2025 FIRST Robotics Competition season
Exec={configurationProvider.InstallDirectory}/tools/{name}.sh
Icon={configurationProvider.InstallDirectory}/icons/{iconName}
Terminal=false
StartupNotify=true
StartupWMClass={wmClass}
";
            }
            var launcherPath = Path.GetDirectoryName(launcherFile);
            if (launcherPath != null)
            {
                Directory.CreateDirectory(launcherPath);
            }
            await File.WriteAllTextAsync(launcherFile, contents, token);
        }

        public bool Succeeded { get; private set; }

        private readonly Task runInstallTask;

        public InstallPageViewModel(IViewModelResolver viewModelResolver, IToInstallProvider toInstallProvider, IConfigurationProvider configurationProvider, IVsCodeInstallLocationProvider vsInstallProvider,
            IProgramWindow programWindow)
            : base("", "")
        {
            this.viewModelResolver = viewModelResolver;
            this.toInstallProvider = toInstallProvider;
            this.configurationProvider = configurationProvider;
            this.vsInstallProvider = vsInstallProvider;
            this.programWindow = programWindow;
            runInstallTask = installFunc();

            async Task installFunc()
            {
                try
                {
                    await RunInstall();
                }
                catch (Exception e)
                {
                    viewModelResolver.ResolveMainWindow().HandleException(e);
                }
            }
        }

        private CancellationTokenSource? source;

        [RelayCommand]
        public async Task CancelInstall()
        {
            source?.Cancel();
            await runInstallTask;
        }

        private async Task ExtractJDKAndTools(CancellationToken token)
        {
            await ExtractArchive(new[] {
                configurationProvider.JdkConfig.Folder + "/",
                configurationProvider.UpgradeConfig.Tools.Folder + "/",
                configurationProvider.AdvantageScopeConfig.Folder + "/",
                configurationProvider.ElasticConfig.Folder + "/",
                "installUtils/", "icons"}, token);
        }

        private async Task InstallTools(CancellationToken token)
        {
            try
            {
                do
                {
                    ProgressTotal = 0;
                    TextTotal = "Extracting JDK and Tools";
                    await ExtractJDKAndTools(token);
                    if (token.IsCancellationRequested) break;
                    ProgressTotal = 33;
                    TextTotal = "Installing Tools";
                    await RunToolSetup();
                    ProgressTotal = 66;
                    TextTotal = "Creating Shortcuts";
                    await RunShortcutCreator(token);
                } while (false);
            }
            catch (OperationCanceledException)
            {
                // Do nothing, we just want to ignore
            }
        }

        private async Task InstallEverything(CancellationToken token)
        {
            try
            {
                do
                {
                    ProgressTotal = 0;
                    TextTotal = "Extracting";
                    await ExtractArchive(null, token);
                    if (token.IsCancellationRequested) break;
                    ProgressTotal = 11;
                    TextTotal = "Installing Gradle";
                    await RunGradleSetup();
                    if (token.IsCancellationRequested) break;
                    ProgressTotal = 22;
                    TextTotal = "Installing Tools";
                    await RunToolSetup();
                    if (token.IsCancellationRequested) break;
                    ProgressTotal = 33;
                    TextTotal = "Installing CPP";
                    await RunCppSetup();
                    if (token.IsCancellationRequested) break;
                    ProgressTotal = 44;
                    TextTotal = "Fixing Maven";
                    await RunMavenMetaDataFixer();
                    if (token.IsCancellationRequested) break;
                    ProgressTotal = 55;
                    TextTotal = "Installing VS Code";
                    await RunVsCodeSetup(token);
                    if (token.IsCancellationRequested) break;
                    ProgressTotal = 66;
                    TextTotal = "Configuring VS Code";
                    await ConfigureVsCodeSettings();
                    if (token.IsCancellationRequested) break;
                    ProgressTotal = 77;
                    TextTotal = "Installing VS Code Extensions";
                    await RunVsCodeExtensionsSetup();
                    if (token.IsCancellationRequested) break;
                    ProgressTotal = 88;
                    TextTotal = "Creating Shortcuts";
                    await RunShortcutCreator(token);
                } while (false);
            }
            catch (OperationCanceledException)
            {
                // Do nothing, we just want to ignore
            }
        }

        private async Task RunInstall()
        {
            source = new CancellationTokenSource();

            await Task.Yield();

            try
            {
                if (toInstallProvider.Model.InstallTools)
                {
                    await InstallTools(source.Token);
                }
                else
                {
                    await InstallEverything(source.Token);
                }
            }
            catch (OperationCanceledException)
            {
                // Ignore, as we just want to continue
            }

            if (source.IsCancellationRequested)
            {
                Succeeded = false;
            }
            else
            {
                Succeeded = true;
            }

            await viewModelResolver.ResolveMainWindow().ExecuteGoNext();
        }

        public override PageViewModelBase MoveNext()
        {
            if (Succeeded)
            {
                return viewModelResolver.Resolve<FinalPageViewModel>();
            }
            else
            {
                return viewModelResolver.Resolve<CanceledPageViewModel>();
            }
        }

        private ValueTask<string> SetVsCodePortableMode()
        {
            string portableFolder = Path.Combine(configurationProvider.InstallDirectory, "vscode");

            var currentPlatform = PlatformUtils.CurrentPlatform;
            switch (currentPlatform)
            {
                case Platform.Win64:
                    portableFolder = Path.Combine(portableFolder, "data");
                    break;
                case Platform.MacArm64:
                case Platform.Mac64:
                    portableFolder = Path.Combine(portableFolder, "code-portable-data");
                    break;
                case Platform.Linux64:
                    portableFolder = Path.Combine(portableFolder, "VSCode-linux-x64", "data");
                    break;
                case Platform.LinuxArm64:
                    portableFolder = Path.Combine(portableFolder, "VSCode-linux-arm64", "data");
                    break;
                default:
                    throw new PlatformNotSupportedException("Invalid platform");
            }

            try
            {
                Directory.CreateDirectory(portableFolder);
            }
            catch (IOException)
            {

            }

            return new ValueTask<string>(portableFolder);
        }

        private static void SetIfNotSet(string key, object value, JsonObject settingsJson)
        {
            if (!settingsJson.ContainsKey(key))
            {
                if (value is string strValue)
                {
                    settingsJson[key] = strValue;
                }
                else if (value is bool boolValue)
                {
                    settingsJson[key] = boolValue;
                }
                else
                {
                    throw new ArgumentException($"Unsupported value type for JSON: {value.GetType()}", nameof(value));
                }
            }
        }

        private async Task ConfigureVsCodeSettings()
        {
            if (!vsInstallProvider.Model.InstallExtensions) return;

            var dataPath = await SetVsCodePortableMode();

            var settingsDir = Path.Combine(dataPath, "user-data", "User");
            var settingsFile = Path.Combine(settingsDir, "settings.json");

            var homePath = configurationProvider.InstallDirectory;

            var codeFolder = Path.Combine(homePath, configurationProvider.UpgradeConfig.PathFolder);

            try
            {
                Directory.CreateDirectory(codeFolder);
            }
            catch (IOException)
            {

            }

            try
            {
                Directory.CreateDirectory(settingsDir);
            }
            catch (IOException)
            {

            }

            JsonObject settingsJson = new JsonObject();
            if (File.Exists(settingsFile))
            {
                settingsJson = JsonNode.Parse(await File.ReadAllTextAsync(settingsFile))?.AsObject() ?? new JsonObject();
            }

            SetIfNotSet("java.jdt.ls.java.home", Path.Combine(homePath, "jdk"), settingsJson);
            SetIfNotSet("extensions.autoUpdate", false, settingsJson);
            SetIfNotSet("extensions.autoCheckUpdates", false, settingsJson);
            SetIfNotSet("extensions.ignoreRecommendations", true, settingsJson);
            SetIfNotSet("update.mode", "none", settingsJson);
            SetIfNotSet("update.showReleaseNotes", false, settingsJson);
            SetIfNotSet("java.completion.matchCase", "off", settingsJson);

            string os;
            string path_seperator;
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                os = "windows";
                path_seperator = ";";
            }
            else if (OperatingSystem.IsMacOS())
            {
                os = "osx";
                path_seperator = ":";
            }
            else
            {
                os = "linux";
                path_seperator = ":";
            }

            if (!settingsJson.ContainsKey("terminal.integrated.env." + os))
            {
                JsonObject terminalProps = new JsonObject
                {
                    ["JAVA_HOME"] = Path.Combine(homePath, "jdk"),
                    ["PATH"] = Path.Combine(homePath, "jdk", "bin") + path_seperator + "${env:PATH}"
                };

                settingsJson["terminal.integrated.env." + os] = terminalProps;

            }
            else
            {
                JsonNode? terminalEnv = settingsJson["terminal.integrated.env." + os]!;
                terminalEnv["JAVA_HOME"] = Path.Combine(homePath, "jdk");
                JsonNode? path = terminalEnv["PATH"];
                if (path == null)
                {
                    terminalEnv["PATH"] = Path.Combine(homePath, "jdk", "bin") + path_seperator + "${env:PATH}";
                }
                else
                {
                    var binPath = Path.Combine(homePath, "jdk", "bin");
                    if (!path.ToString().Contains(binPath))
                    {
                        path = binPath + path_seperator + path;
                        terminalEnv["PATH"] = path.ToString();
                    }
                }
            }

            if (settingsJson.ContainsKey("java.configuration.runtimes"))
            {
                JsonArray? javaConfigEnv = settingsJson["java.configuration.runtimes"]?.AsArray();
                bool javaFound = false;
                if (javaConfigEnv != null)
                {
                    foreach (JsonNode? result in javaConfigEnv)
                    {
                        if (result == null) continue;
                        JsonNode? name = result["name"];
                        if (name != null)
                        {
                            if (name.ToString().Equals("JavaSE-17", StringComparison.OrdinalIgnoreCase))
                            {
                                result["path"] = Path.Combine(homePath, "jdk");
                                result["default"] = true;
                                javaFound = true;
                            }
                            else
                            {
                                result["default"] = false;
                            }
                        }
                    }
                    if (!javaFound)
                    {
                        JsonObject javaConfigProp = new JsonObject
                        {
                            ["name"] = "JavaSE-17",
                            ["path"] = Path.Combine(homePath, "jdk"),
                            ["default"] = true
                        };
                        javaConfigEnv.Add((JsonNode)javaConfigProp);
                        settingsJson["java.configuration.runtimes"] = javaConfigEnv;
                    }
                }
            }
            else
            {
                JsonArray javaConfigProps = new JsonArray();
                JsonObject javaConfigProp = new JsonObject
                {
                    ["name"] = "JavaSE-17",
                    ["path"] = Path.Combine(homePath, "jdk"),
                    ["default"] = true
                };
                javaConfigProps.Add((JsonNode)javaConfigProp);
                settingsJson["java.configuration.runtimes"] = javaConfigProps;
            }

            var serialized = settingsJson.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(settingsFile, serialized);
        }

        private async Task RunVsCodeSetup(CancellationToken token)
        {
            if (!vsInstallProvider.Model.InstallingVsCode) return;

            Text = "Installing Visual Studio Code";
            Progress = 0;

            string intoPath = Path.Join(configurationProvider.InstallDirectory, "vscode");

            if (vsInstallProvider.Model.ToExtractArchiveMacOs != null)
            {
                vsInstallProvider.Model.ToExtractArchiveMacOs.Seek(0, SeekOrigin.Begin);
                var zipPath = Path.Join(intoPath, "MacVsCode.zip");
                Directory.CreateDirectory(intoPath);
                {
                    using var fileToWrite = new FileStream(zipPath, FileMode.Create, FileAccess.Write, FileShare.None);
                    await vsInstallProvider.Model.ToExtractArchiveMacOs.CopyToAsync(fileToWrite, token);
                }
                await RunScriptExecutable("unzip", Timeout.Infinite, zipPath, "-d", intoPath);
                File.Delete(zipPath);
                return;
            }

            var archive = vsInstallProvider.Model.ToExtractArchive!;

            var extractor = archive;

            double totalSize = archive.TotalUncompressSize;
            long currentSize = 0;

            while (await extractor.MoveToNextEntryAsync())
            {
                if (token.IsCancellationRequested)
                {
                    return;
                }
                currentSize += extractor.EntrySize;
                if (extractor.EntryIsDirectory) continue;
                var entryName = extractor.EntryKey;
                Text = "Installing " + entryName;

                double currentPercentage = (currentSize / totalSize) * 100;
                if (currentPercentage > 100) currentPercentage = 100;
                if (currentPercentage < 0) currentPercentage = 0;
                Progress = (int)currentPercentage;

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

                {
                    await extractor.CopyToFileAsync(fullZipToPath, token);
                }

                if (extractor.EntryIsExecutable && !OperatingSystem.IsWindows())
                {
                    var currentMode = File.GetUnixFileMode(fullZipToPath);
                    File.SetUnixFileMode(fullZipToPath, currentMode | UnixFileMode.GroupExecute | UnixFileMode.UserExecute | UnixFileMode.OtherExecute);
                }
            }

        }

        private async Task ExtractArchive(string[]? filter, CancellationToken token)
        {
            Progress = 0;
            if (OperatingSystem.IsWindows())
            {
                Text = "Checking for currently running JDKs";
                bool foundRunningExe = await Task.Run(() =>
                {
                    try
                    {
                        var jdkBinFolder = Path.Join(configurationProvider.InstallDirectory, configurationProvider.JdkConfig.Folder, "bin");
                        var jdkExes = Directory.EnumerateFiles(jdkBinFolder, "*.exe", SearchOption.AllDirectories);
                        bool found = false;
                        foreach (var exe in jdkExes)
                        {
                            try
                            {
                                var name = Path.GetFileNameWithoutExtension(exe)!;
                                var pNames = Process.GetProcessesByName(name);
                                foreach (var p in pNames)
                                {
                                    if (p.MainModule?.FileName == exe)
                                    {
                                        found = true;
                                        break;
                                    }
                                }
                                if (found)
                                {
                                    break;
                                }
                            }
                            catch
                            {
                                // Do nothing. We don't want this code to break.
                            }
                        }
                        return found;
                    }
                    catch
                    {
                        // Do nothing. We don't want this code to break.
                        return false;
                    }
                });
                if (foundRunningExe)
                {
                    string msg = "Running JDK processes have been found. Installation cannot continue. Please restart your computer, and rerun this installer without running anything else (including VS Code)";
                    await programWindow.ShowMessageDialog("JDKs Running", msg);
                    throw new InvalidOperationException(msg);
                }
            }

            var archive = configurationProvider.ZipArchive;

            var extractor = archive;

            double totalSize = extractor.TotalUncompressSize;
            long currentSize = 0;

            string intoPath = configurationProvider.InstallDirectory;

            while (await extractor.MoveToNextEntryAsync())
            {
                if (token.IsCancellationRequested)
                {
                    return;
                }
                currentSize += extractor.EntrySize;
                if (extractor.EntryIsDirectory) continue;

                var entryName = extractor.EntryKey;
                if (filter != null)
                {
                    bool skip = true;
                    foreach (var keep in filter)
                    {
                        if (entryName.StartsWith(keep, StringComparison.Ordinal))
                        {
                            skip = false;
                            break;
                        }
                    }

                    if (skip)
                    {
                        continue;
                    }
                }


                Text = "Installing " + entryName;

                double currentPercentage = (currentSize / totalSize) * 100;
                if (currentPercentage > 100) currentPercentage = 100;
                if (currentPercentage < 0) currentPercentage = 0;
                Progress = (int)currentPercentage;

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

                {
                    await extractor.CopyToFileAsync(fullZipToPath, token);
                }

                if (extractor.EntryIsExecutable && !OperatingSystem.IsWindows())
                {
                    var currentMode = File.GetUnixFileMode(fullZipToPath);
                    File.SetUnixFileMode(fullZipToPath, currentMode | UnixFileMode.GroupExecute | UnixFileMode.UserExecute | UnixFileMode.OtherExecute);
                }
            }
        }

        private Task RunGradleSetup()
        {
            Text = "Configuring Gradle";
            Progress = 50;

            string extractFolder = configurationProvider.InstallDirectory;

            var config = configurationProvider.FullConfig;

            string gradleZipLoc = Path.Combine(extractFolder, "installUtils", config.Gradle.ZipName);

            string userFolder = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            List<Task> tasks = new List<Task>();
            foreach (var extractLocation in config.Gradle.ExtractLocations)
            {
                string toFolder = Path.Combine(userFolder, ".gradle", extractLocation, Path.GetFileNameWithoutExtension(config.Gradle.ZipName), config.Gradle.Hash);
                string toFile = Path.Combine(toFolder, config.Gradle.ZipName);
                tasks.Add(Task.Factory.StartNew(() =>
                {
                    try
                    {
                        Directory.CreateDirectory(toFolder);
                    }
                    catch (IOException)
                    {

                    }
                    File.Copy(gradleZipLoc, toFile, true);
                }));
            }
            return Task.WhenAll(tasks);
        }

        private async Task RunCppSetup()
        {
            Text = "Configuring C++";
            Progress = 50;

            await Task.Yield();
        }

        private static async Task<bool> RunExecutable(string path, int timeoutMs)
        {
            if (OperatingSystem.IsWindows())
            {
                path += ".exe";
            }

            using CancellationTokenSource cts = new CancellationTokenSource(timeoutMs);

            ProcessStartInfo pstart = new ProcessStartInfo(path);
            var p = Process.Start(pstart);
            try
            {
                await p!.WaitForExitAsync(cts.Token);
                return true;
            }
            catch (OperationCanceledException)
            {
                return false;
            }
        }

        private static Task<bool> RunJavaJar(string installDir, string jar, int timeoutMs)
        {
            string java = Path.Join(installDir, "jdk", "bin", "java");
            if (OperatingSystem.IsWindows())
            {
                java += ".exe";
            }
            ProcessStartInfo pstart = new ProcessStartInfo(java, $"-jar \"{jar}\"");
            var p = Process.Start(pstart);
            return Task.Run(() =>
            {
                return p!.WaitForExit(timeoutMs);
            });
        }

        private static Task<bool> RunScriptExecutable(string script, int timeoutMs, params string[] args)
        {
            ProcessStartInfo pstart = new ProcessStartInfo(script, string.Join(" ", args));
            var p = Process.Start(pstart);
            return Task.Run(() =>
            {
                return p!.WaitForExit(timeoutMs);
            });
        }

        private async Task RunToolSetup()
        {
            Text = "Configuring Tools";
            Progress = 50;

            await RunExecutable(
                Path.Combine(configurationProvider.InstallDirectory,
                configurationProvider.UpgradeConfig.Tools.Folder,
                configurationProvider.UpgradeConfig.Tools.UpdaterExe), 30000);
        }

        private async Task RunMavenMetaDataFixer()
        {
            Text = "Fixing up maven metadata";
            Progress = 50;

            await RunJavaJar(configurationProvider.InstallDirectory,
                Path.Combine(configurationProvider.InstallDirectory,
                configurationProvider.UpgradeConfig.Maven.Folder,
                configurationProvider.UpgradeConfig.Maven.MetaDataFixerJar), 20000);
        }

        private async Task RunVsCodeExtensionsSetup()
        {
            if (!vsInstallProvider.Model.InstallExtensions) return;

            string codeExe;

            var currentPlatform = PlatformUtils.CurrentPlatform;
            switch (currentPlatform)
            {
                case Platform.Win64:
                    codeExe = Path.Combine(configurationProvider.InstallDirectory, "vscode", "bin", "code.cmd");
                    break;
                case Platform.MacArm64:
                case Platform.Mac64:
                    var appDirectories = Directory.GetDirectories(Path.Combine(configurationProvider.InstallDirectory, "vscode"), "*.app");
                    if (appDirectories.Length != 1)
                    {
                        throw new InvalidOperationException("Expected exactly one .app directory in the vscode folder.");
                    }
                    codeExe = Path.Combine(appDirectories[0], "Contents", "Resources", "app", "bin", "code");
                    break;
                case Platform.Linux64:
                    codeExe = Path.Combine(configurationProvider.InstallDirectory, "vscode", "VSCode-linux-x64", "bin", "code");
                    break;
                case Platform.LinuxArm64:
                    codeExe = Path.Combine(configurationProvider.InstallDirectory, "vscode", "VSCode-linux-arm64", "bin", "code");
                    break;
                default:
                    throw new PlatformNotSupportedException("Invalid platform");
            }

            // Load existing extensions

            var versions = await Task.Run(() =>
            {
                var startInfo = new ProcessStartInfo(codeExe, "--list-extensions --show-versions")
                {
                    UseShellExecute = false,
                    WindowStyle = ProcessWindowStyle.Hidden,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true
                };
                var proc = Process.Start(startInfo);
                proc!.WaitForExit();
                var lines = new List<(string name, WPIVersion version)>();
                while (true)
                {
                    string? line = proc.StandardOutput.ReadLine();
                    if (line == null)
                    {
                        return lines;
                    }

                    if (line.Contains('@'))
                    {
                        var split = line.Split('@');
                        lines.Add((split[0], new WPIVersion(split[1])));
                    }
                }
            });

            var availableToInstall = new List<(Extension extension, WPIVersion version, int sortOrder)>
            {
                (configurationProvider.VsCodeConfig.WPILibExtension,
                new WPIVersion(configurationProvider.VsCodeConfig.WPILibExtension.Version), int.MaxValue)
            };

            for (int i = 0; i < configurationProvider.VsCodeConfig.ThirdPartyExtensions.Length; i++)
            {
                availableToInstall.Add((configurationProvider.VsCodeConfig.ThirdPartyExtensions[i],
                    new WPIVersion(configurationProvider.VsCodeConfig.ThirdPartyExtensions[i].Version), i));
            }

            var maybeUpdates = availableToInstall.Where(x => versions.Select(y => y.name).Contains(x.extension.Name)).ToList();
            var newInstall = availableToInstall.Except(maybeUpdates).ToList();

            var definitelyUpdate = maybeUpdates.Join(versions, x => x.extension.Name, y => y.name,
                (newVersion, existing) => (newVersion, existing))
                .Where(x => x.newVersion.version > x.existing.version).Select(x => x.newVersion);

            var installs = definitelyUpdate.Concat(newInstall)
                                           .OrderBy(x => x.sortOrder)
                                           .Select(x => x.extension)
                                           .ToArray();

            Text = "Installing Extensions";


            int idx = 0;
            double end = installs.Length;
            Progress = 0;
            foreach (var item in installs)
            {
                var startInfo = new ProcessStartInfo(codeExe, "--install-extension " + Path.Combine(configurationProvider.InstallDirectory, "vsCodeExtensions", item.Vsix))
                {
                    UseShellExecute = false,
                    WindowStyle = ProcessWindowStyle.Hidden,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true
                };
                await Task.Run(() =>
                {
                    var proc = Process.Start(startInfo);
                    proc!.WaitForExit();
                });

                idx++;

                double percentage = (idx / end) * 100;
                if (percentage > 100) percentage = 100;
                if (percentage < 0) percentage = 0;
                Progress = (int)percentage;


            }
        }

        private async Task RunShortcutCreator(CancellationToken token)
        {
            var shortcutData = new ShortcutData();

            var frcHomePath = configurationProvider.InstallDirectory;
            var frcYear = configurationProvider.UpgradeConfig.FrcYear;

            var iconLocation = Path.Join(frcHomePath, "icons");
            var wpilibIconLocation = Path.Join(iconLocation, "wpilib-256.ico");

            shortcutData.IsAdmin = toInstallProvider.Model.InstallAsAdmin;

            if (vsInstallProvider.Model.InstallingVsCode)
            {
                // Add VS Code Shortcuts
                shortcutData.DesktopShortcuts.Add(new ShortcutInfo(Path.Join(frcHomePath, "vscode", "Code.exe"), $"{frcYear} WPILib VS Code", $"{frcYear} WPILib VS Code", wpilibIconLocation));
                shortcutData.StartMenuShortcuts.Add(new ShortcutInfo(Path.Join(frcHomePath, "vscode", "Code.exe"), $"Programs/{frcYear} WPILib VS Code", $"{frcYear} WPILib VS Code", wpilibIconLocation));
            }

            // Add Tool Shortcuts
            shortcutData.DesktopShortcuts.Add(new ShortcutInfo(Path.Join(frcHomePath, "tools", "Glass.exe"), $"{frcYear} WPILib Tools/Glass {frcYear}", $"Glass {frcYear}", ""));
            shortcutData.DesktopShortcuts.Add(new ShortcutInfo(Path.Join(frcHomePath, "tools", "OutlineViewer.exe"), $"{frcYear} WPILib Tools/OutlineViewer {frcYear}", $"OutlineViewer {frcYear}", ""));
            shortcutData.DesktopShortcuts.Add(new ShortcutInfo(Path.Join(frcHomePath, "tools", "SysId.exe"), $"{frcYear} WPILib Tools/SysId {frcYear}", $"SysId {frcYear}", ""));
            shortcutData.DesktopShortcuts.Add(new ShortcutInfo(Path.Join(frcHomePath, "tools", "DataLogTool.exe"), $"{frcYear} WPILib Tools/Data Log Tool {frcYear}", $"Data Log Tool {frcYear}", ""));
            shortcutData.DesktopShortcuts.Add(new ShortcutInfo(Path.Join(frcHomePath, "tools", "WPIcal.exe"), $"{frcYear} WPILib Tools/WPIcal {frcYear}", $"WPIcal {frcYear}", ""));
            shortcutData.DesktopShortcuts.Add(new ShortcutInfo(Path.Join(frcHomePath, "advantagescope", "AdvantageScope (WPILib).exe"), $"{frcYear} WPILib Tools/AdvantageScope (WPILib) {frcYear}", $"AdvantageScope (WPILib) {frcYear}", ""));
            shortcutData.DesktopShortcuts.Add(new ShortcutInfo(Path.Join(frcHomePath, "elastic", "elastic_dashboard.exe"), $"{frcYear} WPILib Tools/Elastic (WPILib) {frcYear}", $"Elastic (WPILib) {frcYear}", ""));

            shortcutData.StartMenuShortcuts.Add(new ShortcutInfo(Path.Join(frcHomePath, "tools", "Glass.exe"), $"Programs/{frcYear} WPILib Tools/Glass {frcYear}", $"Glass {frcYear}", ""));
            shortcutData.StartMenuShortcuts.Add(new ShortcutInfo(Path.Join(frcHomePath, "tools", "OutlineViewer.exe"), $"Programs/{frcYear} WPILib Tools/OutlineViewer {frcYear}", $"OutlineViewer {frcYear}", ""));
            shortcutData.StartMenuShortcuts.Add(new ShortcutInfo(Path.Join(frcHomePath, "tools", "SysId.exe"), $"Programs/{frcYear} WPILib Tools/SysId {frcYear}", $"SysId {frcYear}", ""));
            shortcutData.StartMenuShortcuts.Add(new ShortcutInfo(Path.Join(frcHomePath, "tools", "DataLogTool.exe"), $"Programs/{frcYear} WPILib Tools/Data Log Tool {frcYear}", $"Data Log Tool {frcYear}", ""));
            shortcutData.StartMenuShortcuts.Add(new ShortcutInfo(Path.Join(frcHomePath, "tools", "WPIcal.exe"), $"Programs/{frcYear} WPILib Tools/WPIcal {frcYear}", $"WPIcal {frcYear}", ""));
            shortcutData.StartMenuShortcuts.Add(new ShortcutInfo(Path.Join(frcHomePath, "advantagescope", "AdvantageScope (WPILib).exe"), $"Programs/{frcYear} WPILib Tools/AdvantageScope (WPILib) {frcYear}", $"AdvantageScope (WPILib) {frcYear}", ""));
            shortcutData.StartMenuShortcuts.Add(new ShortcutInfo(Path.Join(frcHomePath, "elastic", "elastic_dashboard.exe"), $"Programs/{frcYear} WPILib Tools/Elastic (WPILib) {frcYear}", $"Elastic (WPILib) {frcYear}", ""));

            if (toInstallProvider.Model.InstallEverything)
            {
                // Add Documentation Shortcuts
                shortcutData.DesktopShortcuts.Add(new ShortcutInfo(Path.Join(frcHomePath, "documentation", "rtd", "frc-docs-latest", "index.html"), $"{frcYear} WPILib Documentation", $"{frcYear} WPILib Documentation", wpilibIconLocation));
                shortcutData.StartMenuShortcuts.Add(new ShortcutInfo(Path.Join(frcHomePath, "documentation", "rtd", "frc-docs-latest", "index.html"), $"Programs/{frcYear} WPILib Documentation", $"{frcYear} WPILib Documentation", wpilibIconLocation));
            }

            var serializedData = JsonSerializer.Serialize(shortcutData, SourceGenerationContext.Default.ShortcutData);

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                // Run windows shortcut creater
                var tempFile = Path.GetTempFileName();
                await File.WriteAllTextAsync(tempFile, serializedData, token);
                var shortcutCreatorPath = Path.Combine(configurationProvider.InstallDirectory, "installUtils", "WPILibShortcutCreator.exe");

                do
                {
                    var startInfo = new ProcessStartInfo(shortcutCreatorPath, $"\"{tempFile}\"")
                    {
                        WorkingDirectory = Environment.CurrentDirectory
                    };
                    if (shortcutData.IsAdmin)
                    {
                        startInfo.UseShellExecute = true;
                        startInfo.Verb = "runas";
                    }
                    else
                    {
                        startInfo.UseShellExecute = false;
                        startInfo.WindowStyle = ProcessWindowStyle.Hidden;
                        startInfo.CreateNoWindow = true;
                        startInfo.RedirectStandardOutput = true;
                    }
                    var exitCode = await Task.Run(() =>
                    {
                        try
                        {
                            var proc = Process.Start(startInfo);
                            proc!.WaitForExit();
                            return proc.ExitCode;
                        }
                        catch (Win32Exception ex)
                        {
                            return ex.NativeErrorCode;
                        }
                    });

                    if (exitCode == 1223) // ERROR_CANCELLED
                    {
                        var results = await programWindow.ShowMessageDialog("UAC Prompt Cancelled",
                            "UAC Prompt Cancelled or Timed Out. Would you like to retry?",
                            MessageDialogButtons.YesNo);
                        if (results == MessageDialogResult.Yes)
                        {
                            continue;
                        }
                        break;
                    }

                    if (exitCode != 0)
                    {
                        await programWindow.ShowMessageDialog("Shortcut Creation Failed",
                            $"Shortcut creation failed with error code {exitCode}");
                        break;
                    }
                    break;
                } while (true);
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                if (vsInstallProvider.Model.InstallingVsCode)
                {
                    // Create Linux desktop shortcut
                    var desktopFile = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Desktop", $@"FRC VS Code {frcYear}.desktop");
                    var launcherFile = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".local/share/applications", $@"FRC_VS_Code_{frcYear}.desktop");
                    string contents = $@"#!/usr/bin/env xdg-open
[Desktop Entry]
Version=1.0
Type=Application
Categories=Development
Name=FRC VS Code {frcYear}
Comment=Official C++/Java IDE for the FIRST Robotics Competition
Exec={configurationProvider.InstallDirectory}/frccode/frccode{frcYear}
Icon={configurationProvider.InstallDirectory}/icons/wpilib-icon-256.png
Terminal=false
StartupNotify=true
StartupWMClass=Code
";

                    var desktopPath = Path.GetDirectoryName(desktopFile);
                    if (desktopPath != null)
                    {
                        Directory.CreateDirectory(desktopPath);
                    }
                    var launcherPath = Path.GetDirectoryName(launcherFile);
                    if (launcherPath != null)
                    {
                        Directory.CreateDirectory(launcherPath);
                    }
                    await File.WriteAllTextAsync(desktopFile, contents, token);
                    await File.WriteAllTextAsync(launcherFile, contents, token);
                    await Task.Run(() =>
                    {
                        var startInfo = new ProcessStartInfo("chmod", $"+x \"{desktopFile}\"")
                        {
                            UseShellExecute = false,
                            WindowStyle = ProcessWindowStyle.Hidden,
                            CreateNoWindow = true
                        };
                        var proc = Process.Start(startInfo);
                        proc!.WaitForExit();
                    }, token);
                }

                CreateLinuxShortcut("AdvantageScope (WPILib)", frcYear, "AdvantageScope (WPILib)", "advantagescope.png", token);
                CreateLinuxShortcut("Elastic (WPILib)", frcYear, "elastic_dashboard", "elastic.png", token);
                CreateLinuxShortcut("Glass", frcYear, "Glass - DISCONNECTED", "glass.png", token);
                CreateLinuxShortcut("OutlineViewer", frcYear, "OutlineViewer - DISCONNECTED", "outlineviewer.png", token);
                CreateLinuxShortcut("DataLogTool", frcYear, "Datalog Tool", "datalogtool.png", token);
                CreateLinuxShortcut("SysId", frcYear, "System Identification", "sysid.png", token);
                CreateLinuxShortcut("WPIcal", frcYear, "WPIcal", "wpical.png", token);
            }
        }
    }
}
