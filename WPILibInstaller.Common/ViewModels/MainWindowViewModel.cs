using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WPILibInstaller.Interfaces;

namespace WPILibInstaller.ViewModels
{
    public partial class MainWindowViewModel : ObservableObject, IMainWindowViewModel
    {
        public PageViewModelBase CurrentPage
        {
            get;
            set
            {
                pages.Push(value);
                this.SetProperty(ref field, value);
                RefreshForwardBackProperties();
            }
        } = null!;

        private readonly Stack<PageViewModelBase> pages = new();

        [ObservableProperty]
        public partial string? ForwardName { get; set; }

        [ObservableProperty]
        public partial string? BackName { get; set; }

        [ObservableProperty]
        public partial bool ForwardVisible { get; set; }

        [ObservableProperty]
        public partial bool BackVisible { get; set; }

        public void HandleException(Exception e)
        {
            var failedPage = viewModelResolver.Resolve<FailedPageViewModel>();
            failedPage.SetException(e);
            CurrentPage = failedPage;
        }

        [RelayCommand]
        public Task GoNext()
        {
            HandleStateChange();
            return Task.CompletedTask;
        }

        [RelayCommand]
        public Task GoBack()
        {
            pages.Pop();
            CurrentPage = pages.Pop();
            return Task.CompletedTask;
        }

        public void RefreshForwardBackProperties()
        {
            ForwardName = CurrentPage?.ForwardName;
            BackName = CurrentPage?.BackName;
            ForwardVisible = CurrentPage?.ForwardVisible ?? false;
            BackVisible = CurrentPage?.BackVisible ?? false;
        }

        private readonly IViewModelResolver viewModelResolver;

#pragma warning disable CS8618 // Non-nullable field is uninitialized. Consider declaring as nullable.
        public MainWindowViewModel(IViewModelResolver viewModelResolver)
#pragma warning restore CS8618 // Non-nullable field is uninitialized. Consider declaring as nullable.
        {
            PropertyChanged += (s, e) =>
            {
                if (s != CurrentPage)
                {
                    return;
                }
                RefreshForwardBackProperties();
            };

            this.viewModelResolver = viewModelResolver;

        }

        public void Initialize()
        {
            var startPage = viewModelResolver.Resolve<StartPageViewModel>();
            CurrentPage = startPage;
            startPage.Initialize();
        }

        private void HandleStateChange()
        {
            CurrentPage = CurrentPage.MoveNext();
        }

        public Task ExecuteGoNext()
        {
            return GoNextCommand.ExecuteAsync(this);
        }
    }
}
