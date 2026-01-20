using System;

namespace Codescene.VSExtension.VS2022.Application.Git
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
