// Copyright (c) CodeScene. All rights reserved.

using System.Collections.Generic;
using Codescene.VSExtension.Core.Enums;
using Codescene.VSExtension.Core.Models.Cli.Refactor;

namespace Codescene.VSExtension.Core.Models.WebComponent.Data
{

    public class Delta
    {
        public decimal ScoreChange { get; set; }

        public decimal NewScore { get; set; }

        public decimal OldScore { get; set; }

        public List<ChangeDetail> FileLevelFindings { get; set; }

        public List<FunctionFinding> FunctionLevelFindings { get; set; }
    }
}
