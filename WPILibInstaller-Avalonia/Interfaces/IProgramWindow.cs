using System.Threading.Tasks;
using Avalonia.Controls;
using WPILibInstaller.ViewModels;

namespace WPILibInstaller.Interfaces
{
    public interface IProgramWindow
    {
        Task<string?> ShowFilePicker(string title, string extensionFilter, string? defaultPath = null);
        Task<string?> ShowFolderPicker(string title, string? initialiDirectory = null);
        Task<MessageDialogResult> ShowMessageDialog(string title, string message, MessageDialogButtons buttons = MessageDialogButtons.Ok);
        void CloseProgram();
    }
}
