using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using WPILibInstaller.ViewModels;

namespace WPILibInstaller.Views
{
    public partial class FailedPage : UserControl
    {
        public FailedPage()
        {
            this.InitializeComponent();
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }
    }
}
