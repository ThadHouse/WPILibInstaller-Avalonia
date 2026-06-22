using WPILibInstaller.ViewModels;

namespace WPILibInstaller.Interfaces
{
    public interface IViewModelResolver
    {

        T Resolve<T>() where T : notnull, PageViewModelBase;

        IMainWindowViewModel ResolveMainWindow();
    }
}
