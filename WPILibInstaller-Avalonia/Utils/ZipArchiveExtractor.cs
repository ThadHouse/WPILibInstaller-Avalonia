using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Threading.Tasks;

namespace WPILibInstaller.Utils
{
    public class ZipArchiveExtractor : IArchiveExtractor
    {
        private ZipArchive archive;
        private IEnumerator<ZipArchiveEntry> entries;


        public ZipArchiveExtractor(Stream stream)
        {
            this.archive = new ZipArchive(stream);
            TotalUncompressSize = (int)archive.Entries.Count;
            entries = archive.Entries.GetEnumerator();
        }

        public long TotalUncompressSize { get; }

        public string EntryKey => entries.Current.FullName;

        public int EntrySize => 1;

        public bool EntryIsDirectory => entries.Current.Name == "";

        public void Dispose()
        {
            GC.SuppressFinalize(this);
        }

        public Task<bool> MoveToNextEntryAsync()
        {
            return Task.FromResult(entries.MoveNext());
        }

        public Task CopyToFileAsync(string path, CancellationToken token)
        {
            return entries.Current.ExtractToFileAsync(path, true, token);
        }

        public bool EntryIsExecutable => false;
    }
}
