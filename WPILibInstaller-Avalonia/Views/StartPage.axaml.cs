using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace WPILibInstaller.Views
{
    public partial class StartPage : UserControl
    {
        public StartPage()
        {
            this.InitializeComponent();
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }
    }
}
