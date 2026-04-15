using Avalonia.Controls;

namespace WPILibInstaller.Interfaces
{
    public interface IProgramWindow
    {
        Task<string?> ShowFilePicker(string title, string extensionFilter, string? defaultPath = null);
        Task<string?> ShowFolderPicker(string title, string? initialiDirectory = null);
        void CloseProgram();

        Window Window { get; }
    }
}
