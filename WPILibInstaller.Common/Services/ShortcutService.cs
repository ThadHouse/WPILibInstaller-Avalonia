using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;
using WPILibInstaller.Interfaces;
using WPILibInstaller.Models;
using WPILibInstaller.Utils;
using WPILibInstaller.ViewModels;

namespace WPILibInstaller.Services
{
    public sealed class ShortcutService : IShortcutService
    {
        private readonly IConfigurationProvider configurationProvider;
        private readonly IToInstallProvider toInstallProvider;
        private readonly IVsCodeInstallLocationProvider vsInstallProvider;
        private readonly IProgramWindow? programWindow;

        public ShortcutService(
            IConfigurationProvider configurationProvider,
            IToInstallProvider toInstallProvider,
            IVsCodeInstallLocationProvider vsInstallProvider,
            IProgramWindow? programWindow = null)
        {
            this.configurationProvider = configurationProvider;
            this.toInstallProvider = toInstallProvider;
            this.vsInstallProvider = vsInstallProvider;
            this.programWindow = programWindow;
        }

        public async Task RunShortcutCreator(CancellationToken token)
        {
            var shortcutData = new ShortcutData();

            var wpilibHomePath = configurationProvider.InstallDirectory;
            var wpilibYear = configurationProvider.UpgradeConfig.WpilibYear;

            var iconLocation = Path.Join(wpilibHomePath, "icons");
            var wpilibIconLocation = Path.Join(iconLocation, "wpilib-256.ico");

            shortcutData.IsAdmin = toInstallProvider.Model.InstallAsAdmin;

            if (vsInstallProvider.Model.InstallingVsCode)
            {
                shortcutData.DesktopShortcuts.Add(new ShortcutInfo(Path.Join(wpilibHomePath, "vscode", "Code.exe"), $"{wpilibYear} WPILib VS Code", $"{wpilibYear} WPILib VS Code", wpilibIconLocation));
                shortcutData.StartMenuShortcuts.Add(new ShortcutInfo(Path.Join(wpilibHomePath, "vscode", "Code.exe"), $"Programs/{wpilibYear} WPILib VS Code", $"{wpilibYear} WPILib VS Code", wpilibIconLocation));
            }

            shortcutData.DesktopShortcuts.Add(new ShortcutInfo(Path.Join(wpilibHomePath, "tools", "glass.exe"), $"{wpilibYear} WPILib Tools/Glass {wpilibYear}", $"Glass {wpilibYear}", ""));
            shortcutData.DesktopShortcuts.Add(new ShortcutInfo(Path.Join(wpilibHomePath, "tools", "outlineviewer.exe"), $"{wpilibYear} WPILib Tools/OutlineViewer {wpilibYear}", $"OutlineViewer {wpilibYear}", ""));
            shortcutData.DesktopShortcuts.Add(new ShortcutInfo(Path.Join(wpilibHomePath, "tools", "sysid.exe"), $"{wpilibYear} WPILib Tools/SysId {wpilibYear}", $"SysId {wpilibYear}", ""));
            shortcutData.DesktopShortcuts.Add(new ShortcutInfo(Path.Join(wpilibHomePath, "tools", "datalogtool.exe"), $"{wpilibYear} WPILib Tools/Data Log Tool {wpilibYear}", $"Data Log Tool {wpilibYear}", ""));
            shortcutData.DesktopShortcuts.Add(new ShortcutInfo(Path.Join(wpilibHomePath, "tools", "wpical.exe"), $"{wpilibYear} WPILib Tools/WPIcal {wpilibYear}", $"WPIcal {wpilibYear}", ""));
            shortcutData.DesktopShortcuts.Add(new ShortcutInfo(Path.Join(wpilibHomePath, "advantagescope", "AdvantageScope (WPILib).exe"), $"{wpilibYear} WPILib Tools/AdvantageScope (WPILib) {wpilibYear}", $"AdvantageScope (WPILib) {wpilibYear}", ""));
            shortcutData.DesktopShortcuts.Add(new ShortcutInfo(Path.Join(wpilibHomePath, "elastic", "elastic_dashboard.exe"), $"{wpilibYear} WPILib Tools/Elastic (WPILib) {wpilibYear}", $"Elastic (WPILib) {wpilibYear}", ""));

            shortcutData.StartMenuShortcuts.Add(new ShortcutInfo(Path.Join(wpilibHomePath, "tools", "glass.exe"), $"Programs/{wpilibYear} WPILib Tools/Glass {wpilibYear}", $"Glass {wpilibYear}", ""));
            shortcutData.StartMenuShortcuts.Add(new ShortcutInfo(Path.Join(wpilibHomePath, "tools", "outlineviewer.exe"), $"Programs/{wpilibYear} WPILib Tools/OutlineViewer {wpilibYear}", $"OutlineViewer {wpilibYear}", ""));
            shortcutData.StartMenuShortcuts.Add(new ShortcutInfo(Path.Join(wpilibHomePath, "tools", "sysid.exe"), $"Programs/{wpilibYear} WPILib Tools/SysId {wpilibYear}", $"SysId {wpilibYear}", ""));
            shortcutData.StartMenuShortcuts.Add(new ShortcutInfo(Path.Join(wpilibHomePath, "tools", "datalogtool.exe"), $"Programs/{wpilibYear} WPILib Tools/Data Log Tool {wpilibYear}", $"Data Log Tool {wpilibYear}", ""));
            shortcutData.StartMenuShortcuts.Add(new ShortcutInfo(Path.Join(wpilibHomePath, "tools", "wpical.exe"), $"Programs/{wpilibYear} WPILib Tools/WPIcal {wpilibYear}", $"WPIcal {wpilibYear}", ""));
            shortcutData.StartMenuShortcuts.Add(new ShortcutInfo(Path.Join(wpilibHomePath, "advantagescope", "AdvantageScope (WPILib).exe"), $"Programs/{wpilibYear} WPILib Tools/AdvantageScope (WPILib) {wpilibYear}", $"AdvantageScope (WPILib) {wpilibYear}", ""));
            shortcutData.StartMenuShortcuts.Add(new ShortcutInfo(Path.Join(wpilibHomePath, "elastic", "elastic_dashboard.exe"), $"Programs/{wpilibYear} WPILib Tools/Elastic (WPILib) {wpilibYear}", $"Elastic (WPILib) {wpilibYear}", ""));

            if (toInstallProvider.Model.InstallEverything)
            {
                shortcutData.DesktopShortcuts.Add(new ShortcutInfo(Path.Join(wpilibHomePath, "documentation", "frc-docs", "index.html"), $"{wpilibYear} WPILib Documentation", $"{wpilibYear} WPILib Documentation", wpilibIconLocation));
                shortcutData.StartMenuShortcuts.Add(new ShortcutInfo(Path.Join(wpilibHomePath, "documentation", "frc-docs", "index.html"), $"Programs/{wpilibYear} WPILib Documentation", $"{wpilibYear} WPILib Documentation", wpilibIconLocation));
            }

            var serializedData = JsonSerializer.Serialize(shortcutData, SourceGenerationContext.Default.ShortcutData);

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                await RunWindowsShortcutCreator(shortcutData, serializedData, token);
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                await CreateLinuxShortcuts(wpilibYear, token);
            }
        }

