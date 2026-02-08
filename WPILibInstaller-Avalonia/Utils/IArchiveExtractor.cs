using System;
using System.IO;
using System.Threading.Tasks;

namespace WPILibInstaller.Utils
{
    public interface IArchiveExtractor : IDisposable
    {
        Task<bool> MoveToNextEntryAsync();

        long TotalUncompressSize { get; }

        string EntryKey { get; }

        int EntrySize { get; }

        bool EntryIsDirectory { get; }

        bool EntryIsExecutable { get; }

        Task CopyToFileAsync(string path, CancellationToken token);
    }
}
