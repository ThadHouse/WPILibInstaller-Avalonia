using Avalonia.Controls;
using Avalonia.Controls.Templates;
using WPILibInstaller.ViewModels;

namespace WPILibInstaller.Views;

public class ViewLocator : IDataTemplate
{
    private static readonly Dictionary<Type, Func<Control>> ViewModelToViewMapping = new()
    {
        { typeof(StartPageViewModel), () => new StartPage() },
        { typeof(ConfigurationPageViewModel), () => new ConfigurationPage() },
        { typeof(DeprecatedOsPageViewModel), () => new DeprecatedOsPage() },
        { typeof(VSCodePageViewModel), () => new VSCodePage() },
        { typeof(CanceledPageViewModel), () => new CanceledPage() },
        { typeof(InstallPageViewModel), () => new InstallPage() },
        { typeof(FinalPageViewModel), () => new FinalPage() },
        { typeof(FailedPageViewModel), () => new FailedPage() },
    };

    public Control Build(object? data)
    {
        return ViewModelToViewMapping[data!.GetType()]();
    }

    public bool Match(object? data)
    {
        return data is PageViewModelBase;
    }
}
