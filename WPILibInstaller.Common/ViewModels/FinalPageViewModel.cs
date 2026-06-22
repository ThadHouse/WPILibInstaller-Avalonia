using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using CommunityToolkit.Mvvm.Input;
using WPILibInstaller.Interfaces;

namespace WPILibInstaller.ViewModels
{
    public partial class FinalPageViewModel : PageViewModelBase
    {
        private readonly IProgramWindow progWindow;
        private readonly IConfigurationProvider configurationProvider;
        private readonly bool vsCodeInstalled;

        public string FinishText { get; }

        public FinalPageViewModel(IProgramWindow progWindow, IConfigurationProvider configurationProvider, IVsCodeInstallLocationProvider vsCodeProvider)
            : base("Finish", "")
        {
            vsCodeInstalled = vsCodeProvider.Model.InstallingVsCode;
            if (vsCodeInstalled)
            {
                if (OperatingSystem.IsMacOS())
                {
                    FinishText = "Clicking finish will open the VS Code folder.\nPlease drag this to your dock.";
                }
                else
                {
                    FinishText = "Use the WPILib VS Code desktop icon to start VS Code.";
                }
            }
            else
            {
                FinishText = "";
            }

            // OpenKnownIssues = buttonFactory.CreateCatchableButton(OpenKnownIssuesFunc);
            // OpenChangelog = buttonFactory.CreateCatchableButton(OpenChangelogFunc);
            // OpenBetaDocs = buttonFactory.CreateCatchableButton(OpenBetaDocsFunc);
            // OpenBetaSite = buttonFactory.CreateCatchableButton(OpenBetaSiteFunc);

            this.progWindow = progWindow;
            this.configurationProvider = configurationProvider;
        }

        [RelayCommand]
        public static void OpenKnownIssues()
        {
            OpenBrowser("https://docs.wpilib.org/en/2027/docs/yearly-overview/known-issues.html");
        }

        [RelayCommand]
        public static void OpenBetaDocs()
        {
            OpenBrowser("https://docs.wpilib.org/en/2027");
        }

        [RelayCommand]
        public static void OpenBetaSite()
        {
            OpenBrowser("https://github.com/wpilibsuite/SystemcoreTesting");
        }

        [RelayCommand]
        public static void OpenChangelog()
        {
            string? verString = null;
            try
            {
                var baseDir = AppContext.BaseDirectory;
                verString = File.ReadAllText(Path.Join(baseDir, "WPILibInstallerVersion.txt")).Trim();
            }
            catch
            {
            }

            if (verString != null)
            {
                OpenBrowser($"https://github.com/wpilibsuite/allwpilib/releases/tag/v{verString}");
            }
            else
            {
                OpenBrowser($"https://github.com/wpilibsuite/allwpilib/releases/");
            }
        }

        public override PageViewModelBase MoveNext()
        {
            if (vsCodeInstalled && OperatingSystem.IsMacOS())
            {
                ProcessStartInfo pstart = new ProcessStartInfo("open", Path.Join(configurationProvider.InstallDirectory, "vscode"));
                var p = Process.Start(pstart);
                if (p != null)
                {
                    p.WaitForExit();
                }
            }
            progWindow.CloseProgram();
            return this;
        }

        // Borrowed under MIT license from
        // https://github.com/AvaloniaUI/Avalonia/blob/master/src/Avalonia.Dialogs/AboutAvaloniaDialog.xaml.cs

        public static void OpenBrowser(string url)
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                // If no associated application/json MimeType is found xdg-open opens retrun error
                // but it tries to open it anyway using the console editor (nano, vim, other..)
                ShellExec($"xdg-open {url}");
            }
            else
            {
                using Process? process = Process.Start(new ProcessStartInfo
                {
                    FileName = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? url : "open",
                    Arguments = RuntimeInformation.IsOSPlatform(OSPlatform.OSX) ? $"{url}" : "",
                    CreateNoWindow = true,
                    UseShellExecute = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
                });
            }
        }

        private static void ShellExec(string cmd)
        {
            var escapedArgs = Regex.Replace(cmd, "(?=[`~!#&*()|;'<>])", "\\")
                .Replace("\"", "\\\\\\\"");

            using var process = Process.Start(
                new ProcessStartInfo
                {
                    FileName = "/bin/sh",
                    Arguments = $"-c \"{escapedArgs}\"",
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    WindowStyle = ProcessWindowStyle.Hidden
                }
            );
        }
    }
}
