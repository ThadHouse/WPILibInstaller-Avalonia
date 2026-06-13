namespace WPILibInstaller.CLI
{
    internal static class Program
    {
        public static async Task<int> Main(string[] args)
        {
            if (args.Contains("--help", StringComparer.Ordinal) || args.Contains("-h", StringComparer.Ordinal))
            {
                PrintHelp();
                return 0;
            }

            bool allUsers = args.Contains("--all-users", StringComparer.Ordinal) || args.Contains("-a", StringComparer.Ordinal);
            string installMode = "all";

            for (int i = 0; i < args.Length; i++)
            {
                if (args[i] == "--install-mode" && i + 1 < args.Length)
                {
                    installMode = args[i + 1].ToLowerInvariant();
                    break;
                }
            }

            await using var installer = new CliInstaller();
            return await installer.RunInstallAsync(allUsers, installMode);
        }

        private static void PrintHelp()
        {
            Console.WriteLine("WPILib Installer - CLI");
            Console.WriteLine();
            Console.WriteLine("Usage:");
            Console.WriteLine("  WPILibInstaller-CLI [options]");
            Console.WriteLine();
            Console.WriteLine("Options:");
            Console.WriteLine("  -a, --all-users            Create Windows shortcuts with elevation when needed");
            Console.WriteLine("  --install-mode <mode>      Installation mode: 'all' or 'tools' (default: all)");
            Console.WriteLine("                             all:   Full installation with VS Code");
            Console.WriteLine("                             tools: JDK + WPILib tools only");
            Console.WriteLine("  -h, --help                 Show this help message");
            Console.WriteLine();
            Console.WriteLine("Offline VS Code:");
            Console.WriteLine("  If the VS Code download fails, the CLI checks the installer directory,");
            Console.WriteLine("  current directory, and macOS installer volume for a matching archive.");
        }
    }
}
