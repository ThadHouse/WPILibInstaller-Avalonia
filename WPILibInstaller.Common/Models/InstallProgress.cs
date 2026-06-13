namespace WPILibInstaller.Models
{
    public sealed class InstallProgress
    {
        public InstallProgress(int percentage, string statusText)
        {
            Percentage = percentage;
            StatusText = statusText;
        }

        public int Percentage { get; }

        public string StatusText { get; }
    }
}
