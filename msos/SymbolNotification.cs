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
        private HashSet<string> _errorsAlreadyDisplayed = new HashSet<string>();

        public SymbolNotification(CommandExecutionContext context)
        {
            _context = context;
        }

        public void DecompressionComplete(string localPath)
        {
            _context.WriteInfo("Extracted symbol file to {0}", localPath);
        }

        public void DownloadComplete(string localPath, bool requiresDecompression)
        {
            _context.WriteInfo("Downloaded symbol file {0}", localPath);
        }

        public void DownloadProgress(int bytesDownloaded)
        {
        }

        public void FoundSymbolInCache(string localPath)
        {
            _context.WriteInfo("Symbol file {0} found in symbol cache", localPath);
        }

        public void FoundSymbolOnPath(string url)
        {
            _context.WriteInfo("Symbol file {0} found on symbol path", url);
        }

        public void ProbeFailed(string url)
        {
            if (!_errorsAlreadyDisplayed.Contains(url))
            {
                _context.WriteWarning("Cannot find symbol file {0}", url);
                _errorsAlreadyDisplayed.Add(url);
            }
        }
    }
}
