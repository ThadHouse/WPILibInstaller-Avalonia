using WPILibInstaller.Models;

namespace WPILibInstaller.Interfaces
{
    public interface IArchiveExtractionService
    {
        Task ExtractArchive(CancellationToken token, string[]? filter = null, IProgress<InstallProgress>? progress = null);

        Task ExtractJDKAndTools(CancellationToken token, IProgress<InstallProgress>? progress = null);
    }
}
