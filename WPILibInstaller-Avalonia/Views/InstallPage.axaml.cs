using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using WPILibInstaller.ViewModels;

namespace WPILibInstaller.Views
{
    public partial class InstallPage : UserControl
    {
        public InstallPage()
        {
            this.InitializeComponent();
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }
    }
}