        private async Task RunWindowsShortcutCreator(ShortcutData shortcutData, string serializedData, CancellationToken token)
        {
            var tempFile = Path.GetTempFileName();
            await File.WriteAllTextAsync(tempFile, serializedData, token);
            var shortcutCreatorPath = Path.Combine(configurationProvider.InstallDirectory, "installUtils", "WPILibShortcutCreator.exe");

            do
            {
                var startInfo = new ProcessStartInfo(shortcutCreatorPath, $"\"{tempFile}\"")
                {
                    WorkingDirectory = Environment.CurrentDirectory,
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
                        using var process = Process.Start(startInfo);
                        process!.WaitForExit();
                        return process.ExitCode;
                    }
                    catch (Win32Exception ex)
                    {
                        return ex.NativeErrorCode;
                    }
                }, token);

                if (exitCode == 1223)
                {
                    if (programWindow == null)
                    {
                        throw new InvalidOperationException("Shortcut creation cancelled.");
                    }

                    var results = await programWindow.ShowMessageDialog(
                        "UAC Prompt Cancelled",
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
                    if (programWindow != null)
                    {
                        await programWindow.ShowMessageDialog("Shortcut Creation Failed", $"Shortcut creation failed with error code {exitCode}");
                        break;
                    }

                    throw new InvalidOperationException($"Shortcut creation failed with error code {exitCode}");
                }

                break;
            }
            while (true);
        }

        private async Task CreateLinuxShortcuts(string wpilibYear, CancellationToken token)
        {
            if (vsInstallProvider.Model.InstallingVsCode)
            {
                var desktopFile = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Desktop", $@"WPILib VS Code {wpilibYear}.desktop");
                var launcherFile = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".local/share/applications", $@"WPILib_VS_Code_{wpilibYear}.desktop");
                string contents = $@"#!/usr/bin/env xdg-open
[Desktop Entry]
Version=1.0
Type=Application
Categories=Development
Name=WPILib VS Code {wpilibYear}
Comment=Official C++/Java IDE for WPILib for FRC & FTC
Exec={configurationProvider.InstallDirectory}/wpilibcode/wpilibcode{wpilibYear}
Icon={configurationProvider.InstallDirectory}/icons/wpilib-icon-256.png
Terminal=false
StartupNotify=true
StartupWMClass=code
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
                        CreateNoWindow = true,
                    };
                    using var process = Process.Start(startInfo);
                    process!.WaitForExit();
                }, token);
            }

            var installDir = configurationProvider.InstallDirectory;
            await CreateLinuxShortcut("AdvantageScope (WPILib)", $"{installDir}/advantagescope/advantagescope-wpilib", wpilibYear, "AdvantageScope (WPILib)", "advantagescope.png", token);
            await CreateLinuxShortcut("Elastic (WPILib)", $"{installDir}/elastic/elastic_dashboard", wpilibYear, "elastic_dashboard", "elastic.png", token);
            await CreateLinuxShortcut("Glass", "glass", wpilibYear, "Glass - DISCONNECTED", "glass.png", token);
            await CreateLinuxShortcut("OutlineViewer", "outlineviewer", wpilibYear, "OutlineViewer - DISCONNECTED", "outlineviewer.png", token);
            await CreateLinuxShortcut("DataLogTool", "datalogtool", wpilibYear, "Datalog Tool", "datalogtool.png", token);
            await CreateLinuxShortcut("SysId", "sysid", wpilibYear, "System Identification", "sysid.png", token);
            await CreateLinuxShortcut("WPIcal", "wpical", wpilibYear, "WPIcal", "wpical.png", token);
        }

        private async Task CreateLinuxShortcut(string name, string executableName, string wpilibYear, string wmClass, string iconName, CancellationToken token)
        {
            var launcherFile = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".local/share/applications", $@"{name.Replace(' ', '_').Replace(")", "").Replace("(", "")}_{wpilibYear}.desktop");
            string contents = $@"#!/usr/bin/env xdg-open
[Desktop Entry]
Version=1.0
Type=Application
Categories=Robotics;Science
Name={name} {wpilibYear}
Comment={name} tool for the {wpilibYear} FIRST Robotics Competition season
Exec={(Path.IsPathRooted(executableName) || executableName.Contains('/') ? executableName : $"{configurationProvider.InstallDirectory}/tools/{executableName}")}
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
    }
}
