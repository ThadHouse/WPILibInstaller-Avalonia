using WPILibInstaller.Interfaces;

namespace WPILibInstaller.ViewModels
{
    public class DeprecatedOsPageViewModel : PageViewModelBase
    {
        private readonly IViewModelResolver viewModelResolver;
        private readonly IConfigurationProvider configurationProvider;

        public DeprecatedOsPageViewModel(IViewModelResolver viewModelResolver, IConfigurationProvider configurationProvider)
            : base("I Understand", "")
        {
            this.viewModelResolver = viewModelResolver;
            this.configurationProvider = configurationProvider;

            var version = Environment.OSVersion;
            var bitness = IntPtr.Size == 8 ? "64 Bit" : "32 Bit";
            CurrentSystem = $"Detected {version.VersionString} {bitness}";
            bool isWindows10 = OperatingSystem.IsWindowsVersionAtLeast(10);
            bool is64Bit = IntPtr.Size == 8;
            if (!isWindows10 || !is64Bit)
            {
                DeprecatedMessage1 = "You are using an unsupported Operating System or Architecture";
                DeprecatedMessage2 = "You will not be able to install or run WPILib " + this.configurationProvider.UpgradeConfig.FrcYear;
            }
            else if (isWindows10 && Environment.OSVersion.Version.Build < 22000)
            {
                DeprecatedMessage1 = "You are using Windows 10, which is no longer supported by Microsoft";
                DeprecatedMessage2 = "You may not be able to install or run future year's versions of WPILib";
            }
            else
            {
                DeprecatedMessage1 = "Unknown Error";
                DeprecatedMessage2 = "";
            }

        }

        public string CurrentSystem { get; }

        public string DeprecatedMessage1 { get; }
        public string DeprecatedMessage2 { get; }

        public override PageViewModelBase MoveNext()
        {
            return viewModelResolver.Resolve<ConfigurationPageViewModel>();
        }

    }
}
