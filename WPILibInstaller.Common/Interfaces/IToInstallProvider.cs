using WPILibInstaller.Models;

namespace WPILibInstaller.Interfaces
{
    public interface IToInstallProvider
    {
        InstallSelectionModel Model { get; }
    }
}
