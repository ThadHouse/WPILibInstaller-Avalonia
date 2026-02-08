using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls.Platform;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WPILibInstaller.Interfaces;

namespace WPILibInstaller.ViewModels
{
    public partial class MainWindowViewModel : ObservableObject, IMainWindowViewModel
    {
        private PageViewModelBase currentPage;
        public PageViewModelBase CurrentPage
        {
            get => currentPage;
            set
            {
                pages.Push(value);
                this.SetProperty(ref currentPage, value);
                RefreshForwardBackProperties();
            }
        }

        private readonly Stack<PageViewModelBase> pages = new();

        [ObservableProperty]
        private string? _forwardName;

        [ObservableProperty]
        private string? _backName;

        [ObservableProperty]
        private bool _forwardVisible;

        [ObservableProperty]
        private bool _backVisible;

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

            Dispatcher.UIThread.UnhandledException += (s, e) =>
            {
                Console.WriteLine("UI thread unhandled exception: " + e.Exception);
                HandleException(e.Exception);
                e.Handled = true;
            };
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
