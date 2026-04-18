namespace WPILibInstaller.Utils
{
    public class IncorrectPlatformException : Exception
    {

        public IncorrectPlatformException(string requested, string current) : base($"Installer {requested} needed for current system.\nCurrent installer is {current}.")
        {

        }
    }
}
