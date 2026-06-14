using System.CommandLine;

namespace WPILibInstaller.CLI
{
    internal static class Program
    {
        public static async Task<int> Main(string[] args)
        {
            var allUsersOption = new Option<bool>("--all-users")
            {
                Description = "Create Windows shortcuts with elevation when needed"
            };
            allUsersOption.Aliases.Add("-a");

            var installModeOption = new Option<string>("--install-mode")
            {
                Description = "Installation mode: 'all' or 'tools'",
                DefaultValueFactory = _ => "all"
            };
            installModeOption.AcceptOnlyFromAmong("all", "tools");

            var resourcesOption = new Option<string?>("--resources")
            {
                Description = "Resource ZIP to use instead of auto-detecting",
                Hidden = true
            };
            resourcesOption.Aliases.Add("--resource-file");
            resourcesOption.Aliases.Add("--resources-file");

            var artifactsOption = new Option<string?>("--artifacts")
            {
                Description = "Artifacts archive to use instead of auto-detecting",
                Hidden = true
            };
            artifactsOption.Aliases.Add("--artifact-file");
            artifactsOption.Aliases.Add("--artifacts-file");

            var forceOption = new Option<bool>("--force")
            {
                Description = "Skip the confirmation prompt and start installation"
            };
            forceOption.Aliases.Add("-y");
            forceOption.Aliases.Add("--yes");

            RootCommand rootCommand = new("WPILib Installer - CLI")
            {
                allUsersOption,
                installModeOption,
                resourcesOption,
                artifactsOption,
                forceOption
            };

            rootCommand.Description = """
                Installer files:
                  Auto-detection uses WPILibInstallerVersion.txt and searches the same bundled installer locations as the GUI.

                Offline VS Code:
                  If the VS Code download fails, the CLI checks the installer directory, current directory, and macOS installer volume for a matching archive.
                """;

            rootCommand.SetAction(async parseResult =>
            {
                var allUsers = parseResult.GetValue(allUsersOption);
                var installMode = parseResult.GetRequiredValue(installModeOption).ToLowerInvariant();
                var resourcesFile = parseResult.GetValue(resourcesOption);
                var artifactsFile = parseResult.GetValue(artifactsOption);
                var force = parseResult.GetValue(forceOption);

                await using var installer = new CliInstaller();
                return await installer.RunInstallAsync(allUsers, installMode, resourcesFile, artifactsFile, force);
            });

            var parseResult = rootCommand.Parse(args);
            return await parseResult.InvokeAsync();
        }
    }
}
