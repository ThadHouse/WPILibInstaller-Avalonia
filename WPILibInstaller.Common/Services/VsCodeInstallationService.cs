using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.Json.Nodes;
using WPILibInstaller.Interfaces;
using WPILibInstaller.Models;
using WPILibInstaller.Utils;

namespace WPILibInstaller.Services
{
    public sealed class VsCodeInstallationService : IVsCodeInstallationService
    {
        private readonly IConfigurationProvider configurationProvider;
        private readonly IVsCodeInstallLocationProvider vsInstallProvider;

        public VsCodeInstallationService(
            IConfigurationProvider configurationProvider,
            IVsCodeInstallLocationProvider vsInstallProvider)
        {
            this.configurationProvider = configurationProvider;
            this.vsInstallProvider = vsInstallProvider;
        }

        public async Task RunVsCodeSetup(CancellationToken token, IProgress<InstallProgress>? progress = null)
        {
            if (!vsInstallProvider.Model.InstallingVsCode)
            {
                return;
            }

            progress?.Report(new InstallProgress(0, "Installing Visual Studio Code"));

            string intoPath = Path.Join(configurationProvider.InstallDirectory, "vscode");

            if (vsInstallProvider.Model.ToExtractArchiveMacOs != null)
            {
                vsInstallProvider.Model.ToExtractArchiveMacOs.Seek(0, SeekOrigin.Begin);
                var zipPath = Path.Join(intoPath, "MacVsCode.zip");
                Directory.CreateDirectory(intoPath);
                await using (var fileToWrite = new FileStream(zipPath, FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    await vsInstallProvider.Model.ToExtractArchiveMacOs.CopyToAsync(fileToWrite, token);
                }

                await ProcessExecutionUtils.RunScriptExecutable("unzip", Timeout.Infinite, token, zipPath, "-d", intoPath);
                File.Delete(zipPath);
                return;
            }

            var extractor = vsInstallProvider.Model.ToExtractArchive!;
            double totalSize = extractor.TotalUncompressSize;
            long currentSize = 0;

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

        public async Task ConfigureVsCodeSettings()
        {
            if (!vsInstallProvider.Model.InstallExtensions)
            {
                return;
            }

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

            JsonObject settingsJson = new();
            if (File.Exists(settingsFile))
            {
                settingsJson = JsonNode.Parse(await File.ReadAllTextAsync(settingsFile))?.AsObject() ?? new JsonObject();
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
            string pathSeparator;
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                os = "windows";
                pathSeparator = ";";
            }
            else if (OperatingSystem.IsMacOS())
            {
                os = "osx";
                pathSeparator = ":";
            }
            else
            {
                os = "linux";
                pathSeparator = ":";
            }

            if (!settingsJson.ContainsKey("terminal.integrated.env." + os))
            {
                JsonObject terminalProps = new()
                {
                    ["JAVA_HOME"] = Path.Combine(homePath, "jdk"),
                    ["PATH"] = Path.Combine(homePath, "jdk", "bin") + pathSeparator + "${env:PATH}",
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
                    terminalEnv["PATH"] = Path.Combine(homePath, "jdk", "bin") + pathSeparator + "${env:PATH}";
                }
                else
                {
                    var binPath = Path.Combine(homePath, "jdk", "bin");
                    if (!path.ToString().Contains(binPath, StringComparison.Ordinal))
                    {
                        path = binPath + pathSeparator + path;
                        terminalEnv["PATH"] = path.ToString();
                    }
                }
            }

            IgnoreSync("terminal.integrated.env." + os, settingsJson);
            ConfigureJavaRuntime(homePath, settingsJson);
            IgnoreExtensionSync(settingsJson);

            SetIfNotSetIgnoreSync("update.enableWindowsBackgroundUpdates", false, settingsJson);
            SetIfNotSetIgnoreSync("workbench.welcomePage.experimentalOnboarding", false, settingsJson);

            var serialized = settingsJson.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(settingsFile, serialized);
        }

        public async Task RunVsCodeExtensionsSetup(IProgress<InstallProgress>? progress = null)
        {
            if (!vsInstallProvider.Model.InstallExtensions)
            {
                return;
            }

            string codeExe = GetVsCodeExecutable();

            var versions = await Task.Run(() =>
            {
                var startInfo = new ProcessStartInfo(codeExe, "--list-extensions --show-versions")
                {
                    UseShellExecute = false,
                    WindowStyle = ProcessWindowStyle.Hidden,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                };

                using var process = Process.Start(startInfo);
                process!.WaitForExit();
                var lines = new List<(string name, WPIVersion version)>();
                while (true)
                {
                    string? line = process.StandardOutput.ReadLine();
                    if (line == null)
                    {
                        return lines;
                    }

                    if (line.Contains('@', StringComparison.Ordinal))
                    {
                        var split = line.Split('@');
                        lines.Add((split[0], new WPIVersion(split[1])));
                    }
                }
            });

            var availableToInstall = new List<(Extension extension, WPIVersion version, int sortOrder)>
            {
                (configurationProvider.VsCodeConfig.WPILibExtension,
                    new WPIVersion(configurationProvider.VsCodeConfig.WPILibExtension.Version), int.MaxValue),
            };

            for (int i = 0; i < configurationProvider.VsCodeConfig.ThirdPartyExtensions.Length; i++)
            {
                availableToInstall.Add((
                    configurationProvider.VsCodeConfig.ThirdPartyExtensions[i],
                    new WPIVersion(configurationProvider.VsCodeConfig.ThirdPartyExtensions[i].Version),
                    i));
            }

            var maybeUpdates = availableToInstall.Where(x => versions.Select(y => y.name).Contains(x.extension.Name, StringComparer.Ordinal)).ToList();
            var newInstall = availableToInstall.Except(maybeUpdates).ToList();

            var definitelyUpdate = maybeUpdates.Join(
                    versions,
                    x => x.extension.Name,
                    y => y.name,
                    (newVersion, existing) => (newVersion, existing))
                .Where(x => x.newVersion.version > x.existing.version)
                .Select(x => x.newVersion);

            var installs = definitelyUpdate.Concat(newInstall)
                .OrderBy(x => x.sortOrder)
                .Select(x => x.extension)
                .ToArray();

            progress?.Report(new InstallProgress(0, "Installing Extensions"));

            int index = 0;
            double end = installs.Length;
            foreach (var item in installs)
            {
                progress?.Report(new InstallProgress((int)Math.Clamp((index / end) * 100, 0, 100), "Installing Extension " + item.Name));

                var startInfo = new ProcessStartInfo(codeExe, "--install-extension " + Path.Combine(configurationProvider.InstallDirectory, "vsCodeExtensions", item.Vsix))
                {
                    UseShellExecute = false,
                    WindowStyle = ProcessWindowStyle.Hidden,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                };

                await Task.Run(() =>
                {
                    using var process = Process.Start(startInfo);
                    process!.WaitForExit();
                });

                index++;
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

        private string GetVsCodeExecutable()
        {
            var currentPlatform = PlatformUtils.CurrentPlatform;
            switch (currentPlatform)
            {
                case Platform.Win64:
                    return Path.Combine(configurationProvider.InstallDirectory, "vscode", "bin", "code.cmd");
                case Platform.MacArm64:
                case Platform.Mac64:
                    var appDirectories = Directory.GetDirectories(Path.Combine(configurationProvider.InstallDirectory, "vscode"), "*.app");
                    if (appDirectories.Length != 1)
                    {
                        throw new InvalidOperationException("Expected exactly one .app directory in the vscode folder.");
                    }

                    return Path.Combine(appDirectories[0], "Contents", "Resources", "app", "bin", "code");
                case Platform.Linux64:
                    return Path.Combine(configurationProvider.InstallDirectory, "vscode", "VSCode-linux-x64", "bin", "code");
                case Platform.LinuxArm64:
                    return Path.Combine(configurationProvider.InstallDirectory, "vscode", "VSCode-linux-arm64", "bin", "code");
                default:
                    throw new PlatformNotSupportedException("Invalid platform");
            }
        }

        private static void SetIfNotSet(string key, object value, JsonObject settingsJson)
        {
            if (settingsJson.ContainsKey(key))
            {
                return;
            }

            if (value is string stringValue)
            {
                settingsJson[key] = stringValue;
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

        private static void SetIfNotSetIgnoreSync(string key, object value, JsonObject settingsJson)
        {
            SetIfNotSet(key, value, settingsJson);
            IgnoreSync(key, settingsJson);
        }

        private static void IgnoreSync(string key, JsonObject settingsJson)
        {
            if (settingsJson.ContainsKey("settingsSync.ignoredSettings"))
            {
                JsonArray? ignoredSettings = settingsJson["settingsSync.ignoredSettings"]?.AsArray();
                bool keyFound = false;
                if (ignoredSettings != null)
                {
                    foreach (JsonNode? result in ignoredSettings)
                    {
                        if (result != null && result.ToString().Equals(key, StringComparison.Ordinal))
                        {
                            keyFound = true;
                        }
                    }

                    if (!keyFound)
                    {
                        ignoredSettings.Add((JsonNode)key);
                        settingsJson["settingsSync.ignoredSettings"] = ignoredSettings;
                    }
                }
            }
            else
            {
                JsonArray? ignoredSettings = new(key);
                settingsJson["settingsSync.ignoredSettings"] = ignoredSettings;
            }
        }

        private static void ConfigureJavaRuntime(string homePath, JsonObject settingsJson)
        {
            if (settingsJson.ContainsKey("java.configuration.runtimes"))
            {
                JsonArray? javaConfigEnv = settingsJson["java.configuration.runtimes"]?.AsArray();
                bool javaFound = false;
                if (javaConfigEnv != null)
                {
                    foreach (JsonNode? result in javaConfigEnv)
                    {
                        if (result == null)
                        {
                            continue;
                        }

                        JsonNode? name = result["name"];
                        if (name != null)
                        {
                            if (name.ToString().Equals("JavaSE-25", StringComparison.OrdinalIgnoreCase))
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
                        JsonObject javaConfigProp = new()
                        {
                            ["name"] = "JavaSE-25",
                            ["path"] = Path.Combine(homePath, "jdk"),
                            ["default"] = true,
                        };
                        javaConfigEnv.Add((JsonNode)javaConfigProp);
                        settingsJson["java.configuration.runtimes"] = javaConfigEnv;
                    }
                }
            }
            else
            {
                JsonArray javaConfigProps = new();
                JsonObject javaConfigProp = new()
                {
                    ["name"] = "JavaSE-25",
                    ["path"] = Path.Combine(homePath, "jdk"),
                    ["default"] = true,
                };
                javaConfigProps.Add((JsonNode)javaConfigProp);
                settingsJson["java.configuration.runtimes"] = javaConfigProps;
            }
        }

        private static void IgnoreExtensionSync(JsonObject settingsJson)
        {
            if (settingsJson.ContainsKey("settingsSync.ignoredExtensions"))
            {
                JsonArray ignoredExtensions = settingsJson["settingsSync.ignoredExtensions"]?.AsArray() ?? new JsonArray();
                bool keyFound = false;
                foreach (JsonNode? result in ignoredExtensions)
                {
                    if (result != null && result.ToString() == "wpilibsuite.vscode-wpilib")
                    {
                        keyFound = true;
                    }
                }

                if (!keyFound)
                {
                    ignoredExtensions.Add((JsonNode)"wpilibsuite.vscode-wpilib");
                    settingsJson["settingsSync.ignoredExtensions"] = ignoredExtensions;
                }
            }
            else
            {
                JsonArray ignoredExtensions = new("wpilibsuite.vscode-wpilib");
                settingsJson["settingsSync.ignoredExtensions"] = ignoredExtensions;
            }
        }
    }
}
