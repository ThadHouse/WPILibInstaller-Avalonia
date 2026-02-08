using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using WPILibInstaller.ViewModels;

namespace WPILibInstaller.Views
{
    public partial class ConfigurationPage : UserControl
    {
        public ConfigurationPage()
        {
            this.InitializeComponent();
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }
    }
}
