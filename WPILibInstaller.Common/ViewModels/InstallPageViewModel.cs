using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WPILibInstaller.Interfaces;
using WPILibInstaller.Models;

namespace WPILibInstaller.ViewModels
{
#pragma warning disable CA1001 // Types that own disposable fields should be disposable
    public partial class InstallPageViewModel : PageViewModelBase
#pragma warning restore CA1001 // Types that own disposable fields should be disposable
    {
        private readonly IViewModelResolver viewModelResolver;
        private readonly IToInstallProvider toInstallProvider;
        private readonly IArchiveExtractionService archiveExtractionService;
        private readonly IVsCodeInstallationService vsCodeInstallationService;
        private readonly IToolInstallationService toolInstallationService;
        private readonly IShortcutService shortcutService;

        [ObservableProperty]
        public partial int Progress { get; set; }

        [ObservableProperty]
        public partial string Text { get; set; } = "";

        [ObservableProperty]
        public partial int ProgressTotal { get; set; }

        [ObservableProperty]
        public partial string TextTotal { get; set; } = "";

        public bool Succeeded { get; private set; }

        private readonly Task runInstallTask;

        private CancellationTokenSource? source;

        public InstallPageViewModel(
            IViewModelResolver viewModelResolver,
            IToInstallProvider toInstallProvider,
            IArchiveExtractionService archiveExtractionService,
            IVsCodeInstallationService vsCodeInstallationService,
            IToolInstallationService toolInstallationService,
            IShortcutService shortcutService)
            : base("", "")
        {
            this.viewModelResolver = viewModelResolver;
            this.toInstallProvider = toInstallProvider;
            this.archiveExtractionService = archiveExtractionService;
            this.vsCodeInstallationService = vsCodeInstallationService;
            this.toolInstallationService = toolInstallationService;
            this.shortcutService = shortcutService;
            runInstallTask = InstallFunc();

            async Task InstallFunc()
            {
                try
                {
                    await RunInstall();
                }
                catch (Exception e)
                {
                    viewModelResolver.ResolveMainWindow().HandleException(e);
                }
            }
        }

        [RelayCommand]
        public async Task CancelInstall()
        {
            source?.Cancel();
            await runInstallTask;
        }

        private Progress<InstallProgress> CreateProgressReporter()
        {
            return new Progress<InstallProgress>(progress =>
            {
                Progress = progress.Percentage;
                Text = progress.StatusText;
            });
        }

        private void SetOverallProgress(int percentage, string status)
        {
            ProgressTotal = percentage;
            TextTotal = status;
        }

        private async Task InstallTools(CancellationToken token)
        {
            try
            {
                var progress = CreateProgressReporter();
                do
                {
                    SetOverallProgress(0, "Extracting JDK and Tools");
                    await archiveExtractionService.ExtractJDKAndTools(token, progress);
                    if (token.IsCancellationRequested)
                    {
                        break;
                    }

                    SetOverallProgress(33, "Installing Tools");
                    await toolInstallationService.RunToolSetup(progress);
                    if (token.IsCancellationRequested)
                    {
                        break;
                    }

                    SetOverallProgress(66, "Creating Shortcuts");
                    await shortcutService.RunShortcutCreator(token);
                }
                while (false);
            }
            catch (OperationCanceledException)
            {
                // Ignore, as we just want to continue.
            }
        }

        private async Task InstallEverything(CancellationToken token)
        {
            try
            {
                var progress = CreateProgressReporter();
                do
                {
                    SetOverallProgress(0, "Extracting");
                    await archiveExtractionService.ExtractArchive(token, null, progress);
                    if (token.IsCancellationRequested)
                    {
                        break;
                    }

                    SetOverallProgress(11, "Installing Gradle");
                    await toolInstallationService.RunGradleSetup(progress);
                    if (token.IsCancellationRequested)
                    {
                        break;
                    }

                    SetOverallProgress(22, "Installing Tools");
                    await toolInstallationService.RunToolSetup(progress);
                    if (token.IsCancellationRequested)
                    {
                        break;
                    }

                    SetOverallProgress(33, "Installing CPP");
                    await toolInstallationService.RunCppSetup(progress);
                    if (token.IsCancellationRequested)
                    {
                        break;
                    }

                    SetOverallProgress(44, "Fixing Maven");
                    await toolInstallationService.RunMavenMetaDataFixer(progress);
                    if (token.IsCancellationRequested)
                    {
                        break;
                    }

                    SetOverallProgress(55, "Installing VS Code");
                    await vsCodeInstallationService.RunVsCodeSetup(token, progress);
                    if (token.IsCancellationRequested)
                    {
                        break;
                    }

                    SetOverallProgress(66, "Configuring VS Code");
                    await vsCodeInstallationService.ConfigureVsCodeSettings();
                    if (token.IsCancellationRequested)
                    {
                        break;
                    }

                    SetOverallProgress(77, "Installing VS Code Extensions");
                    await vsCodeInstallationService.RunVsCodeExtensionsSetup(progress);
                    if (token.IsCancellationRequested)
                    {
                        break;
                    }

                    SetOverallProgress(88, "Creating Shortcuts");
                    await shortcutService.RunShortcutCreator(token);
                }
                while (false);
            }
            catch (OperationCanceledException)
            {
                // Ignore, as we just want to continue.
            }
        }

        private async Task RunInstall()
        {
            source = new CancellationTokenSource();

            await Task.Yield();

            try
            {
                if (toInstallProvider.Model.InstallTools)
                {
                    await InstallTools(source.Token);
                }
                else
                {
                    await InstallEverything(source.Token);
                }
            }
            catch (OperationCanceledException)
            {
                // Ignore, as we just want to continue.
            }

            Succeeded = !source.IsCancellationRequested;

            await viewModelResolver.ResolveMainWindow().ExecuteGoNext();
        }

        public override PageViewModelBase MoveNext()
        {
            if (Succeeded)
            {
                return viewModelResolver.Resolve<FinalPageViewModel>();
            }

            return viewModelResolver.Resolve<CanceledPageViewModel>();
        }
    }
}
