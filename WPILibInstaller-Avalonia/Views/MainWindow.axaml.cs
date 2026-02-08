using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Platform.Storage;
using Microsoft.Extensions.DependencyInjection;
using WPILibInstaller.Interfaces;
using WPILibInstaller.ViewModels;

namespace WPILibInstaller.Views
{
    public partial class MainWindow : Window, IProgramWindow, IViewModelResolver
    {
        public IServiceProvider ServiceProvider { get; }

        public Window Window => this;

        private readonly MainWindowViewModel? viewModel;

        public MainWindow()
        {
            // Initialize our DI
            var services = new ServiceCollection();

            // Register ViewModels as singletons
            services.AddSingleton<CanceledPageViewModel>();
            services.AddSingleton<ConfigurationPageViewModel>();
            services.AddSingleton<FailedPageViewModel>();
            services.AddSingleton<FinalPageViewModel>();
            services.AddSingleton<InstallPageViewModel>();
            services.AddSingleton<MainWindowViewModel>();
            services.AddSingleton<StartPageViewModel>();
            services.AddSingleton<VSCodePageViewModel>();
            services.AddSingleton<DeprecatedOsPageViewModel>();

            // Register this window as interfaces
            services.AddSingleton<IProgramWindow>(this);
            services.AddSingleton<IViewModelResolver>(this);
            services.AddSingleton<IMainWindowViewModel>(sp => sp.GetRequiredService<MainWindowViewModel>());
            services.AddSingleton<IConfigurationProvider>(sp => sp.GetRequiredService<StartPageViewModel>());
            services.AddSingleton<IToInstallProvider>(sp => sp.GetRequiredService<ConfigurationPageViewModel>());
            services.AddSingleton<IVsCodeInstallLocationProvider>(sp => sp.GetRequiredService<VSCodePageViewModel>());

            ServiceProvider = services.BuildServiceProvider();

            InitializeComponent();

            viewModel = ServiceProvider.GetRequiredService<MainWindowViewModel>();
            DataContext = viewModel;
            viewModel.Initialize();
        }

        public void CloseProgram()
        {
            this.Close();
        }

        public async Task<string?> ShowFilePicker(string title, string extensionFilter, string? defaultPath)
        {
            var options = new FilePickerOpenOptions
            {
                Title = title,
                AllowMultiple = false,
                FileTypeFilter = new List<FilePickerFileType>
                {
                    new FilePickerFileType("Archive")
                    {
                        Patterns = new[] { $"*.{extensionFilter}" }
                    }
                }
            };

            if (defaultPath != null)
            {
                options.SuggestedStartLocation = await StorageProvider.TryGetFolderFromPathAsync(defaultPath);
            }

            var result = await StorageProvider.OpenFilePickerAsync(options);
            if (result == null || result.Count != 1) return null;
            return result[0].Path.LocalPath;
        }

        public async Task<string?> ShowFolderPicker(string title, string? initialiDirectory)
        {
            var options = new FolderPickerOpenOptions
            {
                Title = title
            };

            if (initialiDirectory != null)
            {
                options.SuggestedStartLocation = await StorageProvider.TryGetFolderFromPathAsync(initialiDirectory);
            }

            var result = await StorageProvider.OpenFolderPickerAsync(options);
            if (result == null || result.Count != 1) return null;
            return result[0].Path.LocalPath;
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }

        public T Resolve<T>() where T : notnull, PageViewModelBase
        {
            return ServiceProvider.GetRequiredService<T>();
        }

        public IMainWindowViewModel ResolveMainWindow()
        {
            return viewModel!;
        }
    }
}
