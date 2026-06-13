namespace WPILibInstaller.Interfaces
{
    public interface IShortcutService
    {
        Task RunShortcutCreator(CancellationToken token);
    }
}
