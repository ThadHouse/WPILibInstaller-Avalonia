using WPILibInstaller.Models;

namespace WPILibInstaller.Interfaces
{
    public interface IVsCodeInstallLocationProvider
    {
        VsCodeModel Model { get; }
    }
}
