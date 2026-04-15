using System;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace WPILibInstaller.Views
{
    public enum MessageDialogButtons
    {
        Ok,
        YesNo,
        OkAbort
    }

    public enum MessageDialogResult
    {
        Ok,
        Yes,
        No,
        Abort
    }

    public partial class MessageDialog : Window
    {
        public string Message { get; }

        // Parameterless constructor required by Avalonia XAML loader
        public MessageDialog()
        {
            Message = "";
            InitializeComponent();
        }

        public MessageDialog(string title, string message, MessageDialogButtons buttons)
        {
            Message = message;
            Title = title;
            DataContext = this;
            InitializeComponent();
            AddButtons(buttons);
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }

        private void AddButtons(MessageDialogButtons buttons)
        {
            var panel = this.FindControl<StackPanel>("ButtonPanel")
                ?? throw new InvalidOperationException("ButtonPanel not found in MessageDialog XAML");

            switch (buttons)
            {
                case MessageDialogButtons.Ok:
                    AddButton(panel, "OK", MessageDialogResult.Ok);
                    break;
                case MessageDialogButtons.YesNo:
                    AddButton(panel, "Yes", MessageDialogResult.Yes);
                    AddButton(panel, "No", MessageDialogResult.No);
                    break;
                case MessageDialogButtons.OkAbort:
                    AddButton(panel, "OK", MessageDialogResult.Ok);
                    AddButton(panel, "Abort", MessageDialogResult.Abort);
                    break;
            }
        }

        private void AddButton(StackPanel panel, string text, MessageDialogResult result)
        {
            var button = new Button
            {
                Content = text,
                MinWidth = 75,
                Padding = new Avalonia.Thickness(10, 5)
            };
            button.Click += (_, _) => Close(result);
            panel.Children.Add(button);
        }

        public static async Task<MessageDialogResult> ShowDialog(Window owner, string title, string message, MessageDialogButtons buttons = MessageDialogButtons.Ok)
        {
            var dialog = new MessageDialog(title, message, buttons);
            return await dialog.ShowDialog<MessageDialogResult>(owner);
        }
    }
}
