using WPILibInstaller.Models;

namespace WPILibInstaller.Interfaces
{
    public interface IVsCodeInstallationService
    {
        Task RunVsCodeSetup(CancellationToken token, IProgress<InstallProgress>? progress = null);

        Task ConfigureVsCodeSettings();

        Task RunVsCodeExtensionsSetup(IProgress<InstallProgress>? progress = null);
    }
}
