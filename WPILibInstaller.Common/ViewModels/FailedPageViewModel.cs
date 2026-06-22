using CommunityToolkit.Mvvm.ComponentModel;
using WPILibInstaller.Interfaces;

namespace WPILibInstaller.ViewModels
{
    public partial class FailedPageViewModel : PageViewModelBase
    {
        private readonly IProgramWindow progWindow;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(ExceptionText))]
        public partial Exception? CanceledByException { get; set; } = null;

        public string ExceptionText => CanceledByException?.ToString() ?? "";

        public void SetException(Exception ex)
        {
            CanceledByException = ex;
        }

        public FailedPageViewModel(IProgramWindow progWindow)
            : base("Finish", "")
        {
            this.progWindow = progWindow;
        }

        public override PageViewModelBase MoveNext()
        {
            progWindow.CloseProgram();
            return this;
        }
    }
}
