using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using DynamicData;
using MsBox.Avalonia;
using MsBox.Avalonia.Dto;
using MsBox.Avalonia.Models;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using ReactiveUI;
using WPILibInstaller.Interfaces;
using WPILibInstaller.Models;
using WPILibInstaller.Utils;
using static System.Net.WebRequestMethods;
using File = System.IO.File;

namespace WPILibInstaller.ViewModels
{
    public class InstallPageViewModel : PageViewModelBase
    {
        private readonly IViewModelResolver viewModelResolver;
        private readonly IToInstallProvider toInstallProvider;
        private readonly IConfigurationProvider configurationProvider;
        private readonly IVsCodeInstallLocationProvider vsInstallProvider;
        private readonly IProgramWindow programWindow;

        public int Progress { get; set; }
        public string Text { get; set; } = "";
        public int ProgressTotal { get; set; }
        public string TextTotal { get; set; } = "";

        private async void CreateLinuxShortcut(String name, String executableName, String frcYear, String wmClass, String iconName, CancellationToken token)
        {
            var launcherFile = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".local/share/applications", $@"{name.Replace(' ', '_').Replace(")", "").Replace("(", "")}_{frcYear}.desktop");
            string contents = $@"#!/usr/bin/env xdg-open
[Desktop Entry]
Version=1.0
Type=Application
Categories=Robotics;Science
Name={name} {frcYear}
Comment={name} tool for the {frcYear} FIRST Robotics Competition season
Exec={configurationProvider.InstallDirectory}/tools/{executableName}
Icon={configurationProvider.InstallDirectory}/icons/{iconName}
Terminal=false
StartupNotify=true
StartupWMClass={wmClass}
".ReplaceLineEndings("\n");
            var launcherPath = Path.GetDirectoryName(launcherFile);
            if (launcherPath != null)
            {
                Directory.CreateDirectory(launcherPath);
            }
            await File.WriteAllTextAsync(launcherFile, contents, token);
        }

        private void CreateLinuxShortcut(String name, String frcYear, String wmClass, String iconName, CancellationToken token)
        {
            CreateLinuxShortcut(name, name, frcYear, wmClass, iconName, token);
        }

