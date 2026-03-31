// Copyright (c) CodeScene. All rights reserved.

namespace Codescene.VSExtension.Core.Models.Git
{
    public class GitResult
    {
        public int ExitCode { get; set; }

        public string Output { get; set; }

        public string Error { get; set; }

        public bool Success => ExitCode == 0;
    }
}
