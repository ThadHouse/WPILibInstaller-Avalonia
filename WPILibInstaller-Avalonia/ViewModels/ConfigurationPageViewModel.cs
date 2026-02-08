using System.Runtime.InteropServices;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Input;
using WPILibInstaller.Interfaces;
using WPILibInstaller.Models;

namespace WPILibInstaller.ViewModels
{
    public partial class ConfigurationPageViewModel : PageViewModelBase, IToInstallProvider
    {
        private readonly IViewModelResolver viewModelResolver;

        public InstallSelectionModel Model { get; } = new InstallSelectionModel();

        public override bool ForwardVisible => false;

        public bool CanRunAsAdmin { get; } = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);

        public ConfigurationPageViewModel(IViewModelResolver viewModelResolver)
            : base("Install", "Back")
        {
            this.viewModelResolver = viewModelResolver;
        }

        [RelayCommand]
        public async Task InstallLocalUser()
        {
            Model.InstallAsAdmin = false;
            await viewModelResolver.ResolveMainWindow().ExecuteGoNext();
        }

        [RelayCommand]
        public async Task InstallAdmin()
        {
            Model.InstallAsAdmin = true;
            await viewModelResolver.ResolveMainWindow().ExecuteGoNext();
        }

        public override PageViewModelBase MoveNext()
        {
            if (Model.InstallTools)
            {
                return viewModelResolver.Resolve<InstallPageViewModel>();
            }
            else
            {
                return viewModelResolver.Resolve<VSCodePageViewModel>();
            }
        }
    }
}