        public async Task UIUpdateTask(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                this.RaisePropertyChanged(nameof(Progress));
                this.RaisePropertyChanged(nameof(Text));
                this.RaisePropertyChanged(nameof(ProgressTotal));
                this.RaisePropertyChanged(nameof(TextTotal));
                try
                {
                    await Task.Delay(100, token);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }

        public bool succeeded = false;

        private readonly Task runInstallTask;

        public InstallPageViewModel(IViewModelResolver viewModelResolver, IToInstallProvider toInstallProvider, IConfigurationProvider configurationProvider, IVsCodeInstallLocationProvider vsInstallProvider,
            IProgramWindow programWindow, ICatchableButtonFactory buttonFactory)
            : base("", "")
        {
            this.viewModelResolver = viewModelResolver;
            this.toInstallProvider = toInstallProvider;
            this.configurationProvider = configurationProvider;
            this.vsInstallProvider = vsInstallProvider;
            this.programWindow = programWindow;
            CancelInstall = buttonFactory.CreateCatchableButton(CancelInstallFunc);
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

        public ReactiveCommand<Unit, Unit> CancelInstall { get; }

        public async Task CancelInstallFunc()
        {
            source?.Cancel();
            await runInstallTask;
        }

        private async Task ExtractJDKAndTools(CancellationToken token)
        {
            await ExtractArchive(token, new[] {
                configurationProvider.JdkConfig.Folder + "/",
                configurationProvider.UpgradeConfig.Tools.Folder + "/",
                configurationProvider.AdvantageScopeConfig.Folder + "/",
                configurationProvider.ElasticConfig.Folder + "/",
                "installUtils/", "icons"});
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
                    await ExtractArchive(token, null);
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

            var updateSource = new CancellationTokenSource();

            var updateTask = UIUpdateTask(updateSource.Token);

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

                updateSource.Cancel();
                await updateTask;
            }
            catch (OperationCanceledException)
            {
                // Ignore, as we just want to continue
            }

            if (source.IsCancellationRequested)
            {
                succeeded = false;
            }
            else
            {
                succeeded = true;
            }

            await viewModelResolver.ResolveMainWindow().ExecuteGoNext();
        }

        public override PageViewModelBase MoveNext()
        {
            if (succeeded)
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

        private static void SetIfNotSet(string key, object value, JObject settingsJson)
        {
            if (!settingsJson.ContainsKey(key))
            {
                settingsJson[key] = JToken.FromObject(value);
            }
        }

        private static void SetIfNotSetIgnoreSync(string key, object value, JObject settingsJson)
        {
            SetIfNotSet(key, value, settingsJson);
            IgnoreSync(key, settingsJson);
        }

        private static void IgnoreSync(string key, JObject settingsJson)
        {
            if (settingsJson.ContainsKey("settingsSync.ignoredSettings"))
            {
                JArray ignoredSettings = (JArray)settingsJson["settingsSync.ignoredSettings"]!;
                Boolean keyFound = false;
                foreach (JToken result in ignoredSettings)
                {
                    if (result.Value<string>() != null)
                    {
                        if (result.Value<string>() == key)
                        {
                            keyFound = true;
                        }
                    }
                }
                if (!keyFound)
                {
                    ignoredSettings.Add(key);
                    settingsJson["settingsSync.ignoredSettings"] = ignoredSettings;
                }
            }
            else
            {
                JArray ignoredSettings = [key];
                settingsJson["settingsSync.ignoredSettings"] = ignoredSettings;
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

            JObject settingsJson = new JObject();
            if (File.Exists(settingsFile))
            {
                settingsJson = (JObject)JsonConvert.DeserializeObject(await File.ReadAllTextAsync(settingsFile))!;
            }

            SetIfNotSet("java.jdt.ls.java.home", Path.Combine(homePath, "jdk"), settingsJson);
            SetIfNotSetIgnoreSync("extensions.autoUpdate", false, settingsJson);
            SetIfNotSetIgnoreSync("extensions.autoCheckUpdates", false, settingsJson);
            SetIfNotSetIgnoreSync("extensions.ignoreRecommendations", true, settingsJson);
            SetIfNotSetIgnoreSync("extensions.showRecommendationsOnlyOnDemand", true, settingsJson);
            SetIfNotSetIgnoreSync("update.mode", "none", settingsJson);
            SetIfNotSetIgnoreSync("update.showReleaseNotes", false, settingsJson);
            SetIfNotSetIgnoreSync("java.completion.matchCase", "off", settingsJson);
            SetIfNotSetIgnoreSync("workbench.secondarySideBar.defaultVisibility", "hidden", settingsJson);

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
                JObject terminalProps = new JObject
                {
                    ["JAVA_HOME"] = Path.Combine(homePath, "jdk"),
                    ["PATH"] = Path.Combine(homePath, "jdk", "bin") + path_seperator + "${env:PATH}"
                };

                settingsJson["terminal.integrated.env." + os] = terminalProps;

            }
            else
            {
                JToken terminalEnv = settingsJson["terminal.integrated.env." + os]!;
                terminalEnv["JAVA_HOME"] = Path.Combine(homePath, "jdk");
                JToken? path = terminalEnv["PATH"];
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
                        terminalEnv["PATH"] = path;
                    }
                }
            }
            IgnoreSync("terminal.integrated.env." + os, settingsJson);

            if (settingsJson.ContainsKey("java.configuration.runtimes"))
            {
                JArray javaConfigEnv = (JArray)settingsJson["java.configuration.runtimes"]!;
                Boolean javaFound = false;
                foreach (JToken result in javaConfigEnv)
                {
                    JToken? name = result["name"];
                    if (name != null)
                    {
                        if (name.ToString().Equals("JavaSE-17"))
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
                    JObject javaConfigProp = new JObject
                    {
                        ["name"] = "JavaSE-17",
                        ["path"] = Path.Combine(homePath, "jdk"),
                        ["default"] = true
                    };
                    javaConfigEnv.Add(javaConfigProp);
                    settingsJson["java.configuration.runtimes"] = javaConfigEnv;
                }
            }
            else
            {
                JArray javaConfigProps = new JArray();
                JObject javaConfigProp = new JObject
                {
                    ["name"] = "JavaSE-17",
                    ["path"] = Path.Combine(homePath, "jdk"),
                    ["default"] = true
                };
                javaConfigProps.Add(javaConfigProp);
                settingsJson["java.configuration.runtimes"] = javaConfigProps;
            }

            if (settingsJson.ContainsKey("settingsSync.ignoredExtensions"))
            {
                JArray ignoredExtensions = (JArray)settingsJson["settingsSync.ignoredExtensions"]!;
                Boolean keyFound = false;
                foreach (JToken result in ignoredExtensions)
                {
                    if (result.Value<string>() != null)
                    {
                        if (result.Value<string>() == "wpilibsuite.vscode-wpilib")
                        {
                            keyFound = true;
                        }
                    }
                }
                if (!keyFound)
                {
                    ignoredExtensions.Add("wpilibsuite.vscode-wpilib");
                    settingsJson["settingsSync.ignoredExtensions"] = ignoredExtensions;
                }
            }
            else
            {
                JArray ignoredExtensions = ["wpilibsuite.vscode-wpilib"];
                settingsJson["settingsSync.ignoredExtensions"] = ignoredExtensions;
            }

            var serialized = JsonConvert.SerializeObject(settingsJson, Formatting.Indented);
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

            while (extractor.MoveToNextEntry())
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
                    using FileStream writer = File.Create(fullZipToPath);
                    await extractor.CopyToStreamAsync(writer);
                }

                if (extractor.EntryIsExecutable && !OperatingSystem.IsWindows())
                {
                    var currentMode = File.GetUnixFileMode(fullZipToPath);
                    File.SetUnixFileMode(fullZipToPath, currentMode | UnixFileMode.GroupExecute | UnixFileMode.UserExecute | UnixFileMode.OtherExecute);
                }
            }

        }

        private async Task ExtractArchive(CancellationToken token, string[]? filter)
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
                    await MsBox.Avalonia.MessageBoxManager.GetMessageBoxStandard(new MsBox.Avalonia.Dto.MessageBoxStandardParams
                    {
                        ContentTitle = "JDKs Running",
                        ContentMessage = msg,
                        Icon = MsBox.Avalonia.Enums.Icon.Error,
                        ButtonDefinitions = MsBox.Avalonia.Enums.ButtonEnum.Ok
                    }).ShowWindowDialogAsync(programWindow.Window);
                    throw new InvalidOperationException(msg);
                }
            }

            var archive = configurationProvider.ZipArchive;

            var extractor = archive;

            double totalSize = extractor.TotalUncompressSize;
            long currentSize = 0;

            string intoPath = configurationProvider.InstallDirectory;

            while (extractor.MoveToNextEntry())
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
                        if (entryName.StartsWith(keep))
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
                    using FileStream writer = File.Create(fullZipToPath);
                    await extractor.CopyToStreamAsync(writer);
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

            await RunJavaJar(configurationProvider.InstallDirectory,
                Path.Combine(configurationProvider.InstallDirectory,
                configurationProvider.UpgradeConfig.Tools.Folder,
                configurationProvider.UpgradeConfig.Tools.UpdaterJar), 30000);
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
                Text = "Installing Extension " + item.Name;
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
            shortcutData.DesktopShortcuts.Add(new ShortcutInfo(Path.Join(frcHomePath, "tools", "glass.exe"), $"{frcYear} WPILib Tools/Glass {frcYear}", $"Glass {frcYear}", ""));
            shortcutData.DesktopShortcuts.Add(new ShortcutInfo(Path.Join(frcHomePath, "tools", "outlineviewer.exe"), $"{frcYear} WPILib Tools/OutlineViewer {frcYear}", $"OutlineViewer {frcYear}", ""));
            shortcutData.DesktopShortcuts.Add(new ShortcutInfo(Path.Join(frcHomePath, "tools", "PathWeaver.exe"), $"{frcYear} WPILib Tools/PathWeaver {frcYear}", $"PathWeaver {frcYear}", wpilibIconLocation));
            shortcutData.DesktopShortcuts.Add(new ShortcutInfo(Path.Join(frcHomePath, "tools", "RobotBuilder.exe"), $"{frcYear} WPILib Tools/RobotBuilder {frcYear}", $"RobotBuilder {frcYear}", Path.Join(iconLocation, "robotbuilder.ico")));
            shortcutData.DesktopShortcuts.Add(new ShortcutInfo(Path.Join(frcHomePath, "tools", "ShuffleBoard.exe"), $"{frcYear} WPILib Tools/Shuffleboard {frcYear}", $"Shuffleboard {frcYear}", wpilibIconLocation));
            shortcutData.DesktopShortcuts.Add(new ShortcutInfo(Path.Join(frcHomePath, "tools", "SmartDashboard.exe"), $"{frcYear} WPILib Tools/SmartDashboard {frcYear}", $"SmartDashboard {frcYear}", wpilibIconLocation));
            shortcutData.DesktopShortcuts.Add(new ShortcutInfo(Path.Join(frcHomePath, "tools", "sysid.exe"), $"{frcYear} WPILib Tools/SysId {frcYear}", $"SysId {frcYear}", ""));
            shortcutData.DesktopShortcuts.Add(new ShortcutInfo(Path.Join(frcHomePath, "tools", "roborioteamnumbersetter.exe"), $"{frcYear} WPILib Tools/roboRIO Team Number Setter {frcYear}", $"roboRIO Team Number Setter {frcYear}", ""));
            shortcutData.DesktopShortcuts.Add(new ShortcutInfo(Path.Join(frcHomePath, "tools", "datalogtool.exe"), $"{frcYear} WPILib Tools/Data Log Tool {frcYear}", $"Data Log Tool {frcYear}", ""));
            shortcutData.DesktopShortcuts.Add(new ShortcutInfo(Path.Join(frcHomePath, "tools", "wpical.exe"), $"{frcYear} WPILib Tools/WPIcal {frcYear}", $"WPIcal {frcYear}", ""));
            shortcutData.DesktopShortcuts.Add(new ShortcutInfo(Path.Join(frcHomePath, "advantagescope", "AdvantageScope (WPILib).exe"), $"{frcYear} WPILib Tools/AdvantageScope (WPILib) {frcYear}", $"AdvantageScope (WPILib) {frcYear}", ""));
            shortcutData.DesktopShortcuts.Add(new ShortcutInfo(Path.Join(frcHomePath, "elastic", "elastic_dashboard.exe"), $"{frcYear} WPILib Tools/Elastic (WPILib) {frcYear}", $"Elastic (WPILib) {frcYear}", ""));

            shortcutData.StartMenuShortcuts.Add(new ShortcutInfo(Path.Join(frcHomePath, "tools", "glass.exe"), $"Programs/{frcYear} WPILib Tools/Glass {frcYear}", $"Glass {frcYear}", ""));
            shortcutData.StartMenuShortcuts.Add(new ShortcutInfo(Path.Join(frcHomePath, "tools", "outlineviewer.exe"), $"Programs/{frcYear} WPILib Tools/OutlineViewer {frcYear}", $"OutlineViewer {frcYear}", ""));
            shortcutData.StartMenuShortcuts.Add(new ShortcutInfo(Path.Join(frcHomePath, "tools", "PathWeaver.exe"), $"Programs/{frcYear} WPILib Tools/PathWeaver {frcYear}", $"PathWeaver {frcYear}", wpilibIconLocation));
            shortcutData.StartMenuShortcuts.Add(new ShortcutInfo(Path.Join(frcHomePath, "tools", "RobotBuilder.exe"), $"Programs/{frcYear} WPILib Tools/RobotBuilder {frcYear}", $"RobotBuilder {frcYear}", Path.Join(iconLocation, "robotbuilder.ico")));
            shortcutData.StartMenuShortcuts.Add(new ShortcutInfo(Path.Join(frcHomePath, "tools", "ShuffleBoard.exe"), $"Programs/{frcYear} WPILib Tools/Shuffleboard {frcYear}", $"Shuffleboard {frcYear}", wpilibIconLocation));
            shortcutData.StartMenuShortcuts.Add(new ShortcutInfo(Path.Join(frcHomePath, "tools", "SmartDashboard.exe"), $"Programs/{frcYear} WPILib Tools/SmartDashboard {frcYear}", $"SmartDashboard {frcYear}", wpilibIconLocation));
            shortcutData.StartMenuShortcuts.Add(new ShortcutInfo(Path.Join(frcHomePath, "tools", "sysid.exe"), $"Programs/{frcYear} WPILib Tools/SysId {frcYear}", $"SysId {frcYear}", ""));
            shortcutData.StartMenuShortcuts.Add(new ShortcutInfo(Path.Join(frcHomePath, "tools", "roborioteamnumbersetter.exe"), $"Programs/{frcYear} WPILib Tools/roboRIO Team Number Setter {frcYear}", $"roboRIO Team Number Setter {frcYear}", ""));
            shortcutData.StartMenuShortcuts.Add(new ShortcutInfo(Path.Join(frcHomePath, "tools", "datalogtool.exe"), $"Programs/{frcYear} WPILib Tools/Data Log Tool {frcYear}", $"Data Log Tool {frcYear}", ""));
            shortcutData.StartMenuShortcuts.Add(new ShortcutInfo(Path.Join(frcHomePath, "tools", "wpical.exe"), $"Programs/{frcYear} WPILib Tools/WPIcal {frcYear}", $"WPIcal {frcYear}", ""));
            shortcutData.StartMenuShortcuts.Add(new ShortcutInfo(Path.Join(frcHomePath, "advantagescope", "AdvantageScope (WPILib).exe"), $"Programs/{frcYear} WPILib Tools/AdvantageScope (WPILib) {frcYear}", $"AdvantageScope (WPILib) {frcYear}", ""));
            shortcutData.StartMenuShortcuts.Add(new ShortcutInfo(Path.Join(frcHomePath, "elastic", "elastic_dashboard.exe"), $"Programs/{frcYear} WPILib Tools/Elastic (WPILib) {frcYear}", $"Elastic (WPILib) {frcYear}", ""));

            if (toInstallProvider.Model.InstallEverything)
            {
                // Add Documentation Shortcuts
                shortcutData.DesktopShortcuts.Add(new ShortcutInfo(Path.Join(frcHomePath, "documentation", "frc-docs", "index.html"), $"{frcYear} WPILib Documentation", $"{frcYear} WPILib Documentation", wpilibIconLocation));
                shortcutData.StartMenuShortcuts.Add(new ShortcutInfo(Path.Join(frcHomePath, "documentation", "frc-docs", "index.html"), $"Programs/{frcYear} WPILib Documentation", $"{frcYear} WPILib Documentation", wpilibIconLocation));
            }

            var serializedData = JsonConvert.SerializeObject(shortcutData);

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
                        var results = await MsBox.Avalonia.MessageBoxManager.GetMessageBoxStandard(new MsBox.Avalonia.Dto.MessageBoxStandardParams
                        {
                            ContentTitle = "UAC Prompt Cancelled",
                            ContentMessage = "UAC Prompt Cancelled or Timed Out. Would you like to retry?",
                            Icon = MsBox.Avalonia.Enums.Icon.Info,
                            ButtonDefinitions = MsBox.Avalonia.Enums.ButtonEnum.YesNo
                        }).ShowWindowDialogAsync(programWindow.Window);
                        if (results == MsBox.Avalonia.Enums.ButtonResult.Yes)
                        {
                            continue;
                        }
                        break;
                    }

                    if (exitCode != 0)
                    {
                        await MsBox.Avalonia.MessageBoxManager.GetMessageBoxStandard(new MsBox.Avalonia.Dto.MessageBoxStandardParams
                        {
                            ContentTitle = "Shortcut Creation Failed",
                            ContentMessage = $"Shortcut creation failed with error code {exitCode}",
                            Icon = MsBox.Avalonia.Enums.Icon.Warning,
                            ButtonDefinitions = MsBox.Avalonia.Enums.ButtonEnum.Ok
                        }).ShowWindowDialogAsync(programWindow.Window);
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
".ReplaceLineEndings("\n");

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

                CreateLinuxShortcut("AdvantageScope (WPILib)", "AdvantageScope", frcYear, "AdvantageScope (WPILib)", "advantagescope.png", token);
                CreateLinuxShortcut("Elastic (WPILib)", "Elastic", frcYear, "elastic_dashboard", "elastic.png", token);
                CreateLinuxShortcut("Glass", "glass", frcYear, "Glass - DISCONNECTED", "glass.png", token);
                CreateLinuxShortcut("OutlineViewer", "outlineviewer", frcYear, "OutlineViewer - DISCONNECTED", "outlineviewer.png", token);
                CreateLinuxShortcut("DataLogTool", "datalogtool", frcYear, "Datalog Tool", "datalogtool.png", token);
                CreateLinuxShortcut("SysId", "sysid", frcYear, "System Identification", "sysid.png", token);
                CreateLinuxShortcut("SmartDashboard", frcYear, "edu-wpi-first-smartdashboard-SmartDashboard", "wpilib-icon-256.png", token);
                CreateLinuxShortcut("RobotBuilder", frcYear, "robotbuilder-RobotBuilder", "robotbuilder.png", token);
                CreateLinuxShortcut("PathWeaver", frcYear, "edu.wpi.first.pathweaver.PathWeaver", "wpilib-icon-256.png", token);
                CreateLinuxShortcut("roboRIOTeamNumberSetter", "roborioteamnumbersetter", frcYear, "roboRIO Team Number Setter", "roborioteamnumbersetter.png", token);
                CreateLinuxShortcut("Shuffleboard", frcYear, "edu.wpi.first.shuffleboard.app.Shuffleboard", "wpilib-icon-256.png", token);
                CreateLinuxShortcut("WPIcal", "wpical", frcYear, "WPIcal", "wpical.png", token);
            }
        }
    }
}
