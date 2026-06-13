namespace WPILibInstaller.Interfaces
{
    public interface IMainWindowViewModel
    {
        void RefreshForwardBackProperties();

        void HandleException(Exception e);

        Task ExecuteGoNext();
    }
}
