using CommunityToolkit.Mvvm.ComponentModel;

namespace WPILibInstaller.ViewModels
{
    public abstract class PageViewModelBase : ObservableObject
    {
        public string ForwardName { get; }

        public string BackName { get; }

        public virtual bool ForwardVisible => !string.IsNullOrWhiteSpace(ForwardName);
        public bool BackVisible => !string.IsNullOrWhiteSpace(BackName);

        protected PageViewModelBase(string forwardName, string backName)
        {
            ForwardName = forwardName;
            BackName = backName;
        }

        public abstract PageViewModelBase MoveNext();
    }
}
