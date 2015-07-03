using Microsoft.Diagnostics.Runtime;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace msos
{
    class SymbolNotification : ISymbolNotification
    {
        private CommandExecutionContext _context;

        public SymbolNotification(CommandExecutionContext context)
        {
            _context = context;
        }

        public void DecompressionComplete(string localPath)
        {
        }

        public void DownloadComplete(string localPath, bool requiresDecompression)
        {
            _context.WriteLine("Downloaded symbol file {0}", localPath);
        }

        public void DownloadProgress(int bytesDownloaded)
        {
        }

        public void FoundSymbolInCache(string localPath)
        {
        }

        public void FoundSymbolOnPath(string url)
        {
        }

        public void ProbeFailed(string url)
        {
            _context.WriteWarning("Cannot find symbol file {0}", url);
        }
    }
}
