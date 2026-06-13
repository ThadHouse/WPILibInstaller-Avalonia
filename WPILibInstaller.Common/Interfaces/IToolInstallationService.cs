using WPILibInstaller.Models;

namespace WPILibInstaller.Interfaces
{
    public interface IToolInstallationService
    {
        Task RunGradleSetup(IProgress<InstallProgress>? progress = null);

        Task RunToolSetup(IProgress<InstallProgress>? progress = null);

        Task RunCppSetup(IProgress<InstallProgress>? progress = null);

        Task RunMavenMetaDataFixer(IProgress<InstallProgress>? progress = null);
    }
}
