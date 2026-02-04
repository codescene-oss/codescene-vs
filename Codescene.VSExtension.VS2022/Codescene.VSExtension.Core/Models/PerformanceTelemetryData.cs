// Copyright (c) CodeScene. All rights reserved.

using Codescene.VSExtension.Core.Models.Cli.Refactor;

namespace Codescene.VSExtension.Core.Models
{
    public class PerformanceTelemetryData
    {
        public string Type { get; set; }

        public long ElapsedMs { get; set; }

        public string FilePath { get; set; }

        public int Loc { get; set; }

        public string Language { get; set; }

        public FnToRefactorModel FnToRefactor { get; set; }
    }
}
