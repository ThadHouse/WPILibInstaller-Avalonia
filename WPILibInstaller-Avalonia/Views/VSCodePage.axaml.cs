using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace WPILibInstaller.Views
{
    public partial class VSCodePage : UserControl
    {
        public VSCodePage()
        {
            this.InitializeComponent();
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }
    }
}
