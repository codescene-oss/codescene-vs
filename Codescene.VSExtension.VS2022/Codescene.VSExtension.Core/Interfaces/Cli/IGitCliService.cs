// Copyright (c) CodeScene. All rights reserved.

namespace Codescene.VSExtension.Core.Interfaces.Cli
{
    public interface IGitService
    {
        string GetFileContentForCommit(string path);

        bool IsFileIgnored(string filePath);
    }

    public class GitResult
    {
        public int ExitCode { get; set; }

        public string Output { get; set; }

        public string Error { get; set; }

        public bool Success => ExitCode == 0;
    }
}
