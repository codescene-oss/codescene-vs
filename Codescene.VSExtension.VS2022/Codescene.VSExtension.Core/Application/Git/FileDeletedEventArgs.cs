using System;

namespace Codescene.VSExtension.Core.Application.Git
{
    public class FileDeletedEventArgs : EventArgs
    {
        public string FilePath { get; }

        public FileDeletedEventArgs(string filePath)
        {
            FilePath = filePath;
        }
    }
}
