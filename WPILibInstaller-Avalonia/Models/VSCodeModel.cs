using System;
using System.Collections.Generic;
using System.IO;
using WPILibInstaller.Utils;

namespace WPILibInstaller.Models
{
    public class VsCodeModel : IDisposable
    {
        public class PlatformData
        {
            public string DownloadUrl { get; }
            public string NameInZip { get; }
            private readonly byte[] hash;
            public ReadOnlySpan<byte> Sha256Hash => hash;

            public PlatformData(string downloadUrl, string nameInZip, string sha256Hash)
            {
                this.DownloadUrl = downloadUrl;
                this.NameInZip = nameInZip;
                this.hash = Convert.FromHexString(sha256Hash);
            }
        }

        public string VSCodeVersion { get; set; }
        public Dictionary<Platform, PlatformData> Platforms { get; } = new Dictionary<Platform, PlatformData>();

        public IArchiveExtractor? ToExtractArchive { get; set; }

        public Stream? ToExtractArchiveMacOs { get; set; }

        public bool AlreadyInstalled { get; set; }

        public VsCodeModel(string vscodeVersion)
        {
            VSCodeVersion = vscodeVersion;
        }

        public bool InstallingVsCode => ToExtractArchive != null || ToExtractArchiveMacOs != null;

        public bool InstallExtensions => AlreadyInstalled || InstallingVsCode;

        public void Dispose()
        {
            GC.SuppressFinalize(this);
            ToExtractArchive?.Dispose();
            ToExtractArchiveMacOs?.Dispose();
        }
    }
}
