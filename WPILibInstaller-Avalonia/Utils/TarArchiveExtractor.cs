using System;
using System.Formats.Tar;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Threading.Tasks;

namespace WPILibInstaller.Utils
{
    public class TarArchiveExtractor : IArchiveExtractor
    {
        private readonly TarReader dataStream;
        private TarEntry currentEntry = null!;

        public TarArchiveExtractor(Stream stream, long size)
        {
            TotalUncompressSize = size;
            var gzipStream = new GZipStream(stream, CompressionMode.Decompress);
            dataStream = new TarReader(gzipStream, false);
        }

        public long TotalUncompressSize { get; }

        public string EntryKey => currentEntry.Name;

        public int EntrySize => (int)currentEntry.Length;

        public bool EntryIsDirectory => currentEntry.EntryType == TarEntryType.Directory;

        public void Dispose()
        {
            GC.SuppressFinalize(this);
            dataStream.Dispose();
        }

        public async Task<bool> MoveToNextEntryAsync()
        {
            var entry = await dataStream.GetNextEntryAsync();

            if (entry == null)
            {
                return false;
            }

            this.currentEntry = entry;
            return true;
        }

        public async Task CopyToFileAsync(string path, CancellationToken token)
        {
            await currentEntry.ExtractToFileAsync(path, true, token);
        }

        public bool EntryIsExecutable => currentEntry.Mode.HasFlag(UnixFileMode.UserExecute);
    }
}
