// Copyright (c) CodeScene. All rights reserved.

using Codescene.VSExtension.Core.Enums.Git;

namespace Codescene.VSExtension.Core.Application.Git
{
    public class FileChangeEvent
    {
        public FileChangeEvent(FileChangeType type, string filePath)
        {
            Type = type;
            FilePath = filePath;
        }

        public FileChangeType Type { get; }

        public string FilePath { get; }
    }
}
