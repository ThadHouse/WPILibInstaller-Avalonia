using System;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using WPILibInstaller.ViewModels;

namespace WPILibInstaller.Views
{
    public partial class DeprecatedOsPage : UserControl
    {
        public DeprecatedOsPage()
        {
            this.InitializeComponent();
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }
    }
}
