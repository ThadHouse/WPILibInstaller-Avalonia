using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using WPILibInstaller.ViewModels;

namespace WPILibInstaller.Views
{
    public partial class FinalPage : UserControl
    {
        public FinalPage()
        {
            this.InitializeComponent();
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }
    }
}
